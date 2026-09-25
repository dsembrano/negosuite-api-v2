using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using negosuite_api.Models;
using Newtonsoft.Json;

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/customers")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        public static bool STATUS_INACTIVE = false;
        public static bool STATUS_ACTIVE = true;
        private readonly negosuiteContext _context;

        public CustomersController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/Customers
        [HttpGet]
        public async Task<ActionResult> GetCustomers(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            // Check if header contains configUuid which is uique for each company.
            // Then get the config.id using uuid and check if it maches the userConfigId from the payload
            /*
            if (Request.Headers.TryGetValue("configUuid", out StringValues configUuid))
            {
                var config = await _context.Configs.Where(c => c.Uuid == configUuid).FirstOrDefaultAsync();
            } else
            {
                //return Unauthorized();
            }*/


            var result = await _context.Customers
                .Where(c => c.UserConfigId == selectCriteria.UserConfigId)
                .Where(c => selectCriteria != null && selectCriteria.ShowInactive == true ? true : c.Status == STATUS_ACTIVE)
                .Include(c => c.CustomerAddresses).ThenInclude(a => a.CityMunicipality).ThenInclude(s => s.StateProvince)
                .OrderBy(c => c.Name)
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
                    PaymentTermDays = c.PaymentTerm.Days,
                    PaymentTermCode = c.PaymentTerm.Code,
                    c.CreditLimit,
                    StringCustomerAddresses = c.CustomerAddresses.Select(a => StringAddress(a)),
                    c.CustomerAddresses,
                    c.CustomerContacts
                }).ToListAsync();

            return Ok(result);
        }

        // GET: api/Customers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(int id)
        {
            var customer = await _context.Customers.Where(c => c.Id == id)
                .Include(c => c.TaxRate)
                .Include(c => c.PaymentTerm)
                .Include(c => c.CustomerContacts)
                .Include(c => c.CustomerAddresses).ThenInclude(a => a.CityMunicipality).ThenInclude(s => s.StateProvince)
                .SingleOrDefaultAsync();

            if (customer == null)
            {
                return NotFound();
            }

            return customer;
        }

        // PUT: api/Customers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<ActionResult<Customer>> PutCustomer(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                return BadRequest();
            }

            // Customer contacts
            foreach (var c in customer.CustomerContacts.ToList())
            {
                if (c.Id == 0)
                {
                    c.CreatedDate = DateTime.Now;
                    _context.CustomerContacts.Add(c);
                }
                else
                {
                    if (c.Deleted == true)
                    {
                        var contact = await _context.CustomerContacts.FindAsync(c.Id);
                        _context.CustomerContacts.Remove(contact);
                    }
                    else
                    {
                        c.LastUpdatedDate = DateTime.Now;
                        _context.Entry(c).State = EntityState.Modified;
                    }
                }
            }

            // Customer Addresses
            foreach (var c in customer.CustomerAddresses.ToList())
            {
                if (c.Id == 0)
                {
                    c.CreatedDate = DateTime.Now;
                    _context.CustomerAddresses.Add(c);
                }
                else
                {
                    if (c.Deleted == true)
                    {
                        var address = await _context.CustomerAddresses.FindAsync(c.Id);
                        _context.CustomerAddresses.Remove(address);
                    }
                    else
                    {
                        c.LastUpdatedDate = DateTime.Now;
                        _context.Entry(c).State = EntityState.Modified;
                    }
                }
            }

            customer.LastUpdatedDate = DateTime.Now;
            _context.Entry(customer).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            // return NoContent();
            return await GetCustomer(customer.Id);

        }


        // POST: api/Customers
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Customer>> PostCustomer(Customer customer)
        {
            customer.CreatedDate = DateTime.Now;

            foreach (var c in customer.CustomerContacts)
            {
                c.CreatedDate = DateTime.Now;
            }

            foreach (var c in customer.CustomerAddresses)
            {
                c.CreatedDate = DateTime.Now;
            }

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            //return CreatedAtAction("GetCustomer", new { id = customer.Id }, customer);
            return await GetCustomer(customer.Id);

        }

        // DELETE: api/Customers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.Where(c => c.Id == id)
                .Include(c => c.CustomerContacts)
                .Include(c => c.CustomerAddresses).SingleOrDefaultAsync();

            if (customer == null)
            {
                return NotFound();
            }

            foreach (var contact in customer.CustomerContacts)
            {
                _context.CustomerContacts.Remove(contact);
            }

            foreach (var address in customer.CustomerAddresses)
            {
                _context.CustomerAddresses.Remove(address);
            }

            try
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return BadRequest("Unable to delete customer. It is probably used in another transaction.");
            }

            return NoContent();
        }

        private static string StringAddress(CustomerAddress address) {
            return $"{address.AddressLine1} " +
                $"{(!String.IsNullOrEmpty(address.AddressLine2) ? address.AddressLine2 : "")}" +
                $"{address.CityMunicipality.Name}, {address.CityMunicipality.StateProvince.Name} {(!String.IsNullOrEmpty(address.CityMunicipality.PostalCode) ? address.CityMunicipality.PostalCode : "")}";
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }

    }
}
