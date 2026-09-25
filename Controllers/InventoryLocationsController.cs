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
    [Route("api/inventory-locations")]
    [ApiController]
    public class InventoryLocationsController : ControllerBase
    {
        public static bool STATUS_INACTIVE = false;
        public static bool STATUS_ACTIVE = true;
        private readonly negosuiteContext _context;

        public InventoryLocationsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/InventoryLocations
        [HttpGet]
        public async Task<ActionResult> GetInventoryLocations(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await _context.InventoryLocations
                .Where(c => c.UserConfigId == selectCriteria.UserConfigId)
                .Where(c => selectCriteria != null && selectCriteria.ShowInactive == true ? true : c.Status == STATUS_ACTIVE)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Code,
                    c.Name,
                    c.Status,
                }).ToListAsync();

            return Ok(result);
        }

        // GET: api/InventoryLocations/5
        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryLocation>> GetInventoryLocation(int id)
        {
            var inventoryLocation = await _context.InventoryLocations.FindAsync(id);

            if (inventoryLocation == null)
            {
                return NotFound();
            }

            return inventoryLocation;
        }

        // PUT: api/InventoryLocations/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInventoryLocation(int id, InventoryLocation inventoryLocation)
        {
            if (id != inventoryLocation.Id)
            {
                return BadRequest();
            }

            _context.Entry(inventoryLocation).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryLocationExists(id))
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

        // POST: api/InventoryLocations
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<InventoryLocation>> PostInventoryLocation(InventoryLocation inventoryLocation)
        {
            _context.InventoryLocations.Add(inventoryLocation);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetInventoryLocation", new { id = inventoryLocation.Id }, inventoryLocation);
        }

        // DELETE: api/InventoryLocations/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryLocation(int id)
        {
            var inventoryLocation = await _context.InventoryLocations.FindAsync(id);
            if (inventoryLocation == null)
            {
                return NotFound();
            }

            _context.InventoryLocations.Remove(inventoryLocation);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool InventoryLocationExists(int id)
        {
            return _context.InventoryLocations.Any(e => e.Id == id);
        }
    }
}
