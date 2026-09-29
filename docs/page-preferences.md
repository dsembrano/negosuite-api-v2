# Page preferences

The API supports GET/PUT `api/me/page-preferences/customers` and `api/me/page-preferences/suppliers`. The existing Customer web integration uses the customer endpoint; supplier clients can use the supplier endpoint with the same contract.

Before deploying, apply `Db/migrations/001_user_page_preference.sql` to the intended database and rebuild/restart the API. The additive table is separate from legacy `User.UserUIConfig`. The migration has already been applied to the local development database on localhost:3306/negosuitedb; no production database was modified.

Requests and responses contain `{ "version": 1, "columns": { "address": true } }`. An absent record returns version 0 and an empty map. PUT inserts at version 0 or atomically updates the matching version, increments it, and returns the normalized map. Stale versions return 409. Empty columns restore defaults while retaining the version. Only known optional column IDs for the requested page are accepted into storage; mandatory/unknown IDs are ignored. Both pages accept contact, address, tin, taxRateName, paymentTermName and status; only Customers accepts creditLimit. Unknown page keys return 404. Customer and supplier records are independent.

Scope is derived from the signed `negosuite_user_id` claim and the company validated by ConfigUuidFilter. The endpoint requires an active user belonging to that company and the requested page permission: Customers 3110 or Suppliers 3120 (view/create/edit or admin). Posted user/company fields and X-UserLog are not identity sources. Both sign-in and refresh now issue this claim. Old claimless access tokens get 401 here and can use the existing refresh flow; the running API must be updated for refresh to mint the claim.

The web client debounces and serializes saves, offers Restore defaults, and retains unsaved layout with explicit Retry feedback on failure/conflict. It uses no localStorage preference cache. See the sibling web repository's `docs/customer-column-preferences.md` for UI behavior and verification boundaries.

All 114 API compatibility tests passed against isolated MySQL fixtures, including migration idempotence, storage across API hosts, user/company isolation, permissions, conflict/reset behavior, and signed claims from login/refresh. The user's running API process was not restarted and live authenticated application-data acceptance remains outstanding.
