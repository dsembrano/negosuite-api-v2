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
    [Route("api/accounts")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public AccountsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/Accounts
        [HttpGet]
        public async Task<ActionResult> GetAccounts(string criteria)
        {

            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.Accounts
                .Where(a => a.UserConfigId == selectCriteria.UserConfigId)
                .Where(c => c.CategoryId == ((selectCriteria != null && selectCriteria.CategoryId != null && selectCriteria.CategoryId != 0) ? selectCriteria.CategoryId : c.CategoryId))
                .Select(a => new
                {
                    a.Id,
                    Code = a.Code ?? "",
                    a.Name,
                    a.CategoryId,
                    CategoryName = a.Category.Name,
                    a.ParentAccountId,
                    ParentAccountCode = a.ParentAccount.Code,
                    ParentAccountName = a.ParentAccount.Name,
                    a.RequireCustomer,
                    a.RequireSupplier,
                    a.Category.Type,
                    SortCode = ""
                })
                .OrderBy(a => a.Code)
                .ToListAsync();

            return Ok(result);
        }

        [Route("header")]
        [HttpGet]
        public async Task<ActionResult> GetHeaderAccounts()
        {
            var result = await _context.Accounts.OrderBy(a => a.Code)
                .Select(a => new
                {
                    a.Id,
                    a.Code,
                    a.Name,
                    a.CategoryId,
                    CategoryName = a.Category.Name,
                    a.ParentAccountId,
                    ParentAccountCode = a.ParentAccount.Code,
                    a.Category,
                }).ToListAsync();

            return Ok(result);
        }

        [Route("link")]
        [HttpGet]
        public async Task<ActionResult> GetLinkAccounts()
        {
            var result = await _context.Accounts.OrderBy(a => a.Code)
                .Select(a => new
                {
                    a.Id,
                    a.Code,
                    a.Name,
                    a.CategoryId,
                    CategoryName = a.Category.Name,
                    a.ParentAccountId,
                    ParentAccountCode = a.ParentAccount.Code,
                    a.Category,
                    a.ParentAccount
                }).ToListAsync();

            return Ok(result);
        }

        // GET: api/Accounts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Account>> GetAccount(int id)
        {
            var account = await _context.Accounts
                .Include(a => a.Category)
                .Include(a => a.ParentAccount)
                .Where(a => a.Id == id).FirstOrDefaultAsync();

            if (account == null)
            {
                return NotFound();
            }

            return account;
        }

        // PUT: api/Accounts/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccount(int id, Account account)
        {
            if (id != account.Id)
            {
                return BadRequest();
            }

            _context.Entry(account).State = EntityState.Modified;

            try
            {
                account.LastUpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AccountExists(id))
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

        // POST: api/Accounts
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Account>> PostAccount(Account account)
        {
            account.CreatedDate = DateTime.Now;

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAccount", new { id = account.Id }, account);
        }

        // DELETE: api/Accounts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AccountExists(int id)
        {
            return _context.Accounts.Any(e => e.Id == id);
        }

    }

}
