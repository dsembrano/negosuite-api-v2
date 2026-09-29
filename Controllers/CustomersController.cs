using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Customers;
using negosuite_api.Models;
using negosuite_api.Services;
using Newtonsoft.Json;

namespace negosuite_api.Controllers;

[Authorize]
[TypeFilter(typeof(ConfigUuidFilter))]
[Route("api/customers")]
[ApiController]
public class CustomersController : ControllerBase
{
    public const bool STATUS_INACTIVE = false;
    public const bool STATUS_ACTIVE = true;
    private readonly CustomerService customers;

    [ActivatorUtilitiesConstructor]
    public CustomersController(CustomerService customers) => this.customers = customers;

    // Retain the context constructor used by the migration comparison harness.
    public CustomersController(negosuiteContext context) => customers = new CustomerService(context);

    private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

    [HttpGet]
    public async Task<ActionResult> GetCustomers(string criteria, [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default, [FromQuery] string search = null,
        [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (!CustomerPagination.IsValid(pageNumber, pageSize))
            return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
        if (!CustomerSorting.IsValid(sortBy, sortDirection))
            return BadRequest("Unsupported customer sortBy or sortDirection. Use a customer column and asc or desc.");
        CustomerListCriteria filter;
        try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<CustomerListCriteria>(criteria); }
        catch (JsonException) { return BadRequest("Invalid customer criteria JSON."); }
        if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
        if (filter.UserConfigId != CompanyId) return Forbid();
        return Ok(await customers.ListAsync(CompanyId.Value, filter.ShowInactive == true, pageNumber, pageSize, cancellationToken, search, sortBy, sortDirection));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDetailDto>> GetCustomer(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        var result = await customers.GetAsync(CompanyId.Value, id, cancellationToken);
        if (result == null) return NotFound();
        return result;
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDetailDto>> PostCustomer(CustomerCreateRequest customer, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (customer == null || customer.Id != 0) return BadRequest("New customer ID must be zero or omitted.");
        if (customer.UserConfigId != CompanyId) return Forbid();
        var result = await customers.SaveAsync(CompanyId.Value, null, customer, cancellationToken);
        if (result.Error != null) return BadRequest(result.Error);
        return result.Customer;
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CustomerDetailDto>> PutCustomer(int id, CustomerUpdateRequest customer, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (customer == null || customer.Id != id) return BadRequest();
        if (customer.UserConfigId != CompanyId) return Forbid();
        var result = await customers.SaveAsync(CompanyId.Value, id, customer, cancellationToken);
        if (result.Missing) return NotFound();
        if (result.Error != null) return BadRequest(result.Error);
        return result.Customer;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        try
        {
            if (!await customers.DeleteAsync(CompanyId.Value, id, cancellationToken)) return NotFound();
        }
        catch (DbUpdateException)
        {
            return BadRequest("Unable to delete customer. It is probably used in another transaction.");
        }
        return NoContent();
    }
}
