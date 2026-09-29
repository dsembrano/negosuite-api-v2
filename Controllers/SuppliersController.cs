using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Customers;
using negosuite_api.Models;
using negosuite_api.Services;
using Newtonsoft.Json;

namespace negosuite_api.Controllers;

[Authorize]
[TypeFilter(typeof(ConfigUuidFilter))]
[Route("api/suppliers")]
[ApiController]
public class SuppliersController : ControllerBase
{
    public const bool STATUS_INACTIVE = false;
    public const bool STATUS_ACTIVE = true;
    private readonly SupplierService suppliers;

    [ActivatorUtilitiesConstructor]
    public SuppliersController(SupplierService suppliers) => this.suppliers = suppliers;

    // Retain the context constructor used by the migration comparison harness.
    public SuppliersController(negosuiteContext context) => suppliers = new SupplierService(context);

    private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

    [HttpGet]
    public async Task<ActionResult> GetSuppliers(string criteria, [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default, [FromQuery] string search = null,
        [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (!CustomerPagination.IsValid(pageNumber, pageSize))
            return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
        if (!SupplierSorting.IsValid(sortBy, sortDirection))
            return BadRequest("Unsupported supplier sortBy or sortDirection. Use a supplier column and asc or desc.");
        SupplierListCriteria filter;
        try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<SupplierListCriteria>(criteria); }
        catch (JsonException) { return BadRequest("Invalid supplier criteria JSON."); }
        if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
        if (filter.UserConfigId != CompanyId) return Forbid();
        return Ok(await suppliers.ListAsync(CompanyId.Value, filter.ShowInactive == true, pageNumber, pageSize, cancellationToken, search, sortBy, sortDirection));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupplierDetailDto>> GetSupplier(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        var result = await suppliers.GetAsync(CompanyId.Value, id, cancellationToken);
        if (result == null) return NotFound();
        return result;
    }

    [HttpPost]
    public async Task<ActionResult<SupplierDetailDto>> PostSupplier(SupplierCreateRequest supplier, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (supplier == null || supplier.Id != 0) return BadRequest("New supplier ID must be zero or omitted.");
        if (supplier.UserConfigId != CompanyId) return Forbid();
        var result = await suppliers.SaveAsync(CompanyId.Value, null, supplier, cancellationToken);
        if (result.Error != null) return BadRequest(result.Error);
        return CreatedAtAction(nameof(GetSupplier), new { id = result.Supplier.Id }, result.Supplier);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SupplierDetailDto>> PutSupplier(int id, SupplierUpdateRequest supplier, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (supplier == null || supplier.Id != id) return BadRequest();
        if (supplier.UserConfigId != CompanyId) return Forbid();
        var result = await suppliers.SaveAsync(CompanyId.Value, id, supplier, cancellationToken);
        if (result.Missing) return NotFound();
        if (result.Error != null) return BadRequest(result.Error);
        return result.Supplier;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSupplier(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        try
        {
            if (!await suppliers.DeleteAsync(CompanyId.Value, id, cancellationToken)) return NotFound();
        }
        catch (DbUpdateException)
        {
            return BadRequest("Unable to delete supplier. It is probably used in another transaction.");
        }
        return NoContent();
    }
}
