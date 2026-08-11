# CommerceOS — Runtime Cost Model

_Status: AWS deployment cost model superseded on 2026-08-11 by ADR-012._

## 1. Current decision

CommerceOS no longer uses a real AWS account for development, staging, validation, or deployment exercises. LocalStack is the only infrastructure/runtime target under ADR-012.

Therefore the previous monthly AWS pricing model, Free Tier assumptions, credit runway, Regional pricing assumptions, Cost Explorer measurements, AWS Budget thresholds, and per-service cloud-spend estimates are not current architecture constraints.

They remain available in Git history if the project later returns to real AWS.

## 2. Current cost concerns

The learning project now tracks only costs/resources that can affect local reproducibility and developer throughput:

- LocalStack edition/licensing requirements;
- local CPU and memory consumption;
- disk usage from container images, volumes, test artifacts, and synthetic object data;
- CI runner duration/resource limits;
- unnecessarily high-volume integration/failure loops;
- external non-AWS services explicitly introduced by a future task.

These are tooling/operational concerns, not business semantics.

## 3. Architecture rule

Do not introduce infrastructure complexity without a named capability need merely because LocalStack makes a service easy to create.

For a new infrastructure mapping ask:

1. What capability/problem requires it?
2. Can an existing capability satisfy the need?
3. Does it materially increase local operational complexity?
4. Is the required behavior supported by the selected LocalStack setup?
5. Is the feature edition/licensing-dependent?
6. Can its state be deterministically bootstrapped/reset for tests?

## 4. Future real-cloud planning

If CommerceOS later targets AWS or another hosted platform, create a new architecture decision and a fresh cost model using then-current prices, regions, traffic assumptions, security requirements, and operational goals.

Do not revive the historical pricing figures as current estimates without revalidation.
