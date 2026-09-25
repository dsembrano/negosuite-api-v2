using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;

namespace negosuite_api.Controllers
{
    [Authorize]
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        public static bool STATUS_INACTIVE = false;
        public static bool STATUS_ACTIVE = true;

        public static byte STATUS_OPEN = 1;
        public static byte STATUS_CLOSE = 0;

        public static byte APPLICATION_USER = 1;
        public static byte CONFIG_ADMIN = 2;

        private readonly negosuiteContext _context;

        public UsersController(negosuiteContext context)
        {
            _context = context;
        }


        [HttpGet]
        public async Task<ActionResult> GetUsers()
        {

            var result = await _context.Users.Where(u => u.Status ==  STATUS_ACTIVE)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.MobileNo,
                    u.UserTypeId,
                    UserTypeName = u.UserType.Name,
                    u.Avatar,
                    u.Status,
                    u.UserRoleId,
                    UserRoleName = u.UserRole.Name,
                    u.UserRole,
                    u.UserUIConfig
                }).OrderBy(e => e.Name).ToListAsync();

            return Ok(result);
        }


        [Route("config")]
        [HttpGet]
        public async Task<ActionResult> GetConfigUsers(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.Users
                .Where(u => u.ConfigId == selectCriteria.UserConfigId)
                .Where(u => u.Status == STATUS_ACTIVE && u.UserTypeId != CONFIG_ADMIN)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.MobileNo,
                    u.UserTypeId,
                    UserTypeName = u.UserType.Name,
                    u.Avatar,
                    u.Status,
                    u.UserRoleId,
                    UserRoleName = u.UserRole.Name,
                    u.UserRole,
                    u.UserUIConfig
                }).OrderBy(e => e.Name).ToListAsync();

            return Ok(result);
        }


        [AllowAnonymous]
        [Route("config-can-add")]
        [HttpGet]
        public async Task<ActionResult> GetConfigCanAdd(int configId)
        {
            var config = await _context.Configs.FindAsync(configId);

            var userCount = await _context.Users
                .Where(u => u.ConfigId == configId)
                .Where(u => u.Status == STATUS_ACTIVE && u.UserTypeId != CONFIG_ADMIN)
                .CountAsync();

            return Ok(userCount < config.MaxUserCount);
        }


        [HttpGet]
        [Route("roles")]
        public async Task<ActionResult> GetUserRoles(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.UserRoles
                .Where(u => u.UserConfigId == selectCriteria.UserConfigId || u.UserConfigId == null).ToListAsync();
            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return user;

        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest();
            }

            var _user =  await _context.Users.FindAsync(id);
            if (_user == null)
            {
                return NotFound();
            }

            _user.Name = user.Name;
            _user.Email = user.Email;
            _user.UserRoleId = user.UserRoleId;

            _context.Entry(_user).State = EntityState.Modified;

            try
            {
                _user.LastUpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }


        [Route("update-role")]
        [HttpPut]
        public async Task<IActionResult> PutUserRole(int id, int userRoleId)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                user.LastUpdatedDate = DateTime.UtcNow;
                user.UserRoleId = userRoleId;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }



        [Route("update-ui-config")]
        [HttpPut]
        public async Task<IActionResult> PutUserUIConfig(int id, UserUIConfig userUIConfig)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                user.LastUpdatedDate = DateTime.UtcNow;
                user.UserUIConfig = userUIConfig.userUIConfigString;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }



        [Route("update-config")]
        [HttpPut]
        public async Task<ActionResult> PutUserConfig(int id, Config config)
        {

            var userRole = await _context.UserRoles.Where(u => u.IsAdmin == true).FirstOrDefaultAsync();

            if (userRole == null)
            {
                return BadRequest();
            }

            var configTemplate = await this._context.Configs.Where(c => c.IsTemplate == true).FirstOrDefaultAsync();
            if (configTemplate == null)
            {
                return BadRequest();
            }

            try
            {
                var userId = id.ToString();
                var userRoleId = userRole.Id.ToString();
                var configId = configTemplate.Id.ToString();
                var companyName = config.CompanyName;
                var address1 = config.Address1; 
                var phoneNo = config.PhoneNo; 
                var email = config.Email; 
                var website = config.Website; 
                var tin = config.Tin; 
                var industryId = config.IndustryId != null ? config.IndustryId.ToString() : null; 
                var companyAbout = config.CompanyAbout; 
                var countryId = config.CountryId.ToString();
                var taxRates = config.TaxRatesJson;

                var subscriptionPlanId = config.SubscriptionPlanId;
                var subscriptionDate = config.SubscriptionDate?.ToString("yyyy-MM-dd");
                var trial = config.Trial;
                var trialEndDate = config.TrialEndDate?.ToString("yyyy-MM-dd");
                var billingMode = config.BillingMode;
                var maxUserCount = config.MaxUserCount.ToString();

                var result = await _context.Configs
                    .FromSqlInterpolated($@"CALL CreateUserConfig(
                        {userId}, 
                        {userRoleId},
                        {configId}, 
                        {companyName},
                        {address1}, 
                        {phoneNo}, 
                        {email}, 
                        {website}, 
                        {tin}, 
                        {industryId}, 
                        {companyAbout}, 
                        {countryId}, 
                        {taxRates},
                        {subscriptionPlanId},
                        {subscriptionDate},
                        {trial},
                        {trialEndDate},
                        {billingMode},
                        {maxUserCount})"
                    ).ToListAsync();

                var baseCurrency = await _context.Currencies.Where(c => c.IsBase).FirstOrDefaultAsync();

                var user = await _context.Users.Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.MobileNo,
                    u.UserTypeId,
                    UserTypeName = u.UserType.Name,
                    u.Avatar,
                    u.Status,
                    u.ConfigId,
                    u.Config,
                    BaseCurrency = baseCurrency,
                    u.UserRoleId,
                    u.UserRole
                }).FirstOrDefaultAsync();

                return Ok(user);

            }
            catch (Exception ex)
            {
                Console.WriteLine("MySQL exception occurred: " + ex.Message);
                return BadRequest(ex.Message);
            }            

        }


        [Route("update-config-template")]
        [HttpPut]
        public async Task<ActionResult> PutUserConfigFromTemplate(int id, Config config)
        {
            /*
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound("User not found!");
            }*/

            var userRole = await _context.UserRoles.Where(u => u.IsAdmin == true).FirstOrDefaultAsync();

            if (userRole == null)
            {
                return BadRequest();
            }

            try
            {
                var userId = id.ToString();
                var userRoleId = userRole.Id.ToString();
                var configId = config.Id.ToString();
                var companyName = config.CompanyName;
                var address1 = config.Address1;
                var phoneNo = config.PhoneNo;
                var email = config.Email;
                var website = config.Website;
                var tin = config.Tin;
                var industryId = config.IndustryId != null ? config.IndustryId.ToString() : null;
                var companyAbout = config.CompanyAbout;
                var countryId = config.CountryId.ToString();
                var taxRates = config.TaxRatesJson;

                var subscriptionPlanId = config.SubscriptionPlanId;
                var subscriptionDate = config.SubscriptionDate?.ToString("yyyy-MM-dd");
                var trial = config.Trial;
                var trialEndDate = config.TrialEndDate?.ToString("yyyy-MM-dd");
                var billingMode = config.BillingMode;
                var maxUserCount = config.MaxUserCount.ToString();

                var result = await _context.Configs
                    .FromSqlInterpolated($@"CALL CreateUserConfigFromTemplate(
                        {userId}, 
                        {userRoleId},
                        {configId}, 
                        {companyName},
                        {address1}, 
                        {phoneNo}, 
                        {email}, 
                        {website}, 
                        {tin}, 
                        {industryId}, 
                        {companyAbout}, 
                        {countryId}, 
                        {taxRates},
                        {subscriptionPlanId},
                        {subscriptionDate},
                        {trial},
                        {trialEndDate},
                        {billingMode},
                        {maxUserCount})"
                    ).ToListAsync();

                var baseCurrency = await _context.Currencies.Where(c => c.IsBase).FirstOrDefaultAsync();

                var user = await _context.Users.Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.MobileNo,
                    u.UserTypeId,
                    UserTypeName = u.UserType.Name,
                    u.Avatar,
                    u.Status,
                    u.ConfigId,
                    u.Config,
                    BaseCurrency = baseCurrency,
                    u.UserRoleId,
                    u.UserRole
                }).FirstOrDefaultAsync();

                return Ok(user);

            }
            catch (Exception ex)
            {
                Console.WriteLine("MySQL exception occurred: " + ex.Message);
                return BadRequest(ex.Message);
            }

        }



        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            if (user.Identifier == null)
            {
                return BadRequest();
            }

            if (_context.Users.Any(e => e.Email == user.Email))
            {
                return BadRequest("Email address already exist.");
            }

            var emailLog = await _context.EmailLogs
                .Where(e => e.Uuid == user.Identifier && e.Status == STATUS_OPEN).FirstOrDefaultAsync();

            if (emailLog == null)
            {
                return BadRequest();
            }

            _context.Entry(emailLog).State = EntityState.Modified;
            emailLog.Status = STATUS_CLOSE;
            emailLog.LastUpdatedDate = DateTime.UtcNow;


            // Check if number of users does not exist sunscription limit /////////////////////////
            var config = await _context.Configs.FindAsync(user.ConfigId);
            var userCount = await _context.Users
                .Where(u => u.ConfigId == user.ConfigId && u.Status == STATUS_ACTIVE && u.UserTypeId != CONFIG_ADMIN).CountAsync();

            if (userCount >= config.MaxUserCount)
            {
                await _context.SaveChangesAsync();
                return BadRequest("Your team has reached the maximum number of users allowed for the subscription plan.");
            }
            ///////////////////////////////////////////////////////////////////////////////////////

            user.Email = emailLog.Email;
            user.CreatedDate = DateTime.UtcNow;
            user.Status = STATUS_ACTIVE;
            user.Password = AuthController.CalculateSha256Hash(user.Password);
            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            //var createdUser = await _context.Users.FindAsync(user.Id);
            return CreatedAtAction("GetUser", new { id = user.Id }, user);
        }


        [Route("new-account")]
        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<User>> PostNewUserAccount(User user)
        {
            if (user.Identifier == null)
            {
                return BadRequest();
            }

            if (_context.Users.Any(e => e.Email == user.Email))
            {
                return BadRequest("Email address already exist.");
            }

            var emailLog = await _context.EmailLogs
                .Where(e => e.Uuid == user.Identifier && e.Status == STATUS_OPEN).FirstOrDefaultAsync();

            if (emailLog == null)
            {
                return BadRequest();
            }

            if (emailLog.Action != "new-account")
            {
                return BadRequest();
            }

            _context.Entry(emailLog).State = EntityState.Modified;
            emailLog.Status = STATUS_CLOSE;
            emailLog.LastUpdatedDate = DateTime.UtcNow;

            user.Email = emailLog.Email;
            user.CreatedDate = DateTime.UtcNow;
            user.Status = STATUS_ACTIVE;
            user.Password = user.Password;
            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            //var createdUser = await _context.Users.FindAsync(user.Id);
            return CreatedAtAction("GetUser", new { id = user.Id }, user);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.Email = user.Email + "_" + DateTime.UtcNow.ToString("u");
            user.Status = STATUS_INACTIVE;

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                user.LastUpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();

        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

    }


    public class UserUIConfig
    {
        public string userUIConfigString { get; set; }
    }

}
