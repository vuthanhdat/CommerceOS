# F05 — Product Data Ingestion

## Feature goal
Acquire external product evidence safely and reproducibly without transferring authority to external sources.

## Source requirements
REQ-PDI-001..007, REQ-CAT-006, REQ-SUB-004.

## Scope
Source registry/policy; one implementation-time policy-approved adapter; manual URL flow; queue/backpressure/rate/retry/DLQ; raw short-retention snapshot; normalize/version/fixtures; ImportCandidate review; later scheduled refresh with policy + entitlement recheck.

## Out of scope
Anti-bot bypass, arbitrary discovery crawling, automatic Catalog mutation, unlicensed external media republication.

## Architecture
PDI owns source/run/snapshot/candidate truth. SQS-style durable work is for one crawler worker path; S3-style object storage retains bounded raw evidence; Catalog application remains explicit.

## Task sequence
TASK-0140 -> TASK-0141 -> TASK-0142 -> TASK-0143 -> TASK-0144.

## Definition of Done
A permitted fixture-backed source can be ingested deterministically, failures are bounded/actionable, raw/source provenance is preserved and Catalog changes only after approved apply.
