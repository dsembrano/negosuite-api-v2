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
    [Route("api/discount-types")]
    [ApiController]
    public class DiscountTypesController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public DiscountTypesController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/DiscountTypes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DiscountType>>> GetDiscountType(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            return await _context.DiscountTypes.Where(t => t.UserConfigId == selectCriteria.UserConfigId)
                .Include(e => e.DiscountAccount)
                .Include(t => t.TaxRate).ToListAsync();
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<DiscountType>> GetDiscountType(int id)
        {
            var DiscountType = await _context.DiscountTypes
                .FindAsync(id);

            if (DiscountType == null)
            {
                return NotFound();
            }

            return DiscountType;
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutDiscountType(int id, DiscountType DiscountType)
        {
            if (id != DiscountType.Id)
            {
                return BadRequest();
            }

            _context.Entry(DiscountType).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DiscountTypeExists(id))
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
        public async Task<ActionResult<DiscountType>> PostDiscountType(DiscountType DiscountType)
        {
            _context.DiscountTypes.Add(DiscountType);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetDiscountType", new { id = DiscountType.Id }, DiscountType);
        }


        [Route("many")]
        [HttpPost]
        public async Task<ActionResult> PostManyDiscountType(List<DiscountType> DiscountTypes)
        {

            foreach (var e in DiscountTypes.ToList())
            {
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.DiscountTypes.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.DiscountTypes.FindAsync(e.Id);
                        _context.DiscountTypes.Remove(entry);
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
            catch (DbUpdateException ex)
            {
                // A foreign key constraint violation occurred
                return Conflict("Cannot delete Tax Rate because it is currently in use.");
            }

            var list = await _context.DiscountTypes.Where(e => e.UserConfigId == DiscountTypes[0].UserConfigId)
                .Include(e => e.DiscountAccount)
                .Include(t => t.TaxRate).ToListAsync();

            return Ok(list);

        }


        // DELETE: api/DiscountTypes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDiscountType(int id)
        {
            var DiscountType = await _context.DiscountTypes.FindAsync(id);
            if (DiscountType == null)
            {
                return NotFound();
            }

            _context.DiscountTypes.Remove(DiscountType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool DiscountTypeExists(int id)
        {
            return _context.DiscountTypes.Any(e => e.Id == id);
        }
    }
}
