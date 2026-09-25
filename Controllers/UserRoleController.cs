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
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/user-roles")]
    [ApiController]
    public class UserRoleController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public UserRoleController(negosuiteContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult> GetUserRoles(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.UserRoles
                .Where(u => u.UserConfigId == selectCriteria.UserConfigId || u.UserConfigId == null)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Notes,
                    u.Permission,
                    u.IsAdmin
                }).OrderBy(e => e.Name).ToListAsync();

            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<UserRole>> GetUserRole(int id)
        {
            var userRole = await _context.UserRoles.FindAsync(id);

            if (userRole == null)
            {
                return NotFound();
            }

            return userRole;

        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutUserRole(int id, UserRole userRole)
        {
            if (id != userRole.Id)
            {
                return BadRequest();
            }

            _context.Entry(userRole).State = EntityState.Modified;

            try
            {
                userRole.LastUpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserRoleExists(id))
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


        [HttpPost]
        public async Task<ActionResult<UserRole>> PostUser(UserRole userRole)
        {
            userRole.CreatedDate = DateTime.Now;

            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUserRole", new { id = userRole.Id }, userRole);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUserRole(int id)
        {
            var useCount = await _context.Users.Where(u => u.UserRoleId == id).CountAsync();
            if (useCount > 0)
            {
                return BadRequest("Unable to delete user role becuase it is currently in use.");
            }

            var userRole = await _context.UserRoles.FindAsync(id);
            if (userRole == null)
            {
                return NotFound();
            }

            _context.UserRoles.Remove(userRole);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool UserRoleExists(int id)
        {
            return _context.UserRoles.Any(e => e.Id == id);
        }

    }
}
