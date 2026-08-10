# TASK-0073 — Prove backup, recovery, and production delivery readiness

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 16
Milestone: Milestone E
Depends on: TASK-0069, TASK-0070, TASK-0071

## Goal

Critical CommerceOS data can be backed up and restored to an isolated environment within project RPO/RTO targets, and production delivery has protected approval, rollback, and stateful-change safeguards.

## Business context

Production-minded readiness requires recovery proof and safe promotion, not only backup toggles or successful deploys.

## In scope

- classify critical/stateful data and implement production-like DynamoDB PITR/backup and S3 versioning/retention/protection profiles with cost estimates;
- perform isolated restore/rebuild/reconciliation drill targeting RPO <=15 minutes and RTO <=60 minutes for critical data;
- implement protected production delivery readiness: separate role/account recommendation, manual approval, immutable artifact promotion, CDK diff/replacement guard, rollback/migration/post-deploy health;

## Out of scope

- launching real production or using real customer/payment data;
- certified disaster recovery, multi-region active-active, or permanent staging without approval;

## Acceptance criteria

### AC01 — Protected data profile

Given production-like config is synthesized/deployed
when resources are inspected
then critical tables/buckets have explicit retention/PITR/version/protection and destructive replacements are surfaced.

### AC02 — Restore drill

Given synthetic critical data and known recovery point exist
when isolated restore/application reconciliation runs
then integrity is validated and measured RPO/RTO meet targets or blocking gaps are recorded.

### AC03 — Production gate

Given release artifact is promoted toward production
when pipeline evaluates it
then manual approval, separate least-privilege role, prior staging evidence, diff, backup/migration/rollback, smoke and alarm checks are mandatory.

### AC04 — Cost/teardown

Given backup/staging profile is tested
when experiment closes
then actual cost/retained backups/restores/resources are recorded and ephemeral copies removed.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud evidence, cost, and teardown/retention decisions are recorded.

## Architecture impact

- Owning domain: Platform Reliability / IaC / Delivery
- Domains touched: All critical persistence domains, CI/CD, Security, Operations
- Persistence impact: PITR/backups/versioning/restored copies for critical DynamoDB/S3 plus documented projection rebuild order.
- Events/contracts impact: Document event/inbox/outbox/reconciliation recovery ordering and replay requirements.
- AWS/IaC impact: DynamoDB PITR/backup, S3 versioning/retention, IAM, CDK protection, staging/prod workflows; no new always-on compute.
- ADR required? Yes if production account topology, backup service, retention, or deployment strategy materially changes existing decisions.

## Security and tenant impact

- Authentication: Backup/restore/production roles are separate, least-privilege, protected, and use temporary/OIDC credentials.
- Authorization: Restore data is synthetic/isolated, encrypted, access-logged as appropriate, and deleted after drill.
- Tenant scoping: Tenant-owned data and async context remain scoped by trusted identity; explicit audited platform access is the only cross-tenant path.
- Sensitive data/secrets: Secrets/PII/payment/audit data are minimized, protected, and redacted from logs/tests.
- Abuse/rate-limit considerations: Destructive/restore/production actions require confirmation/approval and exact target validation.

## Reliability and idempotency impact

- Retry behavior: Restore/deploy retry only after AWS state is inspected; reconciliation is idempotent.
- Timeout semantics: Timed-out restore/deploy is unknown until service/CloudFormation state is queried.
- Duplicate-delivery behavior: Repeated restore/replay does not duplicate business effects in the validated target.
- Idempotency key/strategy: Restore run/reconciliation source identities and immutable artifact commit.
- DLQ/recovery/reconciliation: Primary scope: documented restore order, projection rebuild, event reconciliation, rollback and escalation.

## Observability impact

- Logs: Structured, redacted logs retain safe tenant/entity/event/operation/correlation context.
- Metrics: Use built-in metrics first; measure security, saturation, errors, latency, failures, recovery, and cost at bounded cardinality.
- Traces/correlation: Verify end-to-end correlation/causation through affected journeys.
- Operational states/errors: Backup status, restore timing, integrity checks, deploy/rollback/smoke/alarm evidence are recorded.

## Cost impact

- Request/compute impact: One bounded restore/deployment rehearsal.
- Storage impact: Temporary restored data plus chosen PITR/version retention; estimate recurring production-like cost.
- Network impact: Measured and bounded; no unapproved fixed-cost network component.
- New AWS resources/services: DynamoDB PITR/backup, S3 versioning/retention, IAM, CDK protection, staging/prod workflows; no new always-on compute.
- Free Tier allowance relevant to this task: Respect the approximately USD 100 credit envelope and normal $0–$5/month target.
- Expected monthly cost change or `negligible` with rationale: may be material relative to learning baseline; update cost model and disable/destroy test copies.
- Estimated one-off cloud-test/load-test cost, if any: Estimate storage/restore/staging cost before drill and record actual.

## Test plan

- Unit: Environment/retention/protection configuration and integrity validators.
- Integration: Real backup/PITR/versioning restore, IAM denial, CDK replacement guards, deployment pipeline.
- Architecture: Re-run and extend tenant/domain/event/IaC/security guardrails for discovered recurring risks.
- Contract: Recovery inventory/runbook and artifact promotion evidence contract.
- IaC: CDK assertions/synth/diff plus real AWS policy/resource verification.
- E2E/manual: Backup, simulate loss in isolated staging, restore/reconcile/validate, then destroy restored environment.
- **Cloud verification required?** Yes — backup/PITR/S3/versioning/IAM/CloudFormation and deployment gates are AWS semantics.
- AWS environment/stack(s) required: isolated ephemeral staging plus protected delivery roles/config
- Preview/staging teardown plan: Destroy restored/staging copies, retain only intentional backup policy/evidence, verify no production deploy occurred.

