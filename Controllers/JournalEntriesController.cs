using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
using negosuite_api.Services;
using Newtonsoft.Json;

namespace negosuite_api.Controllers;

[Authorize, TypeFilter(typeof(ConfigUuidFilter)), ApiController]
[Route("api/journal-entries")]
public class JournalEntriesController : ControllerBase
{
    private readonly JournalLookupService service;
    [ActivatorUtilitiesConstructor]
    public JournalEntriesController(JournalLookupService service) => this.service = service;
    public JournalEntriesController(negosuiteContext context) : this(new JournalLookupService(context)) { }
    private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

    [HttpGet("unpaid-invoices")]
    public Task<ActionResult> GetUnpaidInvoices(string criteria, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null,
        [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null, CancellationToken cancellationToken = default) =>
        List("invoice", criteria, pageNumber, pageSize, search, sortBy, sortDirection, cancellationToken);

    [HttpGet("unpaid-bills")]
    public Task<ActionResult> GetUnpaidBills(string criteria, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null,
        [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null, CancellationToken cancellationToken = default) =>
        List("bill", criteria, pageNumber, pageSize, search, sortBy, sortDirection, cancellationToken);

    [HttpGet("unapplied-ar-credits")]
    public Task<ActionResult> GetUnAppliedARCredits(string criteria, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null,
        [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null, CancellationToken cancellationToken = default) =>
        List("credit", criteria, pageNumber, pageSize, search, sortBy, sortDirection, cancellationToken);

    private async Task<ActionResult> List(string kind, string criteria, int? page, int? size, string search, string sort, string direction, CancellationToken ct)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (!CustomerPagination.IsValid(page, size)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
        if (!JournalLookupService.IsValidSort(kind, sort, direction)) return BadRequest("Unsupported sortBy or sortDirection.");
        JournalLookupCriteria filter;
        try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<JournalLookupCriteria>(criteria); }
        catch (JsonException) { return BadRequest("Invalid criteria JSON."); }
        if (filter == null) return BadRequest("criteria is required.");
        if (filter.UserConfigId.HasValue && filter.UserConfigId != CompanyId) return Forbid();
        return Ok(await service.ListAsync(kind, CompanyId.Value, filter, page, size, search, sort, direction, ct));
    }

    [HttpGet("unapplied-ar-credits/{id}")]
    public async Task<ActionResult<UnappliedCreditDetailDto>> GetUnAppliedARCredit(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
        return result == null ? NotFound() : result;
    }

    public static void SanitizeEntries(ICollection<JournalEntry> journalEntries)
    {
        foreach (var entry in journalEntries) { entry.Amount = Math.Round(entry.Amount, 2); entry.Balance = Math.Round(entry.Balance, 2); }
    }
}
