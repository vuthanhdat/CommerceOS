# CommerceOS Agent Instructions

CommerceOS is developed through direct collaboration between the human and the AI in the current
conversation. Do not require a separate Planner, Builder, Reviewer, or Orchestrator workflow unless
the human explicitly asks for one.

## Before changing code

Read the relevant parts of:

1. `README.md`;
2. the files named by the human and the nearest existing implementation/tests;
3. relevant product and domain documents under `docs/`;
4. `docs/development/03-architecture-rules.md`;
5. relevant ADRs under `docs/adr/`, especially ADR-012 for runtime/infrastructure work;
6. domain-local `AGENTS.md` files, if present.

Existing task files may be used as planning context, but they do not require an automated task
lifecycle or a separate agent handoff. When requirements are materially ambiguous, explain the
decision needed to the human instead of inventing product or architecture behavior.

## Core product invariants

### Multi-tenancy

- Scope tenant-owned data through trusted tenant context.
- Never trust a client-supplied `tenantId` for authorization.
- Deny cross-tenant access by design and cover it with tests.

### Domain boundaries

- A domain owns its business rules and persistence model.
- Do not read or write another domain's persistence as an integration shortcut.
- Use explicit application contracts, commands, queries, or domain/integration events.
- Domain code must not depend on AWS SDKs, LocalStack packages, HTTP frameworks, or persistence
  implementations.
- Application code must not depend on LocalStack-specific endpoints or credentials.

### Accounting

- Posted journals are immutable; corrections use reversal/correction entries.
- Every posted journal balances.
- Event-driven accounting consumers are idempotent and traceable to their source fact.

### Inventory

- Inventory invariants must be concurrency-safe.
- `Available = OnHand - Reserved` unless a later ADR changes the model.
- Do not rely on unprotected read-then-write logic for stock correctness.

### Payments

- Use only the internal Mock Payment Provider until an explicit later decision changes this.
- Retries require idempotency.
- Timeout does not imply failure; ambiguous outcomes require query/reconciliation.
- Never store real card data or secrets in fixtures.

### Events and async consumers

- Consumers assume at-least-once delivery and are idempotent when effects can repeat.
- Integration events carry `eventId`, `eventType`, `eventVersion`, `tenantId` when applicable,
  `aggregateId`, `occurredAt`, `correlationId`, and `causationId` when applicable.
- Prefer meaningful business facts over vague technical events.

### Product-data ingestion

- External snapshots are not the merchant canonical catalog.
- Adapters respect source policy, robots/terms review, rate limits, kill switches, and parser
  fixtures. Prefer official APIs when available and permitted.

### Infrastructure runtime

- ADR-012 makes LocalStack the only infrastructure/runtime target for development and validation.
- Do not require or provision a real AWS account unless a later accepted ADR supersedes ADR-012.
- Keep endpoints, synthetic credentials, regions, ports, prefixes, reset policy, and edition
  switches in configuration.
- Document unsupported or behaviorally different LocalStack features explicitly.

## Working method

For implementation requests:

1. Inspect the repository and state any important assumption.
2. Make the requested change directly in the current workspace.
3. Preserve unrelated user changes.
4. Add or update relevant tests.
5. Run focused verification and `python scripts/harness_check.py` when proportionate.
6. Report what changed, verification results, architecture/security implications, and remaining
   follow-up work.

Use a task specification or ADR when it genuinely improves clarity or records a material decision;
do not create process artifacts merely to route ordinary work through multiple agents.

## Definition of done

- The requested behavior is implemented.
- Relevant tests pass.
- Tenant/security and domain boundaries remain intact.
- Failure, retry, idempotency, and LocalStack implications are considered where relevant.
- Documentation or ADRs are updated when the behavior or architecture changed.
- No failing guardrail is bypassed merely to obtain a green result.
