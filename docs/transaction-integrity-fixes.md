# Transaction integrity fixes — 2026-10-05

API V2 now protects the reviewed sales, purchasing, payment, journal and inventory write paths with a common transaction boundary. Existing V1 URLs, DTO fields, deleted-child flags, statuses and source codes remain available. No database schema change is introduced by these fixes.

## Write behavior

- `TransactionIntegrityFilter` runs after authenticated company validation. It enforces module/action permissions, checks old/new responsibility-center access, and holds the company's `config` row lock through validation, persistence, derived updates and commit.
- PUT compares the `lastUpdatedDate` already returned by the detail endpoint. Stale writes return 409 with a reload message. Clients must retain that field and re-fetch after writes. Server revisions use millisecond precision for legacy browser date serialization.
- `TransactionApplicationService` compares persisted old posted links with final new posted links. V1 delete-old/add-new, in-place allocation edits, generic payment deletion, and draft/post transitions use the same rules. Posted effects alone change AR/AP balances. Target ownership, party, trade account, nature, posting state, scope and available amount are checked. An old link can be reversed after the configured account changes.
- Cash applied and AR/AP settled are distinct when V1's `paymentAdjustmentEntry` is present. Adjustment metadata must have matching journals; a null header balance cannot bypass payment-total bounds.
- Final posted journals must balance. Financial validation includes retained children omitted from legacy partial updates. Journal/detail status must agree with the header. A failure after the controller's save still rolls back the enclosing transaction.
- Existing applied balances survive invoice/bill edits. Removing an applied journal or deleting its parent is rejected until its dependencies are reversed.
- `BillInventorySnapshot` aggregates old/new posted movements per item. Repeated purchase lines, item replacement and deletion no longer overwrite each other's weighted-average calculations. Backdated purchases keep the existing latest-purchase-cost rule.
- Stock Transfer preserves the requested document date. Charge/Cash Invoice numbering commits with the document under the same company lock.
- Legacy Expense Payment reads/writes require consistent company ownership across journals and available header references. Raw legacy navigation objects cannot create/update nested master records.

The company lock intentionally favors correctness over parallel writes within one company. Measure contention before replacing it with narrower locks. Sales Return retains its existing versioned service and shares the company-lock protocol.

## Verification

Run `./scripts/Test-Phase3MySql.ps1`. It permits only the existing `bin/phase1-mysql` sandbox at `127.0.0.1:33316`, creates GUID-named fixture databases, and shuts the sandbox down afterward. Never run these fixtures against the configured application database.

`IntegrityReviewProbes` and `TransactionGuardTests` cover the reproduced failures, rollback injection, concurrent applications/purchases/number allocation, duplicate payment retries, V1 adjustments, scope/ownership, and valid legacy CRUD. Existing success fixtures now contain balanced journals and explicit authorized roles.

## Deployment boundary

These changes do not repair historical balances or implement full server recalculation of every document's taxes/discounts, universal request-key idempotency, closed periods, negative-stock policy, or revision/reversal audit history. Reconcile a restored production copy, inspect installed schema/routines, and accept representative V1/V2 workflows before replacing production API V1. The running API has not been restarted or deployed by this implementation.
