# Supplier list expansion for web v2

Supplier Maintenance is available in the sibling web v2 app at `/purchases/supplier` (module 3120).

`GET /api/suppliers` now accepts optional `includeDetails=true` alongside existing criteria, paging, search, and sort parameters. It adds supplierContacts and supplierAddresses, including nested city/province data, to each result. Without this option, the existing nine-field list shape is unchanged. Paging is applied before children are fetched in two batched queries; unpaged filtered export uses a scoped subquery.

The Supplier preference endpoint already supports the suppliers page key. No database migration is needed for this list expansion. Rebuild/restart the API before using the new web list columns and Excel export. Existing CRUD validation and tenant ownership behavior are preserved.

All 114 API compatibility tests passed with no skips against isolated native MySQL schemas, including expanded page/export contents and unchanged legacy responses. The web implementation passed 115 tests, development/production builds, and isolated browser checks with synthetic responses. See the sibling web repository's docs/supplier-maintenance.md for UI behavior and deployment details. No application business data was changed by verification.
