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
    [Route("api/account-categories")]
    [ApiController]
    public class AccountCategoriesController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public AccountCategoriesController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/AccountCategories
        [HttpGet]
        public async Task<ActionResult> GetAccountCategories(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await _context.AccountCategories.OrderBy(a => a.OrderNo)
                .Where(a => a.UserConfigId == selectCriteria.UserConfigId)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.Type,
                    a.AccountCodePrefix,
                    a.OrderNo,
                    AccountCount = _context.Accounts.Where(c => c.CategoryId == a.Id).Count(),
                }).OrderBy(e => e.OrderNo).ToListAsync();

            return Ok(result);
        }

        // GET: api/AccountCategories/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AccountCategory>> GetAccountCategory(int id)
        {
            var accountCategory = await _context.AccountCategories.FindAsync(id);

            if (accountCategory == null)
            {
                return NotFound();
            }

            return accountCategory;

        }

        // PUT: api/AccountCategories/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccountCategory(int id, AccountCategory accountCategory)
        {
            if (id != accountCategory.Id)
            {
                return BadRequest();
            }

            accountCategory.LastUpdatedDate = DateTime.Now;
            _context.Entry(accountCategory).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AccountCategoryExists(id))
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

        // POST: api/AccountCategories
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<AccountCategory>> PostAccountCategory(AccountCategory accountCategory)
        {
            accountCategory.CreatedDate = DateTime.Now;

            _context.AccountCategories.Add(accountCategory);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAccountCategory", new { id = accountCategory.Id }, accountCategory);
        }

        // DELETE: api/AccountCategories/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccountCategory(int id)
        {
            var accountCategory = await _context.AccountCategories.FindAsync(id);
            if (accountCategory == null)
            {
                return NotFound();
            }

            _context.AccountCategories.Remove(accountCategory);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AccountCategoryExists(int id)
        {
            return _context.AccountCategories.Any(e => e.Id == id);
        }
    }
}
