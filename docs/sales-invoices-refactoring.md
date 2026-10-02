# Sales Invoices API refactoring

`api/sales-invoices` now uses a dedicated list criteria/response DTO, `SalesInvoiceQuery` and `SalesInvoiceService`. Filtering, search, sorting, counting and pagination execute in MySQL. The old controller called `GetSalesInvoices`, loaded all rows, and sorted them in memory. The new parameterized SELECT reproduces the procedure's observed selection rules and supports SQL composition without changing the procedure or database schema.

## Compatibility

- Requests without pagination still return the complete array with the same 22 properties and status labels.
- The default is **posted invoices only** (`Status = 1`), as in the local procedure. Legacy `showDeleted` and `status` properties inside criteria remain ignored.
- Default ordering remains invoice date ascending, then invoice number ascending. ID is now the final tie-breaker.
- `criteria.userConfigId` is required and must match the company validated by `configUuid`. Malformed/missing criteria return 400; company mismatch returns 403.
- `referenceNo` retains exact invoice-number matching; null or empty means no reference filter. `customerId` and `supplierId` retain exact matching.
- Dates are applied only when **both** `periodStart` and `periodEnd` are present, with their time portions removed. The inclusive end comparison remains midnight on the end date, matching the old procedure; it is not expanded to the end of that day.
- `arrayString` remains a comma-separated list of responsibility-center IDs, e.g. `"1,2"`. All selected numeric IDs must occur in the invoice JSON. Empty/whitespace means no filter. Malformed/non-positive/out-of-range IDs return 400; SQL fragments are never evaluated.
- Existing detail, POST, PUT and DELETE routes and transaction payload shapes remain available. POST returns 201 with Location; successful updates/deletes return 204.

## Optional list parameters

```http
GET /api/sales-invoices?criteria=%7B%22userConfigId%22%3A16%7D&pageNumber=1&pageSize=50&search=Acme&sortBy=balance&sortDirection=desc
```

Supply both `pageNumber` (1 or greater) and `pageSize` (1-200) for `{ items, pageNumber, pageSize, totalCount, totalPages }`. Invalid pairs/values or overflowing offsets return 400. Pages beyond the end have empty items and actual totals. Search and sorting also work on unpaginated requests.

`search` is a trimmed substring match against invoice number, purchase order number, customer name/TIN, payment term, notes, billing/shipping addresses and contact names/emails, and displayed status name. Blank search has no effect. Text uses MySQL collation; amount/date fields retain their native types and are not converted to text for search.

`sortBy` supports `invoiceNo`, `invoiceDate`, `dueDate`, `purchaseOrderNo`, `customerName`, `customerTIN`, `billingAddress`, `billingContactName`, `billingContactEmail`, `shippingAddress`, `shippingContactName`, `shippingContactEmail`, `amount`, `balance`, `paymentTermName`, `notes`, `status`, and `statusName`. `sortDirection` accepts `asc` or `desc`. Invalid values return 400. Ties use ascending ID; sorting by invoice date also retains invoice number as its secondary key. Nullable values follow MySQL null ordering.

The new **query parameter** `status` selects `-1` (deleted), `0` (draft) or `1` (posted). Omit it for the existing posted-only behavior. Status labels use server-local today, captured once per list request, and preserve the original Paid/Partially paid/Due today/Due in N days/N days overdue behavior, including the original precedence for zero-value invoices.

## Detail and transaction handling

Detail reads use no-tracking identity resolution and split queries to avoid multiplying invoice details, journal entries and customer contacts/addresses in a single large join. Dedicated detail DTOs now preserve the JSON graph. Split queries and separate count/page queries may see intervening concurrent writes.

All invoice routes now restrict the parent invoice to the validated company. Cross-company or missing IDs return 404 on detail/update/delete. Supplied body-company mismatches return 403. Write validation checks customer/supplier ownership, payment-term existence, inventory-location ownership, item/account ownership and that submitted existing detail/journal IDs belong to this invoice. Duplicate child IDs and null collections/entries are rejected. Update assigns child invoice IDs from the route.

Existing invoice-number generation, duplicate-number conflicts, journal rounding, nested deletion flags, invoice totals/balances, timestamps and payment-applied deletion protection are retained. The [remaining transaction rollout](transactions-refactoring.md) adds create/update DTOs and ignores nested master objects on writes. Accounting calculations, audit attribution and concurrent posting/sequence allocation are unchanged. The existing sequence allocation transaction remains separate from invoice saving. Client transaction acceptance should still cover normal posting and payment workflows.

## Page preferences

GET/PUT `/api/me/page-preferences/sales-invoices` uses Sales Invoice permission `4110` (view/create/edit or admin), signed user identity, company scope, versions, reset and 409 conflicts. Optional columns are the supported list column names except mandatory `invoiceNo` and raw `status`; the display status column is `statusName`. Existing preference storage is reused. Dynamic responsibility-center columns are not included in this fixed whitelist. Angular integration is separate.

## Verification

`scripts/Test-Phase3MySql.ps1` covers invoice list filters, date boundaries, responsibility-center containment, status labels, numeric and text sorting, SQL translation of every sort, pagination/validation, uncapped legacy arrays, company isolation, create/update/delete, journal rounding, nested-entry ownership and deletion, automatic invoice numbering, preference permissions/versioning, and complete Swagger generation.

Read-only development comparison: the original and refactored controller responses matched byte-for-byte for company 16 across full lists, September 2026, one-sided date bounds and a responsibility-center filter. Five sampled detail responses also matched. The full-list sample was 1,260,513 serialized bytes; the first 50-row page was 25,967 bytes. These are local samples, not production performance guarantees. No development-data writes or procedure/schema changes were made.

Final verification on 2026-09-30: Release build succeeded and all 203 tests passed with no skips, including Swagger generation and isolated MySQL transaction tests. The running development API was not restarted; rebuild/restart it before client acceptance testing.
