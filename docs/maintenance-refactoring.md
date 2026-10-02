# Maintenance API refactoring

Tax Rates, Discount Types, Responsibility Centers, Responsibility Center Types and Inventory Locations now use dedicated write/detail DTOs and services. Existing route names, field names and bulk-save routes are retained. EF models remain persistence models; request bodies cannot overwrite nested account/tax records or server-managed audit fields.

## List contracts

All list routes retain JSON `criteria` with required `userConfigId`. Optional query parameters are `search`, `sortBy`, `sortDirection=asc|desc`, and paired `pageNumber`/`pageSize` (page >= 1, size 1-200). Invalid criteria, unsupported sorts or invalid pagination return 400. Search is a trimmed literal substring; `%` and `_` are not wildcards.

Omit both pagination parameters to receive the legacy array. Supply both for `{ items, pageNumber, pageSize, totalCount, totalPages }`. Filtering, ordering, counting and pagination execute in MySQL. Paged or explicitly sorted lists default to name ascending and use ID as a stable tie-breaker. Count and page are separate queries.

| Route | Legacy default | Sort fields |
| --- | --- | --- |
| `/api/tax-rates` | All company rates; original unordered array | `name`, `rate`, `applyToSalesOrPurchase`, `taxAccountName`, `salesAccountName` |
| `/api/discount-types` | All company discounts; original unordered array | `name`, `rate`, `discountAmount`, `discountIsBeforeTax`, `lockedRate`, `discountAccountName`, `taxRateName` |
| `/api/responsibility-centers` | Active and inactive; name ascending | `name`, `status`, `notes`, `responsibilityCenterTypeId` |
| `/api/responsibility-centers/type` | Matching `criteria.responsibilityCenterTypeId`; active only; name ascending | Same as responsibility centers |
| `/api/responsibility-center-types` | Active and inactive; original unordered array | `name`, `isActive`, `requiredBy` |
| `/api/inventory-locations` | Active only; name ascending | `code`, `name`, `status` |

Responsibility centers/types and inventory locations accept explicit `status=true|false`. The `/type` and inventory-location lists retain `criteria.showInactive=true` to include inactive rows; explicit status takes precedence. Tax rates and discount types have no status field. Searches cover names, relevant codes/notes and related account/tax names where available.

Inventory-location lists still expose only `id`, `code`, `name`, `status`; the database projection also selects only these fields. Detail endpoints retain notes/company/audit fields. Tax-rate and discount lists retain their related objects, now represented by DTOs.

Example (URL-encode criteria when constructing the actual request):

```text
GET /api/inventory-locations?criteria={"userConfigId":16,"showInactive":true}&pageNumber=1&pageSize=25&search=warehouse&sortBy=code&sortDirection=asc
```

## Company scope and writes

- Company-scoped calls require the signed, active user and matching `configUuid`. A foreign company in criteria/body is rejected with 403; foreign/missing record IDs return 404. Related accounts, tax rates, and responsibility-center types must belong to the selected company.
- Tax-rate **list reads** additionally permit an explicitly marked template company, including for an authenticated user with no company yet. This preserves V1 company setup. Template status does not authorize detail access or writes; those require ordinary company membership and `configUuid`. If a header is supplied on the template list call it must match the caller's own company.
- Create/update requests validate required names, schema lengths and decimal ranges. Tax/discount decimals use the existing `decimal(20,4)` mapping. Negative rates remain allowed. Required-by tags must be a JSON array of positive integer IDs; `ACCTCAT` validates company account categories, and `SPECIFIC` validates company accounts. Blank tags become null.
- Creation and update audit values come from the authenticated user and server UTC clock. Updates preserve original creation attribution. Resubmitting legacy detail objects is supported; nested navigation and posted audit fields are ignored.
- `/many` remains available for tax rates, discount types and responsibility-center types. `id=0` creates, an existing ID updates, and `deleted=true` deletes. Deletion rows need only ID/company/deleted fields. The complete request is validated before changes are saved in one transaction. Mixed-company rows, unknown/duplicate IDs, null rows and invalid references are rejected without partial writes. An empty array safely returns the unchanged company list.
- Referenced tax rates return 409 on deletion. Responsibility-center types with assigned centers, centers referenced in persisted transaction/journal JSON, and locations used by transactions cannot be deleted (400). Checks cover modelled references even where the database has no foreign key. Disabling an in-use center/location remains possible.

The EF metadata changes match the checked-in database schema. This refactor introduces no schema migration, new module permissions or page-preference keys.

## Verification

`MaintenanceTests` exercises legacy arrays, every supported sort in both directions, pagination and literal search, status defaults, scoped CRUD, audit protection, four-decimal round trips, related IDs, reference guards, bulk rollback and template onboarding. `SwaggerTests` verifies distinct request/detail schemas for all five modules.

Verified on 2026-10-01: Release build succeeded; the full isolated MySQL suite passed 426 tests with zero failures or skips. The only build warnings are the existing unused constants in PayableReportsController and ReceivableReportsController. Results are in `bin/phase3-verification/phase3-mysql.trx` and `bin/maintenance-full-tests.log` (generated, ignored artifacts).

Run the isolated MySQL suite using:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3MySql.ps1
```

It uses temporary test databases on loopback port 33316. It does not modify the configured development database. Restart the development API to exercise the changes from the V1/V2 clients; browser acceptance is separate from these automated checks.
