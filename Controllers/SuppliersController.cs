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
    [Route("api/suppliers")]
    [ApiController]
    public class SuppliersController : ControllerBase
    {
        public static bool STATUS_INACTIVE = false;
        public static bool STATUS_ACTIVE = true;
        private readonly negosuiteContext _context;

        public SuppliersController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/Suppliers
        [HttpGet]
        public async Task<ActionResult> GetSuppliers(string criteria)
        {

            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await _context.Suppliers.OrderBy(c => c.Name)
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .Where(c => selectCriteria != null && selectCriteria.ShowInactive == true ? true : c.Status == STATUS_ACTIVE)
                .Select(c => new
                {
                    c.Id,
                    c.Code,
                    c.Name,
                    c.Tin,
                    c.Status,
                    c.TaxRateId,
                    TaxRateName = c.TaxRate.Name,
                    c.PaymentTermId,
                    PaymentTermName = c.PaymentTerm.Name,
                }).ToListAsync();

            return Ok(result);
        }

        // GET: api/Suppliers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Supplier>> GetSupplier(int id)
        {
            var supplier = await _context.Suppliers.Where(s => s.Id == id)
                .Include(c => c.TaxRate)
                .Include(c => c.PaymentTerm)
                .Include(c => c.SupplierContacts)
                .Include(c => c.SupplierAddresses).ThenInclude(a => a.CityMunicipality).ThenInclude(s => s.StateProvince)
                .SingleOrDefaultAsync();

            if (supplier == null)
            {
                return NotFound();
            }

            return supplier;
        }

        // PUT: api/Suppliers/5
        [HttpPut("{id}")]
        public async Task<ActionResult<Supplier>> PutSupplier(int id, Supplier supplier)
        {
            if (id != supplier.Id)
            {
                return BadRequest();
            }

            // supplier contacts
            foreach (var s in supplier.SupplierContacts.ToList())
            {
                if (s.Id == 0)
                {
                    s.CreatedDate = DateTime.Now;
                    _context.SupplierContacts.Add(s);
                }
                else
                {
                    if (s.Deleted == true)
                    {
                        var contact = await _context.SupplierContacts.FindAsync(s.Id);
                        _context.SupplierContacts.Remove(contact);
                    }
                    else
                    {
                        s.LastUpdatedDate = DateTime.Now;
                        _context.Entry(s).State = EntityState.Modified;
                    }
                }
            }

            // supplier Addresses
            foreach (var s in supplier.SupplierAddresses.ToList())
            {
                if (s.Id == 0)
                {
                    s.CreatedDate = DateTime.Now;
                    _context.SupplierAddresses.Add(s);
                }
                else
                {
                    if (s.Deleted == true)
                    {
                        var address = await _context.SupplierAddresses.FindAsync(s.Id);
                        _context.SupplierAddresses.Remove(address);
                    }
                    else
                    {
                        s.LastUpdatedDate = DateTime.Now;
                        _context.Entry(s).State = EntityState.Modified;
                    }
                }
            }

            supplier.LastUpdatedDate = DateTime.Now;
            _context.Entry(supplier).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SupplierExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            //return NoContent();
            return await GetSupplier(supplier.Id);
        }

        // POST: api/Suppliers
        [HttpPost]
        public async Task<ActionResult<Supplier>> PostSupplier(Supplier supplier)
        {
            supplier.CreatedDate = DateTime.Now;

            foreach (var s in supplier.SupplierContacts)
            {
                s.CreatedDate = DateTime.Now;
            }

            foreach (var s in supplier.SupplierAddresses)
            {
                s.CreatedDate = DateTime.Now;
            }

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSupplier", new { id = supplier.Id }, supplier);
        }

        // DELETE: api/Suppliers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            var supplier = await _context.Suppliers.Where(s => s.Id == id)
               .Include(s => s.SupplierContacts)
               .Include(s => s.SupplierAddresses).SingleOrDefaultAsync();

            if (supplier == null)
            {
                return NotFound();
            }

            foreach (var contact in supplier.SupplierContacts)
            {
                _context.SupplierContacts.Remove(contact);
            }

            foreach (var address in supplier.SupplierAddresses)
            {
                _context.SupplierAddresses.Remove(address);
            }

            try
            {
                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return BadRequest("Unable to delete supplier. It is probably used in another transaction.");
            }

            return NoContent();
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.Id == id);
        }
    }
}
