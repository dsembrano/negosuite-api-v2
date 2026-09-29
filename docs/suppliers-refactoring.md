# Supplier API refactoring

`SuppliersController` now delegates to `SupplierService`, with explicit list/detail/create/update DTOs. Existing routes and the supplier-specific contract remain intact:

- Unpaginated list requests return the complete array of the same nine summary fields. Contacts and addresses remain detail-only.
- Creation returns HTTP 201 and a `Location` header. Updates return HTTP 200; successful deletion returns HTTP 204. Save responses contain the saved supplier detail and nested data; client-supplied lookup graphs are ignored.
- Supplier addresses retain `isPrimaryAddress`. Supplier names retain the existing 100-character database limit; TIN and postal-code limits remain 50 and 20.

## Optional paging and search

```http
GET /api/suppliers?criteria=%7B%22userConfigId%22%3A16%7D&pageNumber=1&pageSize=50
```

Send the existing bearer token, `configUuid`, and session headers. The encoded `criteria` retains `userConfigId` and optional `showInactive`. With no paging parameters the response is the legacy array. Supplying both paging parameters returns `{ items, pageNumber, pageSize, totalCount, totalPages }`; `items` uses the original summary shape.

Page numbers start at 1. Page size is 1Ã¢â‚¬â€œ200. Missing paired parameters, invalid numbers, unsupported offsets and malformed/missing criteria return 400. Pages beyond the end return empty items and actual totals. Ordering is name then ID. Optional `search` filters name, TIN, tax-rate/payment-term name, contact name/phone/email, address lines, address/city postal codes, city and province before counting/paging. As with Customers, the trimmed term uses substring matching and related matches use SQL EXISTS so multiple matching children do not duplicate suppliers. Counts and page reads are separate; concurrent writes can change the data between them.

## Optional sorting

Append `sortBy` and `sortDirection` to either paginated or unpaginated list requests, for example `&sortBy=taxRateName&sortDirection=desc`. Supported fields are `name`, `tin`, `taxRateName`, `paymentTermName`, and `status`; directions are `asc` and `desc`, using the same exact casing as Customers. Omitted field/direction defaults to name/ascending. Unsupported values return HTTP 400. Suppliers have no credit limit, so `creditLimit` is not supported.

Sorting is translated to SQL and applied before paging, after company/inactive/search filters. Equal values use ascending ID as a stable tie-breaker in either direction. Nullable TIN and lookup names are supported; MySQL determines null ordering. Existing requests with no sorting keep name/ID ascending.

## Read and write behavior

List reads project only summary columns without tracking: one query for a full list, two for a page including its count. Details use separate no-tracking supplier/lookup, address and contact queries to avoid multiplying collection rows. No separate child-loading queries run for the list; related search uses EXISTS within the list/count SQL.

All endpoints scope suppliers to the company resolved by the existing `configUuid` filter. A mismatched body/list company returns 403; an inaccessible detail/update/delete ID returns 404. This uses the existing authentication/session model, without changing token membership or role permissions.

Writes validate company ownership, child ownership/duplicate IDs, lookup references and mapped lengths before changing the aggregate. Nested `deleted` flags remain supported; omitted/null collections preserve existing children. New children receive their parent ID through EF relationships. Extra legacy audit/lookup properties remain accepted but cannot overwrite server-managed timestamps or existing audit user IDs. Each mutation uses one atomic SaveChanges; referenced-supplier deletion preserves its existing error and rolls back child removals.

The existing lookup DTOs and pagination contract are reused from the Customers implementation. No database migration, index, frontend or session-switch changes are required.

## Validation

After the functionality parity review, all **114 tests passed**, with no skips, using `scripts/Test-Phase3MySql.ps1`. Sorting tests cover all five fields in both directions, nulls, stable ties, SQL translation and invalid-input rejection; HTTP tests verify sorting across page boundaries. Supplier cases cover legacy shape, HTTP 201/Location, paging boundaries, search/inactive/tenant filtering, name length, full-detail payload saves, nested CRUD, audit protection, foreign child rejection and rollback of child deletions when an FK blocks supplier deletion. Writes ran only in newly created schemas on the isolated port-33316 MySQL instance.

Earlier read-only development checks compared the complete selected-company list and 20 detail responses with the original controller: both matched byte-for-byte. Local warmed measurements (three iterations after warm-up, including JSON serialization):

| Mode | Median | Queries | Payload bytes |
| --- | ---: | ---: | ---: |
| Previous full list | 1.83 ms | 1 | 16,187 |
| Refactored full list | 1.30 ms | 1 | 16,187 |
| First page of 50 | 2.17 ms | 2 | 9,182 |

This small local sample does not establish production latency. Paging adds a count query while bounding transferred rows. No development data was modified. Browser-side acceptance remains to be performed.

## Page preferences and parity review

GET/PUT `api/me/page-preferences/suppliers` now supports the same per-user/per-company persistence, optimistic version checking, reset-to-defaults and conflict response as Customers. The supplier page requires supplier module `3120` (view/create/edit or admin); customer module `3110` alone does not grant access. Records are independent across the two pages.

Optional preference columns are `contact`, `address`, `tin`, `taxRateName`, `paymentTermName`, and `status`. `creditLimit`, mandatory name and unknown keys are ignored. These are visibility preferences, not changes to list serialization: the existing supplier list stays a summary; related data remains available through detail responses. A client must call the supplier preference endpoint to use persistence. This API change does not update the Angular UI.

The existing page-preference table/migration is reused; no additional migration was introduced or applied. See [page preferences](page-preferences.md) for deployment prerequisites.

Reviewed parity: criteria validation, active/company filtering, search, opt-in pagination, stable sorting, cancellation, explicit DTOs, reference validation, nested CRUD, audit protection, guarded delete and atomic saves. Existing supplier-specific differences are intentional: nine-field summary lists, HTTP 201 creation, 100-character name limit, `isPrimaryAddress`, and no customer credit-limit/business-style fields.

New tests cover related-search SQL translation, matching fields, duplicate child matches, paging/counts, tenant/inactive filtering, plus preference persistence, page/user/company isolation, module permissions, normalization, version conflicts and reset for both customer and supplier pages. Development data and configuration were untouched by this parity update.
