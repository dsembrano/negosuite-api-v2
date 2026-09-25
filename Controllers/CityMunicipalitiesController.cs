using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/city-municipalities")]
    [ApiController]
    public class CityMunicipalitiesController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public CityMunicipalitiesController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/CityMunicipalities
        [HttpGet]
        public async Task<ActionResult> GetCityMunicipalities()
        {
            var result = await _context.CityMunicipalities
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.StateProvinceId,
                    StateProvinceName = c.StateProvince.Name,
                    SelectOptionName = $"{c.Name}, {c.StateProvince.Name}",
                    c.PostalCode
                }).ToListAsync();

            return Ok(result);
        }

        // GET: api/CityMunicipalities/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CityMunicipality>> GetCityMunicipality(int id)
        {
            var cityMunicipality = await _context.CityMunicipalities
                .Where(c => c.Id == id)
                .Include(c => c.StateProvince)
                .SingleOrDefaultAsync();

            if (cityMunicipality == null)
            {
                return NotFound();
            }

            return cityMunicipality;
        }

        // PUT: api/CityMunicipalities/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCityMunicipality(int id, CityMunicipality cityMunicipality)
        {
            cityMunicipality.LastUpdatedDate = DateTime.Now;
            if (id != cityMunicipality.Id)
            {
                return BadRequest();
            }

            _context.Entry(cityMunicipality).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CityMunicipalityExists(id))
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

        // POST: api/CityMunicipalities
        [HttpPost]
        public async Task<ActionResult<CityMunicipality>> PostCityMunicipality(CityMunicipality cityMunicipality)
        {
            cityMunicipality.CreatedDate = DateTime.Now;
            _context.CityMunicipalities.Add(cityMunicipality);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetCityMunicipality", new { id = cityMunicipality.Id }, cityMunicipality);
        }

        // DELETE: api/CityMunicipalities/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCityMunicipality(int id)
        {
            var cityMunicipality = await _context.CityMunicipalities.FindAsync(id);
            if (cityMunicipality == null)
            {
                return NotFound();
            }

            _context.CityMunicipalities.Remove(cityMunicipality);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool CityMunicipalityExists(int id)
        {
            return _context.CityMunicipalities.Any(e => e.Id == id);
        }
    }
}
