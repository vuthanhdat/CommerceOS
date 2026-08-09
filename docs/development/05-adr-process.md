# CommerceOS — Architecture Decision Record Process

## 1. Purpose

Architecture decisions must remain discoverable after the conversation that produced them is gone.

Use ADRs for decisions that materially constrain future implementation or operational behavior.

ADRs live in `docs/adr/` and use `docs/adr/ADR-000-template.md`.

---

## 2. ADR required when

Create or update an ADR when introducing/changing:

- an AWS managed service with material architectural or cost impact;
- persistence technology or major access model;
- cross-domain integration mechanism;
- event bus/queue/workflow topology;
- externally consumed API/event contract strategy;
- tenant-isolation strategy;
- authentication/authorization architecture;
- accounting integrity/posting model;
- payment consistency/reconciliation model;
- meaningful availability/reliability trade-off;
- deployment boundary/service extraction;
- standing-cost infrastructure.

Small implementation details do not need ADRs.

---

## 3. ADR lifecycle

Statuses:

- `Proposed`
- `Accepted`
- `Superseded`
- `Rejected`

Do not silently rewrite the historical reason for an accepted decision. If the architecture changes, create a new ADR and mark the old one superseded.

---

## 4. Required analysis

An ADR should answer:

### Context

What problem/pressure exists now?

### Decision

What are we choosing?

### Alternatives

What credible options were considered?

### Consequences

What becomes easier/harder?

### Security / tenant impact

Could this affect isolation, authorization, data exposure, or secrets?

### Reliability impact

How do failures, retries, recovery, and observability change?

### Cost impact

For AWS/resource decisions, estimate the expected monthly effect for at least the Learning/Beta profile when material. Link/update `docs/04-cost-model.md` when appropriate.

### Reversibility

How difficult is rollback/migration?

### Validation

What evidence/tests/measurements will tell us the decision is working?

---

## 5. Agent rule

An agent must not create an architectural dependency merely because it simplifies the current task.

If the task appears to require an ADR but none exists:

1. identify the architectural decision explicitly;
2. create/propose the ADR as part of task scope when allowed;
3. avoid hiding the decision inside implementation code;
4. update cost/architecture documentation when required.

If the task explicitly excludes architecture change, do not smuggle one in; surface it as a blocker/follow-up design task instead.
