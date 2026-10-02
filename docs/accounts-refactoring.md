# Accounts and Account Categories

Both APIs now use dedicated list, create, update and detail DTOs under `Contracts/Accounts`, and scoped EF query/services. The Account and AccountCategory persistence models remain required by transactions and reports; no schema or stored procedure change is needed.

## Compatible list calls

`GET /api/accounts?criteria={...}` and `GET /api/account-categories?criteria={...}` still return complete arrays when pagination is omitted. Supply URL-encoded JSON containing `userConfigId` matching the `configUuid` header. Accounts retains the optional `criteria.categoryId` filter; null/zero means all categories.

Both support:

- `pageNumber` and `pageSize` together; page starts at 1 and size is 1–200. Paged responses contain `items`, `pageNumber`, `pageSize`, `totalCount`, and `totalPages`.
- `search`: trimmed, literal substring search across text list columns, including account category and parent names/codes. Blank search imposes no filter.
- `sortBy` and `sortDirection=asc|desc`. Sorting, search, count and paging run in MySQL. ID provides a stable tie-breaker.

| API | Sort fields | Default |
| --- | --- | --- |
| Accounts | `code`, `name`, `categoryName`, `parentAccountCode`, `parentAccountName`, `requireCustomer`, `requireSupplier`, `type` | `code` ascending |
| Account Categories | `name`, `type`, `accountCodePrefix`, `orderNo`, `accountCount` | `orderNo` ascending |

Legacy list fields are retained, including the empty Account `sortCode` and category `accountCount`. Category counts are restricted to the selected company. Counts and page contents are separate reads and may observe intervening writes.

`GET /api/accounts/header` and `/api/accounts/link` retain their distinct response shapes and full arrays, now restricted to the company selected by `configUuid`. The link response includes the parent account; both include category details. These lookups still include all company accounts, as before. Their names do not imply a new header-only filter.

## Writes and ownership

Create/update requests accept scalar business fields; legacy detail objects may still be submitted. Nested category/parent objects and supplied audit fields are ignored. Creation time and update time are set by the server; existing creator/updater IDs are preserved on update. Account names are required (maximum 150 characters), account codes allow up to 20, and category types are required (maximum 10). Existing type codes and `isSubAccount` behavior are retained.

Detail/update/delete queries are company scoped. Missing or inaccessible IDs return 404; request company mismatches return 403. Malformed criteria and invalid paging/sorting return 400. Account categories and every ancestor of a selected parent must belong to the selected company. Self-parenting and cyclic ancestry return 400. Database foreign-key deletion failures return a useful 400 response. Hierarchy validation does not introduce a concurrency-locking redesign.

These checks use the existing validated `configUuid` context. The broader shared user/company membership and permission review identified previously remains separate work. The temporary multiple-web-session setting is unchanged.

## Column preferences

- `/api/me/page-preferences/accounts`: module `3210`; columns `code`, `categoryName`, `parentAccountCode`, `parentAccountName`, `requireCustomer`, `requireSupplier`, `type`.
- `/api/me/page-preferences/account-categories`: module `3220`; columns `type`, `accountCodePrefix`, `orderNo`, `accountCount`.

These reuse the existing preference table, user/company ownership, permission checks and version conflict handling.

## Verification

`AccountTests` covers SQL translation for each allowed sort, legacy fields and unpaginated lists, category filters, wildcard search, paging, company isolation, header/link responses, DTO round trips, audit/nested-object overposting, parent cycles, reference validation and guarded deletion. Swagger and page-preference tests cover both modules.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3MySql.ps1` for isolated database verification. Restart the API before testing existing account/category maintenance and transaction lookups in the clients.

October 1, 2026 verification: Release solution build succeeded and all 380 compatibility tests passed (zero failures/skips) against isolated MySQL on port 33316. The initial restricted-process run could not acquire Windows TLS credentials; verification succeeded outside that restriction. The clean compilation still reports the four existing unrelated warnings in DiscountTypes, TaxRates, ReceivableReports and PayableReports. No development database records or schema were changed. Browser/client acceptance remains to be performed after restarting the API.
