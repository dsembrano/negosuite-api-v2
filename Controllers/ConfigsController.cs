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
    [Route("api/configs")]
    [ApiController]
    public class ConfigsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public ConfigsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/Configs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Config>>> GetConfigs()
        {
            return await _context.Configs.ToListAsync();
        }


        [Route("template")]
        [HttpGet]
        public async Task<IActionResult> GetConfigTeamplates()
        {
            var list = await _context.Configs.Where(c => c.IsTemplate == true)
                .Select(c => new
                {
                    Id = c.Id,
                    Name = c.CompanyName,
                    About = c.CompanyAbout,
                    c.DiscountAccountId,
                    c.PurchaseDiscountAccountId,
                }).ToListAsync();

            return Ok(list);
        }

        // GET: api/Configs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Config>> GetConfig(int id)
        {
            var config = await _context.Configs
                .Include(e => e.Industry)
                .Include(e => e.Country)
                .Include(e => e.ARTradeAccount)
                .Include(e => e.APTradeAccount)
                .Include(e => e.DiscountAccount)
                .Include(e => e.PurchaseDiscountAccount)
                .Where(e => e.Id == id).SingleOrDefaultAsync();

            if (config == null)
            {
                return NotFound();
            }

            return config;
        }



        [HttpPut("{id}")]
        public async Task<IActionResult> PutConfig(int id, Config config)
        {
            if (id != config.Id)
            {
                return BadRequest();
            }

            _context.Entry(config).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ConfigExists(id))
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
        public async Task<ActionResult<Config>> PostConfig(Config config)
        {
            _context.Configs.Add(config);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetConfig", new { id = config.Id }, config);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteConfig(int id)
        {
            var config = await _context.Configs.FindAsync(id);
            if (config == null)
            {
                return NotFound();
            }

            _context.Configs.Remove(config);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        [HttpPut]
        [Route("payment-adjustment-type/{id}")]
        public async Task<IActionResult> PutConfigPaymentAdjusmentTypes(int id, [FromBody] string paymentAdjustmentTypes)
        {            
            var config = await _context.Configs.SingleOrDefaultAsync(e => e.Id == id);

            if (config == null)
            {
                return BadRequest();
            }

            config.PaymentAdjustmentTypes = paymentAdjustmentTypes;
            _context.Entry(config).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ConfigExists(id))
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

        private bool ConfigExists(int id)
        {
            return _context.Configs.Any(e => e.Id == id);
        }
    }


    public class RCRequiredBy
    {
        public int RCNumber { get; set; }
        public string RequiredBy { get; set; }
        public List<int> RequiredByIds { get; set; }
    }

}
