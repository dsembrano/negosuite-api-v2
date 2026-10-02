# Remaining transaction controller refactoring

This rollout extends the Bills/Payments pattern to the remaining active transaction APIs. Existing paths remain available; list calls without paging still return complete arrays. Report/analytics controllers are outside this document-entry rollout.

| Controller/API | Changes | Preference key / module |
| --- | --- | --- |
| General Journals `/api/general-journals` | SQL list, search/sort/paging, write/detail DTOs, company and child ownership, allocation-target validation | `general-journals` / `4310` |
| Receiving Reports `/api/receiving-reports` | SQL list, search/sort/paging, write/detail/line DTOs, company and child ownership | `receiving-reports` / `4405` |
| Inventory Adjustments `/api/inventory-adjustments` | Same list and DTO features, location/account validation | `inventory-adjustments` / `4410` |
| Stock Transfers `/api/stock-transfers` | Same list and DTO features, source/destination location validation | `stock-transfers` / `4420` |
| Stock Issuances `/api/stock-issuances` | Same list and DTO features, location/party validation | `stock-issuances` / `4430` |
| Sales Invoices, Sales Receipts, Sales Invoice Payments | Added create/update/detail/line/journal DTOs to the previously refactored lists | Existing keys retained |
| Journal Entries `/api/journal-entries` | Typed unpaid-invoice, unpaid-bill and unapplied-credit lookups with SQL search/sort/paging, company scope and authentication | No standalone preference key |

The older `BillPaymentsController` and `ExpensePaymentsController` remain unchanged. Active bill/other payments use `/api/payments`; the old bill-payment controller is explicitly marked unused. The legacy expense-payment model lacks a company field on its header, so safely assigning ownership to old records requires a separate decision. Neither legacy endpoint nor its persistence model was deleted.

## Document lists

Supply URL-encoded JSON in `criteria`, including `userConfigId` matching the validated `configUuid` header. Existing reference/date/responsibility-center and applicable supplier/customer filters remain. A mismatch returns 403; malformed criteria returns 400. Empty responsibility-center strings impose no filter; nonempty values must be positive comma-separated integer IDs and match all selected IDs.

Optional query parameters:

- `pageNumber` and `pageSize` must be supplied together. Page numbers start at 1; page sizes range from 1 to 200. Responses are `{ items, pageNumber, pageSize, totalCount, totalPages }`. Paging beyond the last page returns empty `items`.
- `search` performs a trimmed literal substring search over the textual list columns, including display status labels.
- `sortBy` and `sortDirection=asc|desc` use the allowlists below. Sorting uses native numeric/date columns and an ID tie-breaker. Default order remains reference date, reference number, then ID.
- `status=-1|0|1` explicitly selects deleted, draft or posted documents.

| API | Allowed sort fields |
| --- | --- |
| General Journals | `referenceNo`, `referenceDate`, `notes`, `status`, `statusName` |
| Receiving Reports | `referenceNo`, `referenceDate`, `supplierName`, `amount`, `balance`, `deliveryReceiptNo`, `purchaseOrderNo`, `inventoryLocationName`, `notes`, `status`, `statusName` |
| Stock Transfers | `referenceNo`, `referenceDate`, `notes`, `fromInventoryLocationName`, `toInventoryLocationName`, `status`, `statusName` |
| Stock Issuances / Inventory Adjustments | `referenceNo`, `referenceDate`, `customerName`, `notes`, `inventoryLocationName`, `status`, `statusName` |

General Journals preserves its original default of excluding deleted documents while including drafts. Its existing `criteria.showDeleted=true` includes all statuses when no explicit `status` query is supplied. The four inventory document lists retain their posted-only default. Supplier/customer criteria are applied only to the modules that previously supported them.

Dates on these document lists preserve the both-bound, date-only contract: both bounds are required to filter, and the end date includes midnight. An omitted or single bound imposes no period filter. Intentional correction: the local Receiving Reports procedure previously failed when its dates were null; the new query supports those requests. The stored procedure itself was not changed.

