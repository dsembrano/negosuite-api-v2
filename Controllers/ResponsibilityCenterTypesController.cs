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
    // [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/responsibility-center-types")]
    [ApiController]
    public class ResponsibilityCenterTypesController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public ResponsibilityCenterTypesController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/ResponsibilityCenterTypes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ResponsibilityCenterType>>> GetResponsibilityCenterTypes(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            return await _context.ResponsibilityCenterTypes.Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .ToListAsync();
        }

        // GET: api/ResponsibilityCenterTypes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ResponsibilityCenterType>> GetResponsibilityCenterType(int id)
        {
            var responsibilityCenterType = await _context.ResponsibilityCenterTypes.FindAsync(id);

            if (responsibilityCenterType == null)
            {
                return NotFound();
            }

            return responsibilityCenterType;
        }

        // PUT: api/ResponsibilityCenterTypes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutResponsibilityCenterType(int id, ResponsibilityCenterType responsibilityCenterType)
        {
            if (id != responsibilityCenterType.Id)
            {
                return BadRequest();
            }

            _context.Entry(responsibilityCenterType).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ResponsibilityCenterTypeExists(id))
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

        // POST: api/ResponsibilityCenterTypes
        [HttpPost]
        public async Task<ActionResult<ResponsibilityCenterType>> PostResponsibilityCenterType(ResponsibilityCenterType responsibilityCenterType)
        {
            _context.ResponsibilityCenterTypes.Add(responsibilityCenterType);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetResponsibilityCenterType", new { id = responsibilityCenterType.Id }, responsibilityCenterType);
        }


        // POST: api/ResponsibilityCenterTypes
        [Route("many")]
        [HttpPost]
        public async Task<ActionResult> PostManyResponsibilityCenterType(List<ResponsibilityCenterType> responsibilityCenterTypes)
        {

            foreach (var e in responsibilityCenterTypes.ToList())
            {
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.ResponsibilityCenterTypes.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.ResponsibilityCenterTypes.FindAsync(e.Id);

                        if (await _context.ResponsibilityCenters.Where(c => c.ResponsibilityCenterTypeId == entry.Id).AnyAsync())
                        {
                            return BadRequest($"Unable to delete the Responsibility Center '{entry.Name}' because it is reference by another record.");
                        }

                        _context.ResponsibilityCenterTypes.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            };

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }

            var list = await _context.ResponsibilityCenterTypes.Where(e => e.UserConfigId == responsibilityCenterTypes[0].UserConfigId)
               .ToListAsync();

            return Ok(list);

        }

        // DELETE: api/ResponsibilityCenterTypes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResponsibilityCenterType(int id)
        {
            var responsibilityCenterType = await _context.ResponsibilityCenterTypes.FindAsync(id);
            if (responsibilityCenterType == null)
            {
                return NotFound();
            }

            _context.ResponsibilityCenterTypes.Remove(responsibilityCenterType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ResponsibilityCenterTypeExists(int id)
        {
            return _context.ResponsibilityCenterTypes.Any(e => e.Id == id);
        }
    }
}
