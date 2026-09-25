using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/responsibility-centers")]
    [ApiController]
    public class ResponsibilityCentersController : ControllerBase
    {
        private readonly negosuiteContext _context;
        public static bool STATUS_INACTIVE = false;
        public static bool STATUS_ACTIVE = true;

        public ResponsibilityCentersController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/ResponsibilityCenters
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ResponsibilityCenter>>> GetResponsibilityCenters(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            return await _context.ResponsibilityCenters
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                //.Where(c => selectCriteria != null && selectCriteria.ShowInactive == true ? true : c.Status == STATUS_ACTIVE)
                .OrderBy(e => e.Name).ToListAsync();
        }

        // GET: api/ResponsibilityCenters
        [HttpGet]
        [Route("type")]
        public async Task<ActionResult<IEnumerable<ResponsibilityCenter>>> GetResponsibilityCentersByType(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            return await _context.ResponsibilityCenters
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .Where(c => selectCriteria != null && selectCriteria.ShowInactive == true ? true : c.Status == STATUS_ACTIVE)
                .Where(r => r.ResponsibilityCenterTypeId == selectCriteria.ResponsibilityCenterTypeId)
                .OrderBy(e => e.Name).ToListAsync();
        }

        // GET: api/ResponsibilityCenters/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ResponsibilityCenter>> GetResponsibilityCenter(int id)
        {
            var responsibilityCenter = await _context.ResponsibilityCenters.FindAsync(id);

            if (responsibilityCenter == null)
            {
                return NotFound();
            }

            return responsibilityCenter;
        }

        // PUT: api/ResponsibilityCenters/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutResponsibilityCenter(int id, ResponsibilityCenter responsibilityCenter)
        {
            if (id != responsibilityCenter.Id)
            {
                return BadRequest();
            }

            responsibilityCenter.LastUpdatedDate = DateTime.UtcNow;
            _context.Entry(responsibilityCenter).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ResponsibilityCenterExists(id))
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

        // POST: api/ResponsibilityCenters
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ResponsibilityCenter>> PostResponsibilityCenter(ResponsibilityCenter responsibilityCenter)
        {
            responsibilityCenter.CreatedDate = DateTime.UtcNow;
            _context.ResponsibilityCenters.Add(responsibilityCenter);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetResponsibilityCenter", new { id = responsibilityCenter.Id }, responsibilityCenter);
        }

        // DELETE: api/ResponsibilityCenters/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResponsibilityCenter(int id)
        {
            var responsibilityCenter = await _context.ResponsibilityCenters.FindAsync(id);
            if (responsibilityCenter == null)
            {
                return NotFound();
            }

            var json = $"{{\"id\": {id}}}";

            var record = await _context.JournalEntries
                .FromSqlInterpolated($@"
                    SELECT * 
                    FROM journalentry 
                    WHERE JSON_CONTAINS(ResponsibilityCenterEntry, {json}, '$') 
                    LIMIT 1
                ")
                .AsNoTracking()
                .FirstOrDefaultAsync();


            if (record != null)
            {
                return BadRequest("Cannot delete this record as it is already in use.");
            }

            _context.ResponsibilityCenters.Remove(responsibilityCenter);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ResponsibilityCenterExists(int id)
        {
            return _context.ResponsibilityCenters.Any(e => e.Id == id);
        }
    }
}