Filtering, counting, ordering and paging execute in MySQL using parameterized queries. Counts and page contents are separate reads and can observe intervening writes.

## DTOs and writes

Each active document has separate `CreateRequest`, `UpdateRequest`, `DetailDto`, applicable `LineRequest`/`LineDto` and journal request types under `Contracts/Transactions`. Explicit mappings preserve scalar fields, precision, nullable values, timestamps, automatic-number flags and nested deletion flags. Existing lookup DTOs and the shared journal response DTO are reused. Full detail objects can be resubmitted by legacy clients; navigation objects such as item, account, customer, supplier and payment target are ignored on writes. Scalar IDs are validated before mutation.

Company scope is enforced for detail, update and delete operations. Missing/inaccessible parents return 404; wrong-company request bodies return 403. Duplicate/foreign child IDs, invalid company references, null child arrays and null child entries return 400. New child lines/journals are linked to their parent during updates, including Receiving Report and Stock Issuance journal relationships represented by EF shadow properties.

Existing posting calculations, duplicate-number responses, date assignment, journal rounding, automatic numbering, allocation reversal and deletion guards remain. Allocation edits still follow the existing delete-old/add-replacement contract. This rollout does not redesign concurrent posting or audit attribution. General Journal allocation targets and their linked bill/invoice are validated before changing balances.

Detail queries use no-tracking identity resolution and split queries where applicable. Missing General Journal details now return 404 instead of dereferencing null.

Unused stored-procedure result classes and their DbSets/function mappings were removed for these five document modules and the three previously refactored sales modules. Database entities and existing table/column mappings remain. No schema migration or procedure change is needed. The inventory, journal and transaction persistence models are still required by other modules and reports.

## Journal lookups

`unpaid-invoices`, `unpaid-bills`, and `unapplied-ar-credits` preserve their existing list fields and positive-balance selection. Date filters remain independently optional and retain their time component. Existing status behavior is unchanged; no posted-only restriction was added to these lookups.

They accept the same optional paging/search/sort parameters. Unpaid invoices sort by `invoiceNo`, `invoiceDate`, `dueDate`, `amount`, or `balance`; unpaid bills use `billNo`/`billDate` instead. Credits sort by `referenceNo`, `referenceDate`, `dueDate`, `amount`, `balance`, `customerName`, `source`, or `sourceName`.

The validated header supplies company scope. For compatibility, lookup criteria may omit `userConfigId`; a supplied mismatching ID is rejected. Both unapplied-credit endpoints now require authentication, matching the controller's other transaction lookups. Credit detail IDs from another company return 404.

## Verification and client acceptance

`OtherTransactionTests.cs` covers all five document list defaults, every supported sort, search/paging, criteria validation, cross-company access, duplicates, child insertion/deletion, full-detail round trips, master-object overposting protection, AR/AP balance application/reversal and journal lookup security. Existing sales transaction tests exercise the new DTO binding and automatic numbering. Swagger tests verify separate input/output schemas, and page-preference tests cover all five new keys.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3MySql.ps1` for isolated database verification.

October 1, 2026 verification: all 348 compatibility tests passed, with no skips or failures. The Release solution and standalone database-verification tool build successfully. Four existing warnings remain in unrelated API controllers. The read-only verification tool's direct Bills call was updated to supply the validated company context and accommodate the optional list parameters while remaining compatible with its .NET 6 baseline harness.

Read-only localhost company 16 comparison matched 468 General Journal rows, one Receiving Report row, five General Journal details, the Receiving Report detail, five Sales Invoice details and five Sales Invoice Payment details. Stock Issuance, Stock Transfer and Inventory Adjustment lists were empty on both implementations; populated behavior is covered by isolated fixtures. No Sales Receipt detail sample existed locally; its DTO writes/details are covered by the existing isolated tests. Development data was not changed.

Restart the API before client acceptance. Check posting and allocation reversal side by side, then enable list paging/sorting and the new preference keys in the client. Existing unpaginated clients continue to work.
