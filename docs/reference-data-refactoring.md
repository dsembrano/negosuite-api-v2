# Shared reference-data API refactoring

Payment Modes, Payment Terms, Currencies, Countries, City Municipalities, Industries and Navigation Items now use explicit write/detail DTOs, thin controllers and database-backed services. These tables are shared catalogues: they have no company ownership column. The refactor does not add company predicates or redefine company-admin permissions over global records.

## Access and compatibility

All routes require a signed, active database user. Payment Modes and City Municipalities retain `ConfigUuidFilter` and require a matching company header. The other five use the shared authenticated-user filter, allowing company-less onboarding; a supplied company header must match membership. Existing read/write routes remain available. A platform-wide catalogue administration permission policy is outside this refactor.

List calls without pagination retain arrays and their original fields/defaults. City lists still contain exactly `id`, `name`, `stateProvinceId`, `stateProvinceName`, `selectOptionName`, `postalCode`. Industry lists still contain only `id`, `name`. Country responses retain the unexpanded `stateProvinces` array. City details include their state/province DTO. The navigation route remains `/api/NavigationItems`.

## Optional list parameters

Supply `pageNumber` and `pageSize` together for `{ items, pageNumber, pageSize, totalCount, totalPages }`. Page numbers start at 1; page size is 1-200. Omit both for the complete legacy array. Search uses a trimmed literal substring; `%` and `_` are literal characters. `sortDirection` accepts `asc` or `desc`. Invalid paging or sort parameters return 400. Filtering, SQL projections, ordering, counts and limits execute in MySQL; count and page are separate reads.

| Route | Default list | Allowed sort fields |
| --- | --- | --- |
| `/api/payment-modes` | Active modes only | `id`, `name`, `isActive` |
| `/api/payment-terms` | All terms, including inactive | `id`, `name`, `code`, `days`, `isActive` |
| `/api/currencies` | All currencies | `id`, `name`, `code`, `altCode`, `exchangeRate`, `isBase` |
| `/api/countries` | All countries | `id`, `name`, `code` |
| `/api/city-municipalities` | All cities/municipalities | `id`, `name`, `stateProvinceId`, `stateProvinceName`, `postalCode` |
| `/api/industries` | All industries; name ascending | `id`, `name` |
| `/api/NavigationItems` | Roots with immediate children | `id`, `title`, `subtitle`, `type`, `link`, `icon` |

Without explicit sorting/pagination, previously unordered catalogues remain unordered. Paged or sorted lists default to name ascending (navigation uses title) with an ID tie-breaker. Mode/term lists accept `status=true|false`; modes also accept `showInactive=true`. Explicit status takes precedence. Other catalogues have no active/inactive filter.

City lists additionally accept positive `stateProvinceId` and `countryId` filters, which can be combined. Search covers city name, province name and postal code. The query uses the underlying tables, so it does not require the missing legacy `vcitymunicipality` view.

```text
GET /api/city-municipalities?countryId=1&search=Cebu&pageNumber=1&pageSize=25&sortBy=name&sortDirection=asc
GET /api/payment-modes?showInactive=true
```

`GET /api/currencies/base` remains a single-object response, or 404 if no base exists. If legacy data contains multiple base currencies, it returns the lowest ID deterministically; this refactor does not rewrite the other base flags.

## Navigation

Legacy root and child IDs remain strings in list responses. A root has the original six menu fields plus `children`; each child has only the six menu fields. Detail IDs remain numeric. Pagination counts and selects roots, retaining all immediate children for each selected root. Children use ascending numeric ID ordering. Searching roots or immediate-child titles/subtitles/links returns the complete matching root group, without dropping siblings. The existing two-level response is preserved; grandchildren are not added to the menu payload.

Writes validate parent existence, reject self-parenting and ancestor cycles, and ignore posted parent/children objects. Hierarchy changes acquire row locks in ID order within the write transaction to serialize concurrent reparenting. Deleting an item with children returns 409 instead of cascading away the menu.

## Write validation and models

- Write DTOs accept scalar business fields and the record ID. Posted audit fields and nested related objects are ignored; the server stamps UTC dates and the authenticated user where audit columns exist. Original creation attribution is retained on updates.
- Route/body IDs must match on PUT; missing records return 404. Required names/codes, schema lengths, nonnegative payment-term days, state/province existence and currency exchange rates (0.0001-999999.9999) are validated before saving.
- The checked-in schema uses caller-supplied IDs for Payment Terms, Currencies and Navigation Items. POST therefore requires a positive ID for these catalogues. Currency IDs retain the `short` range. Auto-increment catalogues accept omitted/zero IDs and retain support for explicit positive IDs.
- Delete checks reject referenced records with 409, including customer/supplier terms, payment modes used by transactions, countries used by companies/provinces, industries used by companies, and cities used by addresses. Database constraint conflicts return safe errors. No underlying stored procedures or data are rewritten.
- EF string-length metadata now matches the checked-in schema for payment-mode names, payment-term codes, city postal codes, currency alternate codes, and navigation type/icon/link fields. No database migration or page-preference keys are introduced.

## Verification

`ReferenceDataTests` covers all seven list/detail/write contracts, supported sorts in both directions, literal search, pagination validation, audit protection, ignored nested objects, shared reads across companies, onboarding access, status defaults, base-currency reads, city filters, schema bounds, deletion guards and navigation hierarchy behavior. `SwaggerTests` verifies unique request/detail schemas for all seven routes.

Verified on 2026-10-02: Release build succeeded and the complete isolated MySQL suite passed **444 tests**, with zero failures or skips. The two existing report-controller unused-constant warnings remain. Generated results are in `bin/phase3-verification/phase3-mysql.trx` and `bin/reference-full-tests.log`.

```powershell
dotnet build negosuite-api.sln -c Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3MySql.ps1
```

The test script uses temporary databases on the isolated MySQL instance at loopback port 33316. Restart the API before V1/V2 client acceptance testing. Automated fixtures do not replace browser acceptance against development data.
