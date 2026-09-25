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
    // [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/[controller]")]
    [ApiController]
    public class NavigationItemsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public NavigationItemsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/NavigationItems
        [HttpGet]
        public async Task<ActionResult> GetNavigationItem()
        {
            var result = await _context.NavigationItems
                .Where(e => e.ParentId == null)
                .Select(e => new {
                    Id = Convert.ToString(e.Id),
                    e.Title,
                    e.Subtitle,
                    e.Type,
                    e.Link,
                    e.Icon,
                    Children = e.Children.Select(c => new
                    {
                        Id = Convert.ToString(c.Id),
                        c.Title,
                        c.Subtitle,
                        c.Type,
                        c.Link,
                        c.Icon
                    })
                }).ToListAsync();

            return Ok(result);
        }

        // GET: api/NavigationItems/5
        [HttpGet("{id}")]
        public async Task<ActionResult<NavigationItem>> GetNavigationItem(int id)
        {
            var navigationItem = await _context.NavigationItems.FindAsync(id);

            if (navigationItem == null)
            {
                return NotFound();
            }

            return navigationItem;
        }

        // PUT: api/NavigationItems/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutNavigationItem(int id, NavigationItem navigationItem)
        {
            if (id != navigationItem.Id)
            {
                return BadRequest();
            }

            _context.Entry(navigationItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!NavigationItemExists(id))
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

        // POST: api/NavigationItems
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<NavigationItem>> PostNavigationItem(NavigationItem navigationItem)
        {
            _context.NavigationItems.Add(navigationItem);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetNavigationItem", new { id = navigationItem.Id }, navigationItem);
        }

        // DELETE: api/NavigationItems/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNavigationItem(int id)
        {
            var navigationItem = await _context.NavigationItems.FindAsync(id);
            if (navigationItem == null)
            {
                return NotFound();
            }

            _context.NavigationItems.Remove(navigationItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool NavigationItemExists(int id)
        {
            return _context.NavigationItems.Any(e => e.Id == id);
        }
    }
}
