# Phase 5: completed

Closed September 28, 2026 on the basis of passing automated comparisons and user acceptance of local transaction testing. The localhost database contains all 26 required procedures. Presence and definition visibility are separate fields: hidden definitions do not mean a procedure is missing. EXECUTE is present in the returned grants; the report calls also exercise execution access.

The user accepted four schema gaps as absent in production: `arpaymentdetail`, `debtor.SLType`, `debtortype.SLType`, and `vcitymunicipality`. They remain visible in evidence but do not block readiness. Other gaps still block. This acceptance does not prove those application paths are unused.

## Run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-DevelopmentDatabase.ps1 -CompareNet6
```

Requires the current SDK, SDK 6.0.428/runtime 6, and historical commit `ba95f06c6fad0f51072a185eac234fe6f4743e54`. Configuration comes from local appsettings and environment overrides; only localhost targets are allowed. Reports are written under ignored `bin/phase5-verification`.

Seven fixed GET controller paths cover sales, purchases, receivables, payables, inventory, trial balance and general ledger. The sample uses the first configured tenant with journals, January 1 through its latest non-future posted journal date, and its configured AR/AP trade accounts. Missing, empty or failed samples prevent acceptance. Each report runs in a transaction on a session configured read-only and rolls back afterward. Creation, posting and view-generation procedures are excluded.

Both runtimes run the same verifier. Each report result is compared as a multiset of normalized row hashes, preserving duplicate rows while ignoring root row order, object key order and decimal trailing zeros. Nested array order remains significant. Counts and hashes are persisted; business rows and credentials are not. Customer/journal checks additionally compare counts and journal totals with independent SQL. The seven added report checks establish baseline parity, not independent accounting correctness.

## Evidence and limits

See [current readiness](database-readiness.json), [.NET 6 readiness](net6-readiness.json), and [comparison](net6-comparison.json). The baseline is .NET 6.0.36 and the current local runtime is .NET 10.0.7.

The controllers are called directly, so HTTP authentication, filters and tenant ownership enforcement are outside this test. Runs are sequential against shared development data; concurrent changes can cause mismatches. Procedure source remains hidden, and not all 26 procedures or filter combinations are exercised. Automated readiness alone is not full business acceptance; phase closure also relies on the user testing confirmation recorded below.

## Completion record

- User confirmed the local database backup was completed, sales/payment posting worked, the stored-procedure fix was tested, and subsequent testing looked satisfactory; requested Phase 5 closure.
- Final automated comparison passed: all 26 required procedures present, no unaccepted schema gaps, customer/journal SQL checks passed, and seven populated report samples matched .NET 6 and .NET 10.
- Targeted live check of `GetSalesByCustomer(16, '2026-09-01', '2026-09-30', '', '')` returned four customer rows. NULL array input also returned four rows. The underlying posted sample contained 24 transactions. Literal `[]` remains unsupported (MySQL 1064); the procedure accepts comma-separated IDs rather than a JSON-array literal.
- The reference [procedure definition](../../Db/procedures/GetSalesByCustomer.sql) records the user-supplied body with the confirmed optional-filter correction. It is not an export of the hidden installed body and was not applied by the agent. Preserve the target definer/grants when deploying it.
- Detailed manual case results and document IDs were not supplied. Forced-failure rollback and every cancellation/inventory scenario have not been independently verified by the agent. No new write workflows were executed during closure.

Next phase: staging deployment and acceptance. Confirm environment configuration, run authenticated client smoke tests, include the procedure correction in the database deployment review, and verify backup/rollback readiness before production rollout. Production deployment is not part of Phase 5 closure.
