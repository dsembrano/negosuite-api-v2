# Sales Receipts and Invoice Payments API refactoring

`api/sales-receipts` and `api/sales-invoice-payments` use dedicated list criteria/response DTOs, query helpers and services. Parameterized SELECT queries reproduce the local procedures' selection rules while allowing MySQL to filter, count, sort and limit rows. No stored procedure or database schema changes are required.

## List compatibility

Unpaginated requests still return complete arrays: 21 fields for receipts and 15 for payments. Existing date/reference ordering and status labels are retained, with ascending ID as the final tie-breaker. The default remains **posted only** (`Status = 1`). Historically ignored criteria properties, including `showDeleted` and `status`, remain ignored.

Both lists retain serialized `criteria` with `userConfigId`, `periodStart`, `periodEnd`, `customerId`, `referenceNo`, and `arrayString`. Criteria JSON must be URL-encoded. The company must match the company validated by `configUuid`; a mismatch returns 403, and malformed/missing criteria returns 400.

- Reference and customer matching are exact. Empty reference means no reference filter.
- Dates apply when both bounds are present, with time portions removed. The inclusive end date remains midnight, matching existing valid procedure calls. A single bound leaves dates unrestricted, consistent with Sales Invoices and Invoice Payments.
- Receipt calls with missing dates previously failed inside the local stored procedure. They now return the matching list, which also makes reference-only receipt requests work.
- Responsibility-center `arrayString` remains a comma-separated list of positive integer IDs. Every selected ID must occur in the record's JSON. Empty/whitespace means no filter; malformed input returns 400. Values are bound as parameters, never executed as SQL.
- Receipt criteria also accepts `isPOS`. `true` restricts to POS receipts; `false` or omitted includes regular and POS receipts, preserving the old behavior.

## Optional query parameters

```http
GET /api/sales-receipts?criteria=%7B%22userConfigId%22%3A16%7D&pageNumber=1&pageSize=50&search=Acme&sortBy=amount&sortDirection=desc
GET /api/sales-invoice-payments?criteria=%7B%22userConfigId%22%3A16%7D&pageNumber=1&pageSize=50&sortBy=balance&sortDirection=desc
```

Supply both `pageNumber` (1 or greater) and `pageSize` (1-200) to receive `{ items, pageNumber, pageSize, totalCount, totalPages }`. Invalid pairs, values or overflowing offsets return 400. Pages beyond the end return empty items with actual totals. Search and sorting also work without pagination.

`status`, outside criteria, selects `-1` (deleted), `0` (draft), or `1` (posted). Omit it for the posted-only default. Receipt labels remain Deleted/Draft/Posted. Payment labels retain the original Unapplied/Fully applied/Partially applied logic, including Unapplied for zero amount and zero balance.

`search` trims surrounding whitespace and searches the following text columns using database substring/collation behavior. Blank search has no effect; numeric/date fields are not converted to text.

| List | Search fields |
| --- | --- |
| Receipts | receiptNo, customerName, customerTIN, billingAddress, billingContactName, billingContactEmail, shippingAddress, shippingContactName, shippingContactEmail, paymentModeName, notes, statusName |
| Payments | referenceNo, customerName, paymentModeName, depositToAccountName, notes, statusName |

`sortBy` accepts any listed search field, plus `receiptDate`, `createdDate`, `amount`, `balance`, `status` for receipts, or `referenceDate`, `amount`, `balance`, `status` for payments. `sortDirection` accepts `asc` or `desc`. Invalid values return 400. Defaults remain date then reference ascending. Date sorts retain reference ascending as a secondary key; all sorts end with ascending ID. Nullable fields follow MySQL null ordering; amounts and balances sort numerically.

Unpaginated lists use one projection query. Paginated lists use count plus a bounded projection query. Count/page are separate reads and may observe intervening concurrent writes.

## Detail and transaction compatibility

Detail reads use no-tracking identity resolution and split queries to avoid multiplication of detail/journal/contact rows. The existing entity response graphs and transaction request bodies remain intact. Split queries can observe concurrent changes between reads.

Every parent operation is company-scoped. Cross-company/missing detail, update and delete IDs return 404; supplied company mismatch returns 403. Write validation rejects invalid customer/deposit-account/item references, null collections/entries, duplicate child IDs and attempts to update another transaction's detail/journal entries. Payment modes remain global lookups. Receipt tax/location references and journal customer/supplier references are also checked. Child transaction IDs are assigned from the update route.

Payment target journals and their linked sales invoices are checked for company ownership **before** any balance mutation, including deleted allocation entries. Missing or foreign targets return 400. The balance mutation queries themselves are also company-scoped.

The existing transaction rules are preserved: duplicate-reference conflicts, journal rounding, nested `deleted` flags, receipt/POS sequence generation, payment application/reversal, and the payment deletion guard. POST returns 201 with Location; successful PUT/DELETE returns 204. Receipt SR and POS sequences remain separate. As before, POS creation always allocates a POS number, and sequence allocation remains a separate transaction from receipt saving.

Payment allocation changes use the existing **delete old allocation and add replacement** contract. Editing an existing allocation's amount in place does not recalculate its target balance in the legacy implementation. Deleting the payment header does not itself reverse allocations; normal clients must reverse allocations first, and the existing `Balance < Amount` deletion guard remains. This refactor does not redesign those financial rules, audit attribution, write DTOs or concurrent posting. These remain separate work from the list/query changes.

## Page preferences

GET/PUT `/api/me/page-preferences/sales-receipts` requires module `4120`; `/api/me/page-preferences/sales-invoice-payments` requires module `4125`. Both use the existing signed user/company scope, view/create/edit-or-admin permission rule, version checks, reset and 409 conflict behavior.

Optional columns are the supported list columns except mandatory receiptNo/referenceNo and raw status. Use statusName for displayed status. Dynamic responsibility-center columns are not included in the fixed whitelist. The existing preference table/migration is reused; Angular integration is separate.

## Verification

Run `scripts/Test-Phase3MySql.ps1` for the full isolated MySQL suite. New coverage includes SQL translation for every sort, HTTP list contracts, posted/draft/deleted labels, receipt POS filtering, date boundaries, responsibility-center containment, literal search, numeric sorting, pagination, invalid inputs, uncapped arrays, company isolation, receipt nested writes/deletes, SR/POS numbering, payment application/reversal, rejection of foreign payment targets without partial writes, and preference permissions/versioning. The existing Swagger generation test also runs.

Read-only comparison against the original controllers matched full-list, period, reference and responsibility-center results, and five sampled detail responses per module. Receipt POS-filtered output also matched (the selected sample had no POS rows; nonempty POS coverage is in the isolated tests). Local serialized payload sizes:

| List | Full array | First 50-row page |
| --- | ---: | ---: |
| Sales Receipts | 12,114,697 bytes | 39,892 bytes |
| Invoice Payments | 3,996,468 bytes | 18,216 bytes |

These are local samples, not production latency guarantees. Development data was only read; test mutations use disposable schemas on the separate port-33316 MySQL instance.

Final verification on 2026-10-01: Release build succeeded; all 263 compatibility tests passed with no skips, including Swagger generation and the transaction tests. The existing four unused-variable/field warnings are outside these changes. The development API was not restarted; rebuild/restart it before client acceptance testing.
