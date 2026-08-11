# CommerceOS — Task Specification

## 1. Why every non-trivial change needs a task

A task is the repository contract between product intent and implementation. Use `tasks/TASK-TEMPLATE.md`.

A task should represent one logical scope. If implementation discovers unrelated work, record a follow-up task rather than silently expanding scope.

## 2. Required task content

### Goal and business context

State one observable outcome and the domain/business rules that matter.

### Scope and acceptance criteria

Define explicit in-scope/out-of-scope boundaries and deterministic acceptance criteria that can become tests where practical.

### Architecture impact

State whether the task changes module/domain boundaries, events, persistence, contracts, workflows, or infrastructure capabilities/mappings.

Architecture should name the required capability before an AWS-style service mapping. Under ADR-012, infrastructure mappings target LocalStack only.

### Security and tenant impact

Describe identity evidence, authorization, trusted Tenant scoping, sensitive-data handling, and abuse concerns.

### Reliability/idempotency impact

Required for async workflows, external calls, payments, queues, events, retries, or other distributed operations.

### Observability impact

State required logs, metrics, traces, correlation IDs, and operational states.

### Local runtime/resource impact

For infrastructure-sensitive work, state:

- LocalStack capabilities/services involved;
- endpoint/region/account-placeholder/port/resource-prefix configuration impact;
- version/edition/feature assumptions;
- bootstrap/reset/cleanup behavior;
- material local/CI CPU, memory, disk, or runtime impact;
- known emulator limitations or AWS-behavior gaps.

Do not add AWS account, IAM/OIDC, Budget/Free Tier, cloud authorization, or real-cloud preview/staging gates under ADR-012.

### Test plan

List unit, integration, architecture, contract, IaC, LocalStack, E2E/manual, failure, and reset/redeploy verification expected as applicable.

When LocalStack cannot sufficiently reproduce a capability, identify the nearest reliable project-owned layer that will be tested and record the limitation explicitly.

## 3. Acceptance-criteria quality

Prefer criteria that a machine can verify.

Example:

```text
AC01
Given a user authenticated for Tenant A
when they create a product
then the persisted product is scoped to Tenant A.

AC02
A tenantId supplied by the client cannot override trusted Tenant context.

AC03
Tenant B cannot retrieve Tenant A's product by guessing its productId.

AC04
Blank product name returns a validation error.

AC05
Repository verification passes.
```

Avoid vague statements such as `works correctly`, `secure`, `good performance`, or `follows best practices` unless converted into measurable behavior.

## 4. Task completion record

Before moving a task to `completed`, record:

- implementation summary;
- acceptance-criteria result;
- repository/local verification result;
- LocalStack verification/reset evidence when required;
- known emulator limitations;
- important architecture/security/runtime decisions;
- follow-up tasks;
- harness improvement, if any.
