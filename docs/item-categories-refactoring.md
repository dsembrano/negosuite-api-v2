# Item Categories API refactoring

Item Categories uses explicit request/response DTOs and `ItemCategoryService`, with no-tracking projections for reads. Existing routes and the eight list/detail response fields remain: id, userConfigId, name, status, createdDate, lastUpdatedDate, createdByUserId and lastUpdatedByUserId. POST still returns 201 with Location; PUT and DELETE return 204 on success.

## Lists

Existing `GET /api/item-categories?criteria={"userConfigId":16}` calls return a complete array, including inactive categories. URL-encode the criteria JSON and send the existing authentication, configUuid and session headers. Unknown criteria properties remain ignored, including historically ignored `showInactive` and `status` properties.

Optional query parameters:

| Parameter | Behavior |
| --- | --- |
| `pageNumber`, `pageSize` | Supply both. One-based pages, size 1-200; invalid or overflowing offsets return 400. |
| `search` | Trimmed substring search on name, using the database collation. Blank search has no effect. |
| `status` | `true` for active only, `false` for inactive only; omit for both. This is a query parameter, outside criteria. |
| `sortBy` | `name`, `status`, `createdDate`, or `lastUpdatedDate`. |
| `sortDirection` | `asc` or `desc`. |

```http
GET /api/item-categories?criteria=%7B%22userConfigId%22%3A16%7D&pageNumber=1&pageSize=50&search=Hardware&sortBy=name&sortDirection=asc&status=true
```

Paged responses contain `{ items, pageNumber, pageSize, totalCount, totalPages }`. Filtering happens in SQL before counting and paging. Out-of-range pages contain empty items and the actual totals. Empty matches have zero total pages.

Paged requests default to name ascending; all explicit sorts use ascending ID to break ties. Supplying only a direction sorts by name. Nullable dates follow MySQL null ordering. Unpaginated requests without sort parameters retain the original unordered query; its order is not guaranteed. Invalid sort fields/directions return 400. Sorting and search also work without pagination.

Unpaginated reads use one projection query; paginated reads use a count and a bounded projection query. Count and page are separate reads, so concurrent writes may change the results between them. No schema or index changes are required.

## Writes and company scope

Every route uses the company resolved by `configUuid`. A mismatched criteria/body company returns 403. Missing or inaccessible categories return 404 on detail/update/delete. Missing or malformed criteria return 400 instead of a null-reference error. Existing authentication and module enforcement outside these checks remain unchanged.

Create/update requests accept id, userConfigId, name and status. Name is required and limited to the existing database maximum of 150 characters. Create ID must be omitted or zero; update ID must match the route. Existing full-entity payloads remain accepted, but extra audit fields are ignored. Updates load the owned category and map only name/status, preserving creation and audit identity fields. Timestamps are assigned by the server using the existing local-time convention. No new name uniqueness rule was added.

Deletion relies on database foreign keys. A category referenced by an item returns 400 with an explanatory message; neither record is removed. Unrelated database failures are not disguised as reference errors.

## Page preferences

GET/PUT `/api/me/page-preferences/item-categories` supports the same signed-user/company scope, optimistic version, 409 conflict and reset contract as other lists. It requires Item Category module `3135` (view/create/edit or admin). The optional column is `status`; mandatory `name` and unknown keys are ignored. It uses the existing preference table and migration. UI integration is separate from this API change.

## Verification

Run `scripts/Test-Phase3MySql.ps1`. Category coverage includes legacy eight-field serialization and uncapped arrays, active/inactive defaults, literal-character search, combined filters, SQL translation and HTTP results for every sort, stable pages, invalid inputs, company isolation, create/update status codes, audit protection, referenced deletion and preference persistence/permissions/versioning.

Tests use disposable schemas on the isolated port-33316 MySQL instance. Development application data is not changed.

Verification on 2026-09-29: all 153 compatibility tests passed, with no skips. Release compilation succeeded with four existing unused-variable/field warnings outside this refactor. The running development API was not restarted; client-side acceptance remains to be performed after restart.

Swagger follow-up: the category endpoint DTO is named `ItemCategoryDetailDto` to avoid colliding with the nested `Items.ItemCategoryDto` schema. JSON field names and values are unchanged. `SwaggerTests` requests the complete `/swagger/v1/swagger.json` document, verifies HTTP 200, and checks both category schemas and their references. This test runs without a database and reproduces the previous 500 before the rename.

After the Swagger fix, all 154 compatibility tests passed with no skips, including the full document-generation check and isolated MySQL tests.
