# Customer API refactoring and optional pagination

The Customers endpoints now use explicit request/response DTOs and a customer service. Existing routes, JSON field names, nested contacts/addresses, successful create/update responses (HTTP 200), and guarded deletion are preserved. No database schema or frontend changes were applied.

## List contract

Existing clients continue to request `GET /api/customers?criteria=...`, where `criteria` is URL-encoded JSON containing `userConfigId` and optional `showInactive`. Without paging parameters the response remains the complete array. Active-only filtering remains the default.

Paging is opt-in through **both** `pageNumber` and `pageSize`:

```http
GET /api/customers?criteria=%7B%22userConfigId%22%3A16%7D&pageNumber=1&pageSize=50
Authorization: Bearer <token>
configUuid: <company UUID>
```

The response is an envelope:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 0,
  "totalPages": 0
}
```

`items` has exactly the same item contract as the legacy list. Pages start at 1; page size is 1–200. Both parameters must be supplied together. Invalid values, invalid JSON, missing `criteria.userConfigId`, and offsets beyond the supported integer range return HTTP 400. Pages beyond the end return empty items with the actual totals. Ordering is customer name then ID; child collections use ID order. Count and rows are separate reads, so concurrent writes can change totals between queries.

Existing clients need no changes. Clients opting into paging must consume `items` and metadata rather than treating the response as an array. Unknown legacy criteria fields remain ignored.

## Writes and company scope

- The validated `configUuid` company ID is shared by the existing action filter with Customers. List and write company IDs must match it (403 for a mismatch); detail, update and delete lookups are restricted to that company (404 for inaccessible IDs). Existing authentication/session behavior remains in place. This is not a redesign of token/company membership or module permissions.
- Separate create/update DTOs accept existing scalar and nested payloads. Extra lookup/audit fields sent by older clients are ignored. Timestamps are server-managed; persisted audit user IDs are preserved and cannot be overwritten through the customer payload. No new user-identity attribution mechanism was introduced.
- Updates load the tracked customer aggregate once. Existing child IDs must belong to that customer, supplied parent IDs must agree, and duplicate child IDs are rejected before mutations. New children are attached through the parent relationship. Explicit `deleted: true` removes persisted children; a new child marked deleted is ignored. Missing/null child collections preserve existing children, and omission of an individual child does not delete it.
- Name/TIN/postal-code lengths follow existing EF constraints. Tax rates must belong to the selected company; payment-term and city references must exist. No new name-uniqueness or financial business rules were introduced.
- Each save/delete uses a single atomic EF `SaveChangesAsync`. Existing database concurrency limitations remain; no row-version column was added. A referenced customer still returns the existing guarded-deletion error and its child deletions roll back.

## Query changes

List headers are projected directly to DTOs without tracking. The service reads addresses and contacts separately to avoid multiplying rows through collection joins. Full lists use three queries with company-filtered subqueries for children. A nonempty page uses four queries: count, page headers, page addresses and page contacts. Empty results skip child reads. Page IDs are bounded at 200; the legacy path does not construct an unbounded ID parameter list.

Detail reads use explicit response mappings with unchanged nested lookup/audit fields. Legacy null navigation fields that were not loaded by the customer endpoints remain null. Address formatting preserves the existing output for valid lookups and safely handles missing navigation values. Cancellation is propagated to EF operations.

## Verification

Run the database-independent suite:

```powershell
dotnet test tests/Negosuite.Api.CompatibilityTests/Negosuite.Api.CompatibilityTests.csproj -c Release
```

Run the native MySQL suite against the existing isolated fixture (port 33316):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3MySql.ps1
```

All **25 tests passed**, with no skips, in the isolated run. Coverage includes pagination boundaries, malformed criteria, stable ordering, inactive filtering, legacy detail payload round trips, nested additions/updates/deletions, audit-field protection, company/child ownership rejection, reference validation, and rollback of child deletion when an FK prevents customer deletion. Write fixtures use newly generated schemas on the dedicated test instance and are removed afterward. Development data was only read.

The live pre-refactor and final responses matched byte-for-byte for **5,173 list rows** and **20 sampled customer details**. The migration comparison harness now supplies the validated company context normally set by the HTTP filter; its historical .NET 6 code path remains compilable.

Local benchmark, one warm-up followed by three measured runs per mode, including serialization but excluding HTTP transport:

| Mode | Median time | Queries | JSON bytes | Tracked entities |
| --- | ---: | ---: | ---: | ---: |
| Previous full list | 285.9 ms | 1 | 5,308,922 | 5,292 |
| Refactored full list | 139.6 ms | 3 | 5,308,922 | 0 |
| First page, 50 customers | 7.6 ms | 4 | 52,261 | 0 |

These local measurements are not production latency guarantees. The unlimited legacy payload remains large by design. Existing indexes include a company foreign-key index, but EXPLAIN for the sampled company/name pagination query selected a table scan and filesort (estimated 5,283 rows). No index was added: consider `(UserConfigId, Status, Name, Id)` and a separate inactive-inclusive workload review if larger production measurements justify the schema change.

See [sanitized evidence](customers-refactoring-evidence.json). Browser acceptance and production query latency have not been measured in this change.


## Contact/address search extension (2026-09-29)

The existing optional `search` parameter now also matches any contact name, phone number or email; address lines 1/2; city/municipality; province; and address/city postal codes. `Services/CustomerSearch.cs` applies this predicate to the company/active-scoped query before count, sort and paging. Related matches use SQL EXISTS so multiple matches do not duplicate customers. Paged and unpaged reads share this behavior. Existing substring/collation semantics remain; phone punctuation is not normalized. `Deleted` is an unmapped write instruction; saved child deletions are physical removals.

All 56 focused Customer tests passed with no skips against the isolated MySQL fixture at 127.0.0.1:33316. New coverage includes related-field HTTP queries, duplicate contact matches, page 2 and total counts, active/tenant boundaries, unpaged parity, and MySQL provider translation. Temporary schemas were created and removed by the existing integration test; application data and configuration were untouched. Restart/deploy this API build for the web client's expanded search. No new route or schema migration is required.
