
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using negosuite_api.Services;
using negosuite_api.Models;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;

namespace negosuite_api.Controllers
{
    [Authorize]
    [Route("api/email")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        private readonly negosuiteContext _context;

        public static bool STATUS_ACTIVE = true;
        public static byte STATUS_OPEN = 1;
        public static byte STATUS_CLOSE = 0;

        public static string ACTION_MEMBER_INVITE = "member-invite";
        public static string ACTION_RESET_PASSWORD = "password-reset";
        public static string ACTION_NEW_ACCOUNT = "new-account";

        public EmailController(IEmailService emailService, IConfiguration config, negosuiteContext context)
        {
            _emailService = emailService;
            _config = config;
            _context = context;
        }

        [AllowAnonymous]
        [Route("log/{uuid}")]
        [HttpGet]
        public async Task<ActionResult<EmailLog>> GetEmailLog(string uuid)
        {
            var nowUTC = DateTime.Now;
            var emailLog = await _context.EmailLogs
                .Where(e => e.Uuid == uuid && e.Status == STATUS_OPEN && e.ExpiryDate > nowUTC).FirstOrDefaultAsync();

            if (emailLog == null)
            {
                return NotFound("This link is no longer valid or has aleady expired.");
            }

            return emailLog;
        }

        [HttpPost]
        public IActionResult SendEmail([FromBody] EmailPayload payload)
        {
            try
            {
                _emailService.SendEmail(payload);
                return Ok();
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


		[Route("member-invite")]
		[HttpPost]
		public async Task<IActionResult> SendMemberInvite([FromBody] EmailPayload payload)
		{
            var actor = await new CompanyAccessService(_context).CurrentAsync(User, HttpContext.RequestAborted);
            if (actor == null) return Unauthorized();
            if (!CompanyAccessService.IsAdmin(actor) || payload.ConfigId != actor.ConfigId) return Forbid();
            if (payload.Recipients == null || payload.Recipients.Count == 0 || string.IsNullOrWhiteSpace(payload.Recipients[0])) return BadRequest("A recipient is required.");
            if (payload.UserRoleId.HasValue && !await _context.UserRoles.AnyAsync(r => r.Id == payload.UserRoleId && (r.UserConfigId == actor.ConfigId || r.UserConfigId == null)))
                return BadRequest("Role must belong to this company or be a shared system role.");
            if (!Guid.TryParse(payload.Identifier, out _)) return BadRequest("A valid invitation identifier is required.");
            payload.ExpiryDate = DateTime.Now.AddDays(7);
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };

            payload.Subject = $"{payload.SenderName} invites you to join their Negosuite team";
            payload.Message = EmailTeamplates.InviteNewMemberTemplate(payload, _config);

            var emailLog = new EmailLog
            {
                Email = payload.Recipients[0],
                Uuid = payload.Identifier,
                ConfigId = (int)payload.ConfigId,
                Data = JsonConvert.SerializeObject(payload, settings),
                Status = STATUS_OPEN,
                CreatedDate = DateTime.Now,
                ExpiryDate = payload.ExpiryDate,
                Action = ACTION_MEMBER_INVITE
            };

            _context.EmailLogs.Add(emailLog);
            _context.SaveChanges();

            try
            {
                _emailService.SendEmail(payload);
				return Ok();
			}
			catch (System.Exception ex)
			{
				return BadRequest(ex.Message);
			}
		}


        [AllowAnonymous]
        [Route("email-confirmation")]
        [HttpPost]
        public IActionResult EmailConfirmation([FromBody] EmailPayload payload)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };


            if (_context.Users.Any(e => e.Email == payload.Recipients[0]))
            {
                return BadRequest("Email address already exist.");
            }


            payload.Subject = "Negosuite - Emmail Confirmation";
            payload.Message = EmailTeamplates.EmailConfirmationTemplate(payload, _config);

            var notificationRecipients = _config["NotificationRecipients"].Split(';').Select(s => s.Trim());

            payload.Recipients.AddRange(notificationRecipients);            

            payload.Password = AuthController.CalculateSha256Hash(payload.Password);

            var emailLog = new EmailLog
            {
                Email = payload.Recipients[0],
                Uuid = payload.Identifier,
                ConfigId = payload.ConfigId,
                Data = JsonConvert.SerializeObject(payload, settings),
                Status = STATUS_OPEN,
                CreatedDate = DateTime.Now,
                ExpiryDate = payload.ExpiryDate,
                Action = ACTION_NEW_ACCOUNT
            };

            _context.EmailLogs.Add(emailLog);
            _context.SaveChanges();

            try
            {
                _emailService.SendEmail(payload);
                return Ok();
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }



        [AllowAnonymous]
        [Route("password-reset")]
        [HttpPost]
        public async Task<IActionResult> SendPasswordReset([FromBody] EmailPayload payload)
        {

            var user = await _context.Users.Where(u => u.Email == payload.Recipients[0] && u.Status == STATUS_ACTIVE).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound("Email not found. Are you sure you are already a member?");
            }

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };

