# TASK-0094 — Deploy the AWS dev foundation and cost guardrails

Status: Backlog
Specification maturity: Refined
Execution permission: NO
Owner: Builder — Platform Engineering
Recommended model: Default implementation model
Created: 2026-08-10
Roadmap phase: Phase 0
Depends on: completed TASK-0003, TASK-0093
Cloud verification: Required

## Goal

Preserve the useful Phase 0 outcome from legacy TASK-0004: prove that the existing cost-safe dev foundation can be deployed, inspected, destroyed, and redeployed from repository-owned CDK/configuration without hidden Console-created application infrastructure.

This task remains Refined because real cloud execution and account-specific inputs are intentionally not inferred by the Backlog Planner.

## In scope

- validate/document the selected dev AWS account and region plus CDK bootstrap/temporary-auth prerequisites;
- deploy the existing `FoundationStack` using cost-safe environment configuration;
- configure/verify AWS Budget or equivalent documented credit-spend notification guardrails at the approved thresholds/recipients;
- inspect stack resources/tags/log retention and verify prohibited standing-cost services are absent;
- record `cdk synth` / reviewed diff / deploy / smoke / destroy / redeploy evidence;
- verify cleanup and any intentionally retained account/bootstrap resources.

## Out of scope

- GitHub OIDC, preview or main-to-dev workflows (`TASK-0095`);
- Cognito, business API/Lambda, module DynamoDB tables, queues/events/workflows, crawler schedules, storefront delivery, or other business infrastructure;
- production infrastructure;
- changing accepted architecture merely to make deployment easier.

## Remaining Ready gates

This task may not move to Ready until all are true:

1. `TASK-0093` is Completed;
2. explicit human/cloud execution authorization is recorded for the selected dev account;
3. dev account/region and Budget-notification destination/threshold inputs are concrete enough for a Builder to execute without guessing.

## Acceptance criteria once Ready

### AC01 — Reproducible declared foundation

A clean checkout can synthesize and deploy the selected dev FoundationStack, and every application resource created by the task maps back to CDK/repository configuration.

### AC02 — Cost guardrails are real

Standard tags, bounded log retention, Budget/credit monitoring, and the absence of prohibited fixed-cost services are verified in the selected account.

### AC03 — Reversible deployment

The dev foundation can be destroyed and redeployed successfully; intentionally retained CDK bootstrap/account-level guardrail resources are explicitly documented.

### AC04 — Verification evidence is complete

Repository harness/local IaC tests, reviewed CDK diff, real cloud smoke evidence, and teardown/redeploy result are recorded without claiming a green cloud result from synthesis alone.

## Architecture/security/cost constraints

- Use AWS CDK/CloudFormation as source of truth.
- Use temporary/SSO credentials for local execution; no long-lived keys committed.
- No business Tenant data is introduced.
- No always-on compute, NAT Gateway, ALB, RDS/Aurora, Redis, OpenSearch, MSK, EKS, or another standing-cost service.
- A deployment timeout is Unknown until CloudFormation state is inspected; do not retry blindly.
- Expected normal monthly change from the empty foundation remains negligible; record any non-negligible actual cost before completion.

## Test plan once Ready

- local harness and CDK assertion tests;
- `cdk synth` and reviewed `cdk diff`;
- real dev deploy and smoke inspection;
- destroy and redeploy proof;
- verify Budget/notification configuration and cleanup state.

**Current gate: REFINED — not executable.**
