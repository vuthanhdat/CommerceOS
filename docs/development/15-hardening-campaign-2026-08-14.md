# F14 hardening campaign — 2026-08-14

## Scope and evidence

The campaign used deterministic unit, architecture and CDK suites with synthetic tenant IDs. `dotnet test CommerceOS.slnx --no-restore` is the fast evidence command. LocalStack-specific queue resource topology is asserted by `CommerceOS.Cdk.Tests`; emulator API behavior is not represented as AWS-equivalence.

| Failure family | Evidence | Result |
|---|---|---|
| Tenant A/B known-ID access | Tenancy, Audit and Notification unit tests use trusted context and return only tenant-keyed records | Pass |
| Client tenant/role spoofing | Storefront resolves public tenant; Sales/Payments command contracts distinguish trusted tenant values; merchant mutation services accept trusted contexts | Pass |
| Suspension, role and last-owner policy | `CommerceOS.Tenancy.UnitTests` lifecycle, authority and membership scenarios | Pass |
| Final-unit/concurrent inventory | Warehouse/stock operation tests and conditional DynamoDB store contract | Pass |
| Duplicate facts/callbacks | Inventory receipt/return, Payments capture/refund, Accounting source journal, Notification source-recipient tests | Pass |
| Provider timeout/reconciliation | Payment capture and refund `OutcomeUnknown` tests | Pass |
| Accounting duplicate/unbalanced posting | Accounting journal and refund-correction tests | Pass |
| DLQ/replay task isolation | CDK queue tests plus `recovery-inspect` / bounded `recovery-redrive` command checks | Pass |

## Regression findings

1. Refund operation state needed its own Payments-owned persistence record rather than retrying an unsafe provider call. The resulting durable ledger and reconciliation tests were added.
2. A notification read flag must include recipient identity in the persistence key. The resulting source-plus-recipient dedupe guard is covered by unit and DynamoDB adapter tests.

## LocalStack limitation

`start-message-move-task` can be unavailable or edition-dependent in the pinned LocalStack image. The recovery command surfaces the provider response; logical replay remains safe through the consumer source identity. It is not evidence of AWS control-plane fidelity.
