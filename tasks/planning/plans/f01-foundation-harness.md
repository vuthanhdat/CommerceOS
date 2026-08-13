# F01 — LocalStack Foundation & Harness

## Feature goal
Prove and harden a deterministic repository-owned LocalStack foundation before business modules depend on it.

## Source requirements
REQ-FND-001..004, REQ-HARD-002..003.

## Scope
- verify existing `tools/commerceos.py` lifecycle against current code and ADR-012;
- deterministic start/readiness/synth/bootstrap/deploy/smoke/inspect/reset/redeploy/destroy;
- isolated task-instance configuration;
- CI-selected LocalStack verification;
- architecture tests preventing LocalStack/AWS SDK leakage into Domain/Application.

## Out of scope
Real AWS accounts, IAM/OIDC, Budgets, cloud staging, speculative business resources.

## Architecture
CDK remains IaC source; LocalStack runtime details remain Infrastructure/Delivery configuration. Foundation is technical only and owns no merchant business truth.

## Task sequence
TASK-0100 -> {TASK-0101, TASK-0102, TASK-0103}.

## Progress

All F01 tasks are complete. The launcher and foundation CDK tests are isolated
by task instance, the `0094` lifecycle/reset/redeploy evidence proves clean
LocalStack bootstrap for the current CloudFormation/Logs foundation, the harness
executes the Application configuration-boundary guardrails, and selected
LocalStack verification is now represented in CI through isolated instance
`0077`.

## Definition of Done
A clean checkout can run the documented lifecycle and repository checks deterministically; supported behavior and emulator limitations are explicit.
