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
    [Route("api/tax-rates")]
    [ApiController]
    public class TaxRatesController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public TaxRatesController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/TaxRates
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaxRate>>> GetTaxRate(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            return await _context.TaxRates.Where(t => t.UserConfigId == selectCriteria.UserConfigId)
                .Include(t => t.TaxAccount).Include(t => t.SalesAccount)
                .ToListAsync();
        }

        // GET: api/TaxRates/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TaxRate>> GetTaxRate(int id)
        {
            var taxRate = await _context.TaxRates.FindAsync(id);

            if (taxRate == null)
            {
                return NotFound();
            }

            return taxRate;
        }

        // PUT: api/TaxRates/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTaxRate(int id, TaxRate taxRate)
        {
            if (id != taxRate.Id)
            {
                return BadRequest();
            }

            _context.Entry(taxRate).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TaxRateExists(id))
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

        // POST: api/TaxRates
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TaxRate>> PostTaxRate(TaxRate taxRate)
        {
            _context.TaxRates.Add(taxRate);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTaxRate", new { id = taxRate.Id }, taxRate);
        }


        [Route("many")]
        [HttpPost]
        public async Task<ActionResult> PostManyTaxRate(List<TaxRate> taxRates)
        {

            foreach (var e in taxRates.ToList())
            {
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.TaxRates.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.TaxRates.FindAsync(e.Id);
                        _context.TaxRates.Remove(entry);
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

            var list = await _context.TaxRates.Where(e => e.UserConfigId == taxRates[0].UserConfigId)
               .Include(t => t.TaxAccount).Include(t => t.SalesAccount)
               .ToListAsync();

            return Ok(list);

        }


        // DELETE: api/TaxRates/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTaxRate(int id)
        {
            var taxRate = await _context.TaxRates.FindAsync(id);
            if (taxRate == null)
            {
                return NotFound();
            }

            _context.TaxRates.Remove(taxRate);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TaxRateExists(int id)
        {
            return _context.TaxRates.Any(e => e.Id == id);
        }
    }
}
