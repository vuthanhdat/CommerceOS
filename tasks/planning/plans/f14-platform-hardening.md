# F14 — Platform Hardening & Architecture Audit

## Feature goal
Turn distributed failures, tenant/security risks and architecture rules into diagnosable behavior and reusable mechanical guardrails.

## Source requirements
REQ-OBS-001, REQ-HARD-001..003, REQ-FND-004, REQ-SEC-001..003.

## Scope
structured logs/correlation/metrics; DLQ/redrive/recovery tooling; tenant-isolation/authorization campaign; deterministic failure/load experiments sized for local/CI resources; harness/architecture checks; milestone architecture audit and evidence-based extraction assessment.

## Out of scope
Claiming AWS production performance/IAM/quota fidelity; microservice extraction merely for style; bypassing valid checks.

## Architecture
LocalStack proves only exercised capability contracts. Harness improvements target recurring/high-impact failure classes.

## Task sequence
TASK-0230 -> TASK-0231 -> TASK-0232 -> TASK-0233 -> TASK-0234.

## Definition of Done
Critical async failures are actionable; cross-Tenant and retry/idempotency failure campaigns pass; recurring architecture rules are executable where practical; extraction recommendations are evidence-based.
