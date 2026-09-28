# Phase 5: development database verification

Status: read-only verification implemented; full business regression blocked. Verified on September 28, 2026 against the configured localhost MySQL 8.0.46 database. A separate database is not required for these checks. No database rows or schema were changed, and no backup was needed for this read-only run.

## Results

| Check | Result |
| --- | --- |
| Existing database | 67 tables; 279,027 journal entries |
| EF materialization | Config, user, customer, invoice, bill and journal reads passed |
| Customer controller | 4,881 active customers for the selected tenant; count matches independent SQL |
| Journal controller | Four rows for June 24–30, 2026; debit and credit each 102,750.1700; matches independent SQL |
| .NET 6 vs .NET 10 | Both selected controller results match after row ordering and numeric normalization |
| Inventory materialization | Failed: MySQL 1146, missing mapped view |
| Required procedures | All 26 definition checks returned MySQL 1305; no routines visible |
| Account grants | No EXECUTE or SHOW_ROUTINE grant found in returned grant text; effective privileges need administrator verification |

Comparison used historical commit `ba95f06c6fad0f51072a185eac234fe6f4743e54` on .NET 6.0.36 and current source on .NET 10.0.7. Customer serialized fingerprints match. Journal debit/credit serialized fingerprints differ, but normalized numeric values match; this is not a byte-for-byte response compatibility claim.

Eight mapped schema gaps were found:

- Tables: `arpaymentdetail`, `chatmessage`, `chatsession`.
- Columns: `debtor.SLType`, `debtortype.SLType`.
- Views: `inventorytransaction`, `salestransaction`, `vcitymunicipality`.

The database also has 13 future-dated journal rows. The sampled period excludes future dates; no data correction was attempted.

Evidence: [current readiness](database-readiness.json), [.NET 6 readiness](net6-readiness.json), and [runtime comparison](net6-comparison.json). These snapshots contain counts, metadata and response hashes, without credentials or raw business records.

## Repeat verification

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-DevelopmentDatabase.ps1 -CompareNet6
```

Omit `-CompareNet6` to check only the current application. The comparison requires SDK 6.0.428 and its runtime plus the historical commit in local Git history. The current verifier uses the SDK selected by `global.json`. Database configuration comes from local appsettings, the selected environment settings, and environment variables. The host must be localhost, 127.0.0.1 or ::1.

The verifier sets the MySQL session to read-only and executes metadata and SELECT queries. It never calls stored procedures or writes data. Reports are generated under ignored `bin/phase5-verification`; the historical build is under ignored `bin/phase5-net6`. Exit 2 means database readiness is blocked; build or comparison failures are reported separately. Missing samples also prevent readiness. Normal application builds and tests do not execute this opt-in verifier.

Controller methods are invoked directly, so these checks do not exercise HTTP middleware, authentication or ownership enforcement. Comparisons run sequentially; concurrent development changes can produce differences. Normalization sorts root rows by ID and object property names and removes decimal trailing-zero differences. The saved readiness reports retain raw fingerprints. These limited comparisons do not prove posting, rollback, stock, tax, stored-procedure or complete financial compatibility.

## Remaining work

1. Obtain the authoritative current V1 schema export or migrations, including procedures and views. Available historical 2022 dumps do not supply the complete required definitions. Exporting this incomplete localhost schema alone cannot recover them.
2. Review and apply the missing definitions and columns from that source, with a backup before changes. Have the database administrator verify routine existence and the application's required execution privileges.
3. Rerun readiness and baseline comparisons.
4. Before write-based regression, back up the development database and stop concurrent use. Exercise representative posting, reversal, rollback and inventory workflows with controlled fixtures and cleanup. Use a separate test schema if the development database must remain in use or retain its data untouched.

No replacement business routines, permission changes, guessed migrations or write tests were applied while these prerequisites remain unresolved.