            payload.Subject = "Reset your password";
            payload.RecipientName = user.Name;
            payload.Message = EmailTeamplates.ResetPasswordTemplate(payload, _config);

            var emailLog = new EmailLog
            {
                Email = payload.Recipients[0],
                Uuid = payload.Identifier,
                Data = JsonConvert.SerializeObject(payload, settings),
                Status = STATUS_OPEN,
                CreatedDate = DateTime.Now,
                ExpiryDate = payload.ExpiryDate,
                Action = ACTION_RESET_PASSWORD
            };

            _context.EmailLogs.Add(emailLog);
            _context.SaveChanges();

            try
            {
                _emailService.SendEmail(payload);
                return Ok();
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

    }

    static public class EmailTeamplates
    {
        public static string InviteNewMemberTemplate(EmailPayload payload, IConfiguration config)
        {
            return @$"
                <body style=""padding: 24px; background-color: #f7f7f7; font-family: 'Inter var', ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,'Noto Sans',sans-serif,'Apple Color Emoji','Segoe UI Emoji','Segoe UI Symbol','Noto Color Emoji'; font-size: 16px; line-height: 1.5; text-align: left; color: grey;"">    
                  <div style=""max-width: 600px; margin: 0 auto; padding: 40px; background-color: #fff; border-radius: 8px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);"">
                      <img style=""display: block; margin: 0 auto; max-width: 200px;"" src=""https://negosuite-web-app.sgp1.cdn.digitaloceanspaces.com/assets%2Fimages%2Flogo%2Fnegosuite_logo_240x87.png"" alt="""">    
                      <p style=""font-size: 24px; margin-top: 50px;"">Hi {payload.RecipientName},</p>
                      <p style=""margin-top: 20px; margin-bottom: 30px; font-weight: 400;"">We at <span style=""font-size: 24px;"">{payload.CompanyName},</span> have implemented Negosuite as our accounting system and we would like to extend an invitation for you to join our Negosuite team. 
                      <br><br>By clicking on the button below, you will be brought to the member confirmation page so you can gain immediate access to our account and start using the system.</p>
                      <p style=""font-size: 16px; margin: 0;"">Sincerely,</p>                
                      <p style=""font-size: 24px; margin: 24px 0 0;"">{payload.SenderName}</p>        
                      <p style=""font-size: 16px; margin: 0 0 50px;"">Team Admin</p>        
                      <a style=""display: block; margin: 0 auto; padding: 10px 20px; background-color: #008cba; color: #fff; font-weight: bold; border-radius: 4px; text-decoration: none; transition: background-color 0.2s ease-in-out; text-align: center; max-width: 200px;""        
                          href=""{config["AppUrl"]}accept-member?id={payload.Identifier}"" target=""_blank"">Join our Negosuite Team</a>
                  </div>
                </body>";
        }

