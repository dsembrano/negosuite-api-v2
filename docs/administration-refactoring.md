# Users, roles, configs and shared company validation

This refactor keeps existing routes and legacy unpaginated arrays while separating administration input/output DTOs from EF entities. `UserService`, `UserRoleService`, `ConfigService` and `CompanyAccessService` now own their respective workflows. No schema migration is required. User, UserRole and Config remain persistence models used by authentication and transactions.

## Access rules

- `ConfigUuidFilter` requires an authenticated, active database user identified by the signed `negosuite_user_id` claim. The company UUID must exist and equal that user's current `ConfigId`. An unrelated valid UUID returns 403. JSON list/report criteria containing a company ID must match the same membership.
- Unknown, inactive or claimless identities return 401. Existing pre-claim access tokens must refresh or sign in again. Membership is checked on each request using these filters, so a stale token cannot retain company access after moving/deactivating its user.
- Users/Configs use `AuthenticatedUserFilter`, which permits authenticated company-less onboarding. Their company scope comes from the current database user; if a `configUuid` header is supplied, it must match. These endpoints do not grant access to arbitrary companies through route/body IDs.
- Company admins manage team users, role assignments, company-specific roles and company settings. Admin status is taken from a role belonging to the same company or a shared system role. Ordinary users may read/update their own profile and UI settings but cannot assign themselves roles or alter company membership.
- Shared roles (`UserConfigId = null`) remain available for reads/assignment but are read-only through company administration. Referenced roles cannot be deleted. Company operations cannot remove the last active administrator. Self-deactivation is rejected. User removal remains soft deactivation and revokes refresh sessions.
- `/api/configs` now returns only the caller's company. Template discovery remains available to authenticated users through `/api/configs/template`. Deletion of a company with members is rejected; bulk company/data erasure is not exposed by this API.

The temporary `Authentication:EnforceSingleWebSession=false` development setting is unchanged. When enforcement is enabled and `X-UserLog` is supplied, parsing is guarded and the user ID must match the signed identity. Session lookup is asynchronous and rejects outdated/revoked sessions. Omitting that header retains the existing non-web-client behavior.

## List options

`pageNumber` and `pageSize` are optional together (page >= 1, size 1–200). Omit both for the complete array; supply both for `{ items, pageNumber, pageSize, totalCount, totalPages }`. Search is a trimmed literal substring. Invalid paging, sort fields/directions or criteria return 400. Counts and pages are separate database reads.

| Route | Default | Sort fields |
| --- | --- | --- |
| `/api/users`, `/api/users/config` | Active users, name ascending | `name`, `email`, `mobileNo`, `userTypeName`, `userRoleName`, `status` |
| `/api/user-roles`, `/api/users/roles` | Name ascending | `name`, `notes`, `isAdmin` |
| `/api/configs` | Company name ascending | `companyName`, `email`, `createdDate` |

All allow `sortDirection=asc|desc` and a stable ID tie-breaker. Users support explicit `status=true|false`; the team route retains its exclusion of configuration-admin user types. The `config`/role list routes still accept JSON `criteria.userConfigId`. `/api/users/roles` retains full role fields, while `/api/user-roles` retains its smaller legacy list projection. Filtering, ordering, counting and pagination execute in MySQL.

## DTO and write behavior

- User responses never contain password hashes. Profile updates accept name/email/role ID; role changes require an administrator. Supplied company, user type, password, nested navigation and audit fields cannot change persisted values through profile updates.
- Config updates retain editable business and document settings and validate account ownership plus country/industry references. UUID, template flags, subscription identifiers/dates/limits, trial dates and feature entitlements are server-owned and ignored when a legacy full-detail body is submitted.
- Role requests accept role/business permission fields, validate permission JSON and bind ownership to the current company. Creation/update attribution is assigned by the server. Administrative role writes and user role/deactivation operations serialize on the company row for last-admin checks.
- Unique-key and referenced-record database failures return a safe 400 message without returning raw SQL errors.

## Invitations, activation and company setup

Public `/api/users` and `/api/users/new-account` are retained for invitation acceptance and email activation, respectively. They require a matching, open, unexpired email-log token of the correct action. Invitation company and role come from stored invitation data; activation always creates an unconfigured account. Stored activation password hashes are used instead of trusting the client to supply a hash. Responses omit passwords. Token consumption and user insertion are atomic; failed validation does not consume the token. Invitation acceptance checks subscription capacity under the company lock.

The email `member-invite` issuer now requires a current company admin, validates company/role, and sets a seven-day expiry. This small companion change prevents bypassing the registration rules by issuing an invitation for another company. Refresh also rejects inactive users.

`update-config` and `update-config-template` retain their existing procedure calls and returned user/config structure. Only the authenticated, unconfigured caller may initialize their own company. Templates must be marked as templates, and the initial admin role must be a shared system role. An advisory lock prevents simultaneous setup calls for the same user without changing procedure transaction handling.

Setup retains the client's existing 14-day, one-seat trial. For non-trial setup, the selected plan must be active, the user limit comes from its minimum-user setting, and billing mode must be M or Y. Dates/limits can no longer be extended through posted values. `POST /api/configs` uses the same guarded setup workflow instead of creating an unattached configuration.

## Verification boundaries

The isolated MySQL suite includes identity/membership, session toggle/header, admin versus member access, paging/search, global/foreign roles, last-admin protection, sensitive-field overposting, safe response schemas, invitation/activation expiry and replay, and company-setup call contracts. Existing fixtures now seed actual active users and issue signed user-ID claims; the production membership check is not bypassed for tests.

`scripts/Test-Phase3MySql.ps1` runs the complete suite. Its optional `-Filter AdministrationTests` selects the focused administration suite.

October 1, 2026: the Release solution build succeeded and all 403 compatibility tests passed against isolated MySQL on port 33316, with zero failures or skips. The focused administration suite also passed all six tests. The four pre-existing warnings in DiscountTypes, TaxRates, ReceivableReports and PayableReports remain on compilation. Connection strings and the temporary multiple-web-session setting were not changed.

Read-only local discovery confirmed both setup procedures exist, but the configured account could not read their definitions (`ROUTINE_DEFINITION` was NULL). Setup tests therefore use isolated contract stubs; they do not prove the production procedures' internal cloning/accounting behavior. No development data was modified. Restart the API, refresh/sign in both clients, and verify team maintenance, role changes, company settings and onboarding against the actual procedures before deployment.

This is a single-company membership model based on the existing `User.ConfigId`; it does not introduce multi-company memberships or a platform-superadmin bypass. Other controllers' business-specific permissions and child-reference validation remain separate refactoring work.
