# CommerceOS — Testing Strategy

## 1. Principle

Testing is part of the development harness, not a cleanup step after implementation.

Use the cheapest test that can reliably detect the failure class, while ensuring critical boundaries are exercised end-to-end where unit tests are insufficient.

---

## 2. Test layers

### Unit tests

Use for:

- domain invariants;
- value objects;
- pricing/calculation rules;
- journal balancing;
- inventory state transitions;
- order state transitions;
- pure mapping/normalization logic.

Unit tests should not require AWS or network access.

### Integration tests

Use for:

- DynamoDB access patterns and conditional writes;
- tenant scoping;
- API authorization boundaries;
- event serialization/deserialization;
- SQS duplicate/redelivery behavior;
- Mock Payment API behavior;
- webhook deduplication;
- reconciliation flows.

Prefer reproducible local/container/emulator or isolated AWS test environments depending on the service and fidelity required. The chosen method must be documented rather than hidden in developer setup.

### Architecture tests

Use for structural rules that humans/agents repeatedly risk violating:

- dependency direction;
- domain isolation;
- forbidden framework/AWS dependencies in domain assemblies;
- cross-domain reference constraints;
- naming/event-contract conventions where valuable.

### Contract tests

Use for boundaries whose compatibility matters independently:

- Mock Payment Provider API/webhook contract;
- domain-event schemas;
- public API contracts;
- crawler source-adapter normalized output.

### Infrastructure tests

Use for:

- CDK synth;
- expected resources;
- IAM constraints;
- encryption/public-access defaults;
- log retention;
- queue DLQ/redrive policy;
- reserved/max concurrency/throughput guardrails where configured.

### End-to-end tests

Use sparingly for high-value business journeys:

```text
merchant onboarding
→ create product
→ publish
→ storefront purchase
→ inventory reservation
→ mock payment
→ order confirmation
```

Later add failure-oriented E2E journeys rather than only happy paths.

---

## 3. Mandatory scenario families

### Multi-tenant scenarios

For tenant-owned APIs:

- Tenant A can access its own object;
- Tenant B cannot access Tenant A object even with a known ID;
- request-body/query `tenantId` cannot override authenticated tenant context;
- privileged platform roles, if introduced, use explicit separate authorization.

### Event/queue scenarios

For side-effecting consumers:

- same event delivered twice;
- event delivered after temporary processing failure;
- poison message behavior;
- out-of-order event behavior when order matters;
- old event version behavior when compatibility matters.

### Payment scenarios

- success;
- decline;
- HTTP 500 then retry;
- timeout before provider commit;
- timeout after provider commit;
- delayed success;
- duplicate webhook;
- webhook retry;
- refund;
- same idempotency key repeated.

### Accounting scenarios

- balanced journal accepted;
- unbalanced journal rejected;
- posted journal mutation rejected;
- reversal creates auditable correction path;
- duplicate source event does not duplicate logical posting.

### Inventory scenarios

- reservation with sufficient stock;
- reservation with insufficient stock;
- concurrent reservation of final units;
- release after payment/order failure;
- issue/fulfillment movement auditability.

---

## 4. Regression rule

For meaningful defects, add a regression test at the lowest reliable layer that reproduces the failure.

Then ask whether the same class of defect deserves a broader harness guardrail.

Example:

```text
Bug: Tenant B can fetch Tenant A product
   ↓
Fix API/repository
   ↓
Add cross-tenant integration regression
   ↓
Improve reusable tenant test fixture / repository contract
```

---

## 5. Verification command evolution

H0 begins with:

```bash
python3 scripts/harness_check.py
```

As implementation arrives, the command should orchestrate stable project commands such as:

```text
restore/install
format/lint
build/typecheck
unit tests
architecture tests
integration tests
CDK synth/IaC checks
security/static checks
repository/document checks
```

The exact stack commands should be added once Phase 0 fixes the concrete solution/toolchain structure.

Agents and CI should call the same verification entry point whenever practical to prevent "works locally but CI runs something different" drift.

---

## 6. Test-quality rules

- Test observable behavior/invariants rather than implementation trivia.
- Do not weaken assertions just to make a refactor pass.
- Avoid tests that depend on wall-clock sleeps when deterministic clocks/events can be injected.
- Avoid nondeterministic random payment fixtures unless the seed/scenario is explicit.
- Preserve useful failure messages; a harness is stronger when an agent can understand why it failed.
- Keep failure-injection scenarios first-class as distributed architecture grows.
