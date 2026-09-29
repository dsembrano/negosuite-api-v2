# Items API refactoring

Items now use explicit request/response DTOs, `ItemService`, and SQL-translated search/sorting. Existing routes, the 15-field list shape, nested detail shape, SKU duplicate message, HTTP 201 creation with Location, HTTP 204 updates/deletes, and the inventory helper used by posting controllers are retained.

## List requests

```http
GET /api/items?criteria=%7B%22userConfigId%22%3A16%7D&pageNumber=1&pageSize=50&sortBy=rate&sortDirection=desc
```

Send the existing authentication, company UUID and session headers. Existing calls without paging continue to return a complete array. Supplying both `pageNumber` and `pageSize` returns `{ items, pageNumber, pageSize, totalCount, totalPages }`. Page numbers start at 1; page size is 1–200. Missing paired parameters, invalid values or excessive offsets return 400. Pages beyond the end return empty items with actual totals.

The serialized `criteria` retains `userConfigId`, `showInactive`, `itemType` and `itemCategoryId`. Unknown fields remain ignored. Active-only is the default. New explicit query parameters `toSell` and `toPurchase` optionally filter their corresponding flags; properties with these names inside legacy criteria remain ignored, as before.

`search` matches SKU/code, name, unit, category name and displayed type name (Goods/Service), with whitespace trimmed and existing database substring/collation behavior. Filters run before counting, sorting and paging. No numeric text conversion or phone-style normalization is performed.

`sortBy` supports `code`, `name`, `itemCategoryName`, `type`, `typeName`, `unit`, `rate`, `cost`, `status`, `toSell`, `toPurchase`, `trackInventory`, and `reorderPoint`. `sortDirection` is `asc` or `desc`. Both are optional, defaulting to name ascending. Invalid values return 400; ties use ascending ID in either direction. Numeric and boolean fields sort by their native database types. Nullable fields use MySQL null ordering.

Unpaginated lists use one projection query; paginated lists use count plus projection. No related entity collections are loaded for lists. Count and data are separate reads, so concurrent writes can change results between them.

## Other routes and writes

- Detail, create, update, delete, units and average-cost routes use the company resolved by `configUuid`. A mismatched supplied company returns 403; an inaccessible item returns 404. Existing authentication/module behavior is otherwise unchanged.
- `GET /api/items/units?criteria=...` still returns distinct nonempty `{ name }` entries across all items in the selected company, including inactive items.
- `GET /api/items/average-cost/{id}` still returns a scalar weighted bill-detail rate. Division remains in MySQL to preserve its decimal rounding; no posted-only filter was introduced. Item and bill ownership are checked. No details or zero total quantity return 404 rather than dividing by zero.
- Saves map editable item fields explicitly, validate the existing name length and company-owned category/account/tax references, and preserve timestamps/audit identity fields. AverageCost and LastPurchasedDate are maintained by purchase workflows and cannot be overwritten by stale item-edit payloads. Cost, rate, opening quantity, reorder point and existing item flags remain editable. Extra navigation/audit properties from legacy full-detail payloads are accepted and ignored.
- SKU uniqueness remains per company and applies whenever code is non-null, including empty strings. The existing database unique index also protects concurrent requests; duplicate-key races return the same SKU message. No schema/index migration was introduced.
- Each mutation uses a single SaveChanges transaction. Referenced-item deletion retains the existing database-FK guard and error. No new optimistic-concurrency column or inventory recalculation was added. The public static `GetInventoryItem` helper and its result type remain available to posting workflows.

## Page preferences

GET/PUT `api/me/page-preferences/items` uses the existing preference table, signed user identity, company scope, version checks, reset behavior and 409 conflicts. It requires Item module `3130` (view/create/edit or admin), independently of Customer/Supplier permissions. Optional columns are code, itemCategoryName, typeName, unit, rate, cost, toSell, toPurchase, trackInventory, reorderPoint and status. Name/unknown columns are ignored.

No new preference migration is required; installations still need the existing `001_user_page_preference.sql`. This change exposes API support only; Angular integration and browser acceptance remain separate.

## Verification

`scripts/Test-Phase3MySql.ps1` runs the compatibility suite using newly generated schemas on the isolated port-33316 MySQL instance. Item tests cover legacy serialization, type/category/active filters, SKU/category/type-name search, pagination, numeric/stable sorting, all sort-field SQL translations, invalid inputs, company isolation, invalid references, duplicate SKU, full-detail writes, protected purchase/audit values, units, weighted cost, zero quantities, create/update status codes, guarded deletion, and item preferences.

All **143 tests passed**, with no skips. Existing Customers/Suppliers and session tests also passed. Development database data was only read; no migrations or writes were applied there.

The selected-company unpaginated list and 20 sampled detail responses matched the original controller byte-for-byte. Local read-only benchmark (three warmed runs, including serialization, excluding HTTP transport):

| Request | Median | SQL queries | JSON bytes |
| --- | ---: | ---: | ---: |
| Original full list | 120.2 ms | 1 | 2,526,819 |
| Refactored full list | 129.3 ms | 1 | 2,526,819 |
| First page, 50 items | 13.8 ms | 2 | 13,489 |

The optimization benefit is bounded page size; the full-list path retains its original payload cost and does not demonstrate a speedup in this sample. These measurements are not production latency guarantees. Build has four existing unused-variable/field warnings outside the Items changes.
