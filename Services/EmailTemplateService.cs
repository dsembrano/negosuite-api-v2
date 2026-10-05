using System;
using System.Net;
using Microsoft.Extensions.Configuration;
using negosuite_api.Contracts.Email;

namespace negosuite_api.Services;

public sealed class EmailTemplateService
    {
        private string InviteNewMemberTemplate(EmailWorkflowData payload, IConfiguration config)
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
                          href=""{Link("accept-member", payload.Identifier)}"" target=""_blank"">Join our Negosuite Team</a>
                  </div>
                </body>";
        }

        private string ResetPasswordTemplate(EmailWorkflowData payload, IConfiguration config)
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
                          href=""{Link("reset-password", payload.Identifier)}"" target=""_blank"">Reset your password</a>
                  </div>
                </body>";
        }

        private string EmailConfirmationTemplate(EmailWorkflowData payload, IConfiguration config)
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
                            href=""{Link("activate-account", payload.Identifier)}"" target=""_blank"">Activate your account</a>
                    </div>
                </body>";
        }

    
        private readonly IConfiguration configuration;
        public EmailTemplateService(IConfiguration configuration) => this.configuration = configuration;
        public string Render(EmailWorkflowData data, string action)
        {
            var safe = new EmailWorkflowData { RecipientName = WebUtility.HtmlEncode(data.RecipientName), CompanyName = WebUtility.HtmlEncode(data.CompanyName),
                SenderName = WebUtility.HtmlEncode(data.SenderName), Identifier = data.Identifier, ExpiryDate = data.ExpiryDate };
            return action switch
            {
                EmailWorkflowService.InviteAction => InviteNewMemberTemplate(safe, configuration),
                EmailWorkflowService.ResetAction => ResetPasswordTemplate(safe, configuration),
                EmailWorkflowService.ActivationAction => EmailConfirmationTemplate(safe, configuration),
                _ => throw new ArgumentException("Unknown email action.", nameof(action))
            };
        }
        public string RegistrationNotice(EmailWorkflowData data) => "<p>New registration: " + WebUtility.HtmlEncode(data.RecipientName) + " (" + WebUtility.HtmlEncode(data.Recipients[0]) + ")</p>";
        private string Link(string path, string identifier)
        {
            var configured = configuration["AppUrl"];
            if (!Uri.TryCreate(configured, UriKind.Absolute, out var root) || (root.Scheme != "https" && root.Scheme != "http") || !string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment))
                throw new AdministrationException("Application URL is not configured.", 503);
            return WebUtility.HtmlEncode(configured.TrimEnd('/') + "/" + path + "?id=" + Uri.EscapeDataString(identifier));
        }

}