        public static string ResetPasswordTemplate(EmailPayload payload, IConfiguration config)
        {
            return @$"
                <body style=""padding: 24px; background-color: #f7f7f7; font-family: 'Inter var', ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,'Noto Sans',sans-serif,'Apple Color Emoji','Segoe UI Emoji','Segoe UI Symbol','Noto Color Emoji'; font-size: 16px; line-height: 1.5; text-align: left; color: grey;"">    
                  <div style=""max-width: 600px; margin: 0 auto; padding: 40px; background-color: #fff; border-radius: 8px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);"">
                      <img style=""display: block; margin: 0 auto; max-width: 200px;"" src=""https://negosuite-web-app.sgp1.cdn.digitaloceanspaces.com/assets%2Fimages%2Flogo%2Fnegosuite_logo_240x87.png"" alt="""">    
                      <p style=""font-size: 24px; margin-top: 50px;"">Hi {payload.RecipientName},</p>
                      <p style=""margin-top: 20px; margin-bottom: 30px; font-weight: 400;"">We've received a request to reset your password.
                      <br><br><span style=""font-weight: 400;"">If you didn't make the request, just ignore this message. Otherwise, you can reset your password by clicking on the link below.</span></p>
                      <p style=""font-weight: 400; margin: 0 0 50px;"">Thank you,
                      <br>The Negosuite team</p>        
                      <a style=""display: block; margin: 0 auto; padding: 10px 20px; background-color: #008cba; color: #fff; font-weight: bold; border-radius: 4px; text-decoration: none; transition: background-color 0.2s ease-in-out; text-align: center; max-width: 200px;""        
                          href=""{config["AppUrl"]}reset-password?id={payload.Identifier}"" target=""_blank"">Reset your password</a>
                  </div>
                </body>";
        }

        public static string EmailConfirmationTemplate(EmailPayload payload, IConfiguration config)
        {
            var expiry = (DateTime)payload.ExpiryDate;
            return @$"
                <body style=""padding: 24px; background-color: #f7f7f7; font-family: 'Inter var', ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,'Noto Sans',sans-serif,'Apple Color Emoji','Segoe UI Emoji','Segoe UI Symbol','Noto Color Emoji'; font-size: 16px; line-height: 1.5; text-align: left; color: grey;"">    
                    <div style=""max-width: 600px; margin: 0 auto; padding: 40px; background-color: #fff; border-radius: 8px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);"">
                        <img style=""display: block; margin: 0 auto; max-width: 200px;"" src=""https://negosuite-web-app.sgp1.cdn.digitaloceanspaces.com/assets%2Fimages%2Flogo%2Fnegosuite_logo_240x87.png"" alt="""">    
                        <p style=""font-size: 24px; margin-top: 50px;"">Hi {payload.RecipientName},</p>
                        <p style=""margin-top: 20px; margin-bottom: 30px; font-weight: 400;"">Thank you for joining Negosuite. 
                        <br><br>Just one last step to complete your registration, please click the button below to confirm your email address and activate your account.
                        <br><br>Please note that confirmation of your email and activation of account will expire on {expiry.ToString("dddd, MMMM dd yyyy h:mm tt")}.</p>                        
                        <p style=""font-size: 16px; margin: 0;"">Sincerely,</p>                
                        <p style=""font-size: 24px; margin: 24px 0 50px;"">Team Negosuite</p>        
                        <a style=""display: block; margin: 0 auto; padding: 10px 20px; background-color: #008cba; color: #fff; font-weight: bold; border-radius: 4px; text-decoration: none; transition: background-color 0.2s ease-in-out; text-align: center; max-width: 200px;""        
                            href=""{config["AppUrl"]}activate-account?id={payload.Identifier}"" target=""_blank"">Activate your account</a>
                    </div>
                </body>";
        }

    }

}
