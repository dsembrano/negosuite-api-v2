# Bills and payments API refactoring

Implemented in `BillsController` (`/api/bills`) and the active `PaymentsController` (`/api/payments`, including `/bill` transaction routes). `BillPaymentsController` is marked unused in the repository and still targets the old bill-payment table; it is unchanged.

## Lists and compatibility

Existing `GET ?criteria={...}` calls still return the complete JSON array. Both lists default to posted records and retain their original fields and default ordering (date, reference number, then ID for deterministic ties). Payments retain the legacy JSON spelling `customerrName`.

Supply both `pageNumber` (at least 1) and `pageSize` (1–200) to receive `{ items, pageNumber, pageSize, totalCount, totalPages }`. Filtering, counting, sorting and pagination execute in parameterized SQL. An out-of-range page has an empty `items` array. Invalid paging, sorting or criteria returns 400.

Both endpoints accept:

- `criteria`: JSON containing required `userConfigId`, optional `supplierId`, `referenceNo`, `periodStart`, `periodEnd`, and `arrayString`.
- `search`: trimmed literal substring search across reference number, party names, terms/payment mode, notes and status labels. Bills also search supplier TIN; payments also search payee, check number and paid-through account name.
- `sortBy` and `sortDirection=asc|desc`, using the fields below.
- `status=-1|0|1`: explicit deleted, draft or posted selection. Omitted defaults to posted. Previously ignored status/showDeleted fields inside criteria stay ignored.
- Payments additionally accept the query parameter `isBillPayment=true|false`. Omit it to include both kinds, as before.

| Endpoint | Sort fields |
| --- | --- |
| Bills | `billNo`, `billDate`, `dueDate`, `supplierName`, `supplierTIN`, `paymentTermName`, `amount`, `balance`, `notes`, `status`, `statusName` |
| Payments | `referenceNo`, `referenceDate`, `supplierName`, `customerrName` (also `customerName`), `payee`, `checkNo`, `paymentModeName`, `paidThroughAccountName`, `amount`, `balance`, `notes`, `status`, `statusName` |

Dates preserve the stored-procedure contract: both bounds must be present, dates are reduced to midnight, and the end date is inclusive at midnight. A single bound is ignored. Empty responsibility-center strings impose no filter. Nonempty strings must contain positive integer IDs separated by commas; records must contain all selected IDs.

Intentional correction: the local `GetPayments` procedure builds `JSON_ARRAY()` even when IDs are supplied. The new payment query uses the actual selected IDs. Filtered results can therefore differ from that defective procedure. No stored procedure was modified.

Example (URL-encode the criteria JSON in an actual request):

```text
GET /api/bills?criteria={"userConfigId":16}&pageNumber=1&pageSize=50&search=vendor&sortBy=dueDate&sortDirection=desc
GET /api/payments?criteria={"userConfigId":16}&isBillPayment=true&pageNumber=1&pageSize=50&sortBy=amount&sortDirection=desc
```

## Details and writes

List DTOs, SQL query helpers and services separate reads from controllers. Detail reads use dedicated response DTOs, preserve the nested JSON contract and use no-tracking split queries. Company scope comes from the validated `configUuid` header. Criteria/body company mismatches return 403; missing or inaccessible detail/update/delete IDs return 404. Supplier, account, child-entry and payment-target references are validated before writes. A missing payment detail now returns 404 instead of dereferencing null.

Create/update actions bind `BillCreateRequest`/`BillUpdateRequest` and `PaymentCreateRequest`/`PaymentUpdateRequest`, including the bill-payment routes. Bill lines and journals have separate request DTOs that preserve `deleted` and `touched` where applicable. Explicit mappings convert these requests into persistence entities. Nested master objects sent by older clients are ignored: supplier, customer, account, item, tax and linked-journal objects cannot be written through a bill/payment payload. Scalar IDs remain the reference source. Unrelated journal parent IDs (such as a sales invoice ID on a bill journal) are also excluded from write contracts. Header reference numbers are required and limited to 50 characters; invalid input returns 400.

Detail and create responses use `BillDetailDto`, `BillLineDto`, `PaymentDetailDto` and `TransactionJournalDto`, with existing lookup DTOs reused where possible. They retain legacy field names, nulls, amounts, timestamps and nested detail data. Audit fields remain accepted for compatibility; existing server timestamp behavior is unchanged. Swagger now distinguishes create/update inputs from detail outputs.

The unused `SPBill` and `SPPayment` models, DbSets and function mappings were removed. `Bill`, `BillDetail`, `Payment` and shared `JournalEntry` entities remain necessary for persistence; their table/column mappings are preserved. The legacy `BillPayment` entity remains referenced by the legacy controller and was retained. No database tables or procedures were removed.

Bill inventory reads are batched and asynchronous, replacing per-line blocking calls. Existing landed-cost, last-purchase-cost and weighted-average calculations remain; zero-quantity landed-cost entries are rejected and removing all remaining stock avoids division by zero. Bill journal rounding and the payment-applied deletion guard remain.

Payment routes preserve existing posting, nested deletion and bill-balance application/reversal behavior, including their existing amount precision. This refactor does not redesign allocation editing: clients must still delete/recreate an allocation to change its applied amount or target. General-payment and bill-payment write routes retain their existing distinction. Concurrent posting control is outside this change.

## Column preferences

GET/PUT `/api/me/page-preferences/bills` uses module `4210`; `/api/me/page-preferences/payments` uses module `4240`. They share the existing authenticated user/company scope, version conflict handling and reset contract. Mandatory bill/reference numbers are ignored in visibility overrides. Optional column keys follow the list fields except IDs, responsibility-center JSON and numeric status; payment customer visibility uses `customerrName`. No new schema migration is required beyond the existing preference table.

## Verification

Regression coverage is in `BillPaymentTests.cs` and the extended `PagePreferenceTests.cs`. Run `scripts/Test-Phase3MySql.ps1` for the full suite against isolated MySQL fixtures, including Swagger generation. Coverage includes SQL translation for every sort, legacy arrays, paging, search, responsibility centers, status/type filters, company isolation, bill inventory updates/deletion, payment application/reversal and page preferences.

Verification on October 1, 2026: all 323 tests passed, with no failures or skips. This includes full-detail request round trips, ignored master-object writes, unrelated journal-parent exclusion, malformed child collections and Swagger DTO schemas. The Release build succeeds; four existing warnings remain in unrelated controllers.

Read-only comparison against localhost company 16 matched all 334 bills and 1,326 payments, the first 50 rows of each list, and five detail samples per module. September 2026 matched 0 bills and 11 payments. These comparisons used empty responsibility-center filters. Development data and stored procedures were not changed. Client-side acceptance of the new controls remains to be performed after restarting the API.

After the DTO follow-up, a fresh read-only comparison of 10 populated bill details and 10 payment details matched the legacy entity JSON, including nested data.
