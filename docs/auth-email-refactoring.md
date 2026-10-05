# Authentication and email refactoring

`AuthController` and `EmailController` now contain route binding only. Authentication/session operations live in `AuthService`, JWT creation in `AuthTokenService`, persisted-password compatibility and new-password validation in `PasswordSecurity`. Email workflows, HTML templates and SMTP transport live in `EmailWorkflowService`, `EmailTemplateService` and `EmailService`. The misspelled `AuthControlle.cs` filename has been replaced by `AuthController.cs`.

Dedicated request/response DTOs are in `Contracts/Auth` and `Contracts/Email`. The broad `Credentials`/`EmailPayload` request types and controller-owned hashing/template helpers are removed. User registration reads the same stored email JSON through `EmailWorkflowData` and uses the shared password helper. Persistence entities are no longer auth/email response contracts.

## Retained routes and behavior

| Route | Behavior |
| --- | --- |
| `POST /api/auth/sign-in` | Username or email/password; returns `user`, `accessToken`, `refreshToken`, `tokenType`, `userLog`, `appVersion` |
| `POST /api/auth/refresh-access-token` | Existing opaque refresh token; returns the same session fields except `userLog` |
| `POST /api/auth/change-password` | Active signed user; matching email, current password and existing password-strength rules |
| `POST /api/auth/reset-password` | Email/new password plus a verified reset-link identifier |
| `GET /api/auth/app-version` | Existing application-version fields |
| `GET /api/auth/metabase-token?userConfigId=...` | Token for the authenticated user's company only |
| `POST /api/email` | Authenticated active user; explicit recipients/subject/message |
| `POST /api/email/member-invite` | Current company admin; company/role checks and invitation email |
| `POST /api/email/email-confirmation` | Public registration confirmation workflow |
| `POST /api/email/password-reset` | Public reset-link request for an active account |
| `GET /api/email/log/{uuid}` | Public capability-based lookup for an open, unexpired link; sanitized data |

Sign-in and refresh retain the signed `negosuite_user_id` claim, 30-minute access-token lifetime and two-day refresh lifetime. Refresh keeps the submitted token, preserving V1/V2 behavior. Newly issued refresh tokens use 32 cryptographically random bytes. Missing refresh-token requests retain their existing problem-details response. Revoked, expired or null-expiry refresh sessions are rejected; inactive users cannot sign in or refresh. Missing app-version data no longer crashes sign-in: its metadata fields are null.

The existing SHA-256 password storage format is retained so current accounts and pending activations remain compatible. No password-storage migration is introduced. The password-change actor checks and strength policy already present in the working tree are preserved. A successful password change/reset revokes the user's refresh sessions. Existing access JWTs are not centrally revoked and remain subject to their normal expiry and endpoint session checks.

New registration, reset and change-password requests use the same policy: 8-250 characters with a letter, digit and supported symbol. Sign-in remains compatible with existing passwords and does not apply the new-password policy.

`Authentication:EnforceSingleWebSession` and its temporary development override are unchanged. V1/V2 parallel web testing remains available.

## Required reset-form adjustment

The previous anonymous reset endpoint accepted an email and replacement password without verifying a link. It now requires a matching, open, unexpired `password-reset` log and consumes that log atomically. Concurrent requests using the same identifier have only one successful result. Wrong-action, wrong-email, expired and consumed links cannot reset a password.

The inspected V1 reset form currently omits the identifier. Add it to the existing payload:

```typescript
const credential = {
  email: this.emailLog.email,
  password: this.resetPasswordForm.controls['password'].value,
  identifier: this.identifier // existing URL query parameter "id"
};
```

V2 or mobile implementations of this flow must submit the same field. Requests containing only email/password now return 400. Client files were inspected as references and were not edited by this API refactor.

## Email link and delivery changes

- The server generates the actual link identifier. Caller-supplied `identifier` and `expiryDate` are accepted for request compatibility but do not control the issued capability. The generated identifier is delivered only through the intended recipient's email; issuing endpoints retain their empty successful response.
- Workflows accept exactly one recipient, preventing extra recipients from obtaining reset/activation links. Generic authenticated email allows 1-100 valid recipients and deduplicates addresses. Subject-header line breaks and malformed addresses are rejected.
- Server-owned expiries are seven days for invitations, one day for activation and two hours for reset. Email-log timestamps retain the existing server-local convention so existing consumers remain compatible. Authentication/session timestamps remain UTC.
- Invitation company and sender names come from current membership. Company IDs must match; roles must belong to that company or be shared. Activation cannot supply a company or role.
- Email logs are saved as pending (`Status=2`), activated (`1`) after successful delivery, and closed (`0`) on failure. Undelivered links cannot be used. Transport errors return a safe 503 message without SMTP diagnostics. Delivery is awaited; no background fire-and-forget work is introduced.
- `GET email/log/{uuid}` retains the `data` JSON string but whitelists its fields. Password hashes, rendered messages and operational recipient lists are omitted, including for older stored logs. Account activation continues using the stored hash server-side; the V1 client may omit its former `data.password` field when submitting activation.
- Generated HTML encodes recipient/company/sender names. Existing email layout and link destinations are retained. If `NotificationRecipients` is configured, operations receives a separate registration notification with no activation link or password.
- SMTP is asynchronous, cancellation-aware, and disposes `MailMessage`/`SmtpClient`. Existing `Smtp:*`, `AppUrl`, `NotificationRecipients` and JWT/Metabase configuration keys are retained.

No database migration is required. Reports, unused payment controllers, and unrelated concurrent work are outside this change.

## Verification

`AuthEmailTests` uses an in-memory mail transport: no real email is sent. Coverage includes session DTOs/claims, parallel web sessions, missing version data, invalid and revoked refresh tokens, password-change session revocation, server-generated reset links, concurrent single-use reset, expiry/action/email checks, sanitized activation data, invitation acceptance, HTML encoding, notification isolation, recipient validation, failed delivery and Metabase company ownership. Existing `PasswordChangeTests` retain the recent actor/password-policy regression coverage. `SwaggerTests` checks the separate request types and preserved routes.

Verified on 2026-10-05: Release build succeeded; all **479 compatibility tests passed**, with zero failures or skips. The build retains two existing report-controller warnings and the warning in the concurrently added SalesReturnTests. Generated evidence is in `bin/auth-build.log`, `bin/auth-email-full-tests.log` and `bin/phase3-verification/phase3-mysql.trx`.

Run:

```powershell
dotnet build negosuite-api.sln -c Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3MySql.ps1
```

Tests use temporary databases on isolated loopback MySQL port 33316. Live SMTP delivery, browser acceptance, and the reset-form payload update are separate from automated API verification. Restart the API before client acceptance testing.
