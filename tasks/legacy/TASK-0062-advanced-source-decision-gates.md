# TASK-0062 — Decide advanced source integrations and discovery crawling

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 14
Milestone: Milestone D
Depends on: TASK-0061
Execution gate: Amazon and discovery work proceed only through separately accepted policy/license decisions.

## Goal

CommerceOS has explicit accepted/rejected decisions for Amazon Creators API integration and controlled discovery crawling, with any approved implementation decomposed into new scoped tasks.

## Business context

Amazon licensing/account eligibility and discovery crawl policy/cost can change and must not be assumed from the early roadmap.

## In scope

- re-verify current Amazon Creators API account/license/content-use requirements and evaluate project eligibility/credentials/cost;
- evaluate whether controlled discovery/category ingestion remains useful and permitted for any source, including pagination, dedup, load, policy, and kill controls;
- write separate decision records with alternatives, security/reliability/cost/reversibility, then create implementation tasks only for accepted options;

## Out of scope

- implementing Amazon or discovery in this decision task;
- HTML scraping Amazon, bypassing access controls, or enabling uncontrolled category crawling;

## Acceptance criteria

### AC01 — Amazon decision

Given current official program documentation and project eligibility are verified
when the review completes
then an accepted/rejected decision records permitted fields/use, credentials, attribution, retention, cost, and next tasks.

### AC02 — Discovery decision

Given source policy and measured refresh operations are reviewed
when discovery is evaluated
then an accepted/rejected decision defines allowed sources/scope/rates/pagination/dedup/cost and why value exceeds risk.

### AC03 — No premature integration

Given no accepted decision/task exists
when repository/deployments are inspected
then no Amazon/discovery credentials, schedules, or crawl behavior are introduced.

### AC04 — Actionable output

Given an option is accepted
when decision closes
then one or more independently implementable task specs/ADR updates are created with gates and verification.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud verification is explicitly N/A unless AWS semantics changed.

## Architecture impact

- Owning domain: Product Data Ingestion / Architecture
- Domains touched: Ingestion, Legal/policy review, Cost governance, Security
- Persistence impact: Decision only; proposed data/retention implications are documented.
- Events/contracts impact: Decision defines any future adapter/discovery contracts; no event implemented.
- AWS/IaC impact: No new resources; evaluate Scheduler/SQS/Lambda/API/secret costs using current pricing.
- ADR required? Yes for any accepted official external integration or discovery topology with material contract/cost impact.

## Security and tenant impact

- Authentication: Evaluate credential storage/rotation and account eligibility; do not acquire/store credentials without human setup.
- Authorization: Evaluate licensing, SSRF/source restrictions, secret access, abuse, and data reuse.
- Tenant scoping: Define whether integration is shared/platform or tenant-authorized and how usage is scoped.
- Sensitive data/secrets: Never expose credentials; permitted content/attribution/retention must be explicit.
- Abuse/rate-limit considerations: Discovery must have strict depth/page/target/rate/concurrency/budget/kill limits if accepted.

## Reliability and idempotency impact

- Retry behavior: Decision specifies safe API/rate retry behavior; policy denial/CAPTCHA never retries as bypass.
- Timeout semantics: Decision specifies continuation/checkpoint and partial discovery semantics.
- Duplicate-delivery behavior: Decision specifies URL/product dedup and repeated schedule behavior.
- Idempotency key/strategy: Proposed source/target/window/page identity.
- DLQ/recovery/reconciliation: Decision includes pause/kill/resume and cost/source incident response.

## Observability impact

- Logs: Structured logs include safe tenant/source/entity/operation/event and correlation data.
- Metrics: Track outcomes, failures, retries, duplicates, lag/age, recovery, and latency.
- Traces/correlation: Preserve correlation/causation across changed domains and providers.
- Operational states/errors: Required usage/quota/policy/parser/discovery metrics and alarms are defined.

## Cost impact

- Request/compute impact: Research/decision only; estimate learning and beta usage before acceptance.
- Storage impact: Decision only; proposed data/retention implications are documented.
- Network impact: Only approved bounded external/internal traffic.
- New AWS resources/services: No new resources; evaluate Scheduler/SQS/Lambda/API/secret costs using current pricing.
- Free Tier allowance relevant to this task: Use accepted serverless allowances, disabled/low non-prod schedules, and bounded concurrency.
- Expected monthly cost change or `negligible` with rationale: no runtime change; current official pricing/allowances and expected request/storage volume are documented.
- Estimated one-off cloud-test/load-test cost, if any: None expected.

## Test plan

- Unit: N/A — use existing adapter/refresh measurements as evidence.
- Integration: N/A — no live integration without accepted gate.
- Architecture: Enforce domain ownership, tenant isolation, event/idempotency, and no persistence shortcuts.
- Contract: Proposed API/discovery normalized output and policy contracts documented.
- IaC: N/A unless infrastructure changes.
- E2E/manual: Review decision artifacts and generated follow-up tasks.
- **Cloud verification required?** No — this is a decision gate; any optional API eligibility probe needs explicit human credentials/approval.
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

