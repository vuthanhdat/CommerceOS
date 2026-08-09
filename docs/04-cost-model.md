# CommerceOS — Monthly AWS Cost Model

_Last planning update: 2026-08-09._

## 1. Purpose

CommerceOS is intentionally serverless, but "serverless" does not mean "always free". This document models how cost changes as tenant count, traffic, workflows, events, and authentication grow.

The numbers below are **planning estimates**, not AWS quotes.

Important assumptions:

- proposed application region: `ap-southeast-1` (Singapore);
- AWS public pricing varies by Region and can change;
- where AWS pricing pages provide simple US East examples, this model uses those public rates as a normalized planning baseline and adds a budget buffer rather than pretending to have invoice-level Singapore precision;
- public storefront shoppers are mostly anonymous/guest users; Cognito MAU therefore primarily represents merchant staff;
- no real payment processor cost;
- no NAT Gateway, ALB, EC2, or always-on relational database;
- CloudFront flat-rate plans are used as a planning model for public delivery;
- promotional/new-account credits are **not** counted as permanent architecture savings;
- taxes are not included.

Before a production-like deployment, reproduce the scenario in AWS Pricing Calculator with `ap-southeast-1` and current usage assumptions.

---

# 2. Current public pricing anchors used by this model

These are intentionally simple cost anchors, not exhaustive service pricing rules.

## AWS Lambda

AWS currently documents a free tier of:

- 1,000,000 requests/month;
- 400,000 GB-seconds/month.

Reference public rate used after free tier:

- requests: approximately `$0.20 / 1M`;
- x86 duration example: approximately `$0.0000166667 / GB-second`.

Source: https://aws.amazon.com/lambda/pricing/

## API Gateway HTTP API

Public pricing examples currently use:

- `$1.00 / 1M HTTP API requests` for the first 300M requests in the example pricing region.

API Gateway also has a time-limited new-customer free tier, but this model **does not subtract it** so the result better represents steady state after introductory benefits.

Source: https://aws.amazon.com/api-gateway/pricing/

## DynamoDB On-Demand

Public US East example rates used for normalized planning:

- writes: `$0.625 / 1M write request units`;
- reads: `$0.125 / 1M read request units`;
- DynamoDB Standard storage: first 25 GB included in current free tier, then public example rate `$0.25 / GB-month`.

Source: https://aws.amazon.com/dynamodb/pricing/

For the very small learning profile, we can optionally use provisioned capacity inside DynamoDB's documented free allowance (25 WCU/25 RCU) to push the bill closer to zero. For realistic spiky SaaS behavior, the main model assumes On-Demand.

## SQS

Current documented free tier:

- first 1M SQS requests/month free.

Normalized standard-queue planning rate after free tier:

- approximately `$0.40 / 1M requests` in common pricing examples.

Remember one logical message can create multiple SQS requests (send, receive, delete, visibility operations).

Source: https://aws.amazon.com/sqs/pricing/

## EventBridge

Current public EventBridge pricing documents:

- custom event ingestion: `$1.00 / 1M events` in the pricing example region;
- delivery from an event bus to a service in the same account: free in the listed pricing model;
- EventBridge Scheduler: first 14M invocations/month free, then `$1.00 / 1M`.

Source: https://aws.amazon.com/eventbridge/pricing/

This cost model includes custom event ingestion but assumes scheduler usage remains far inside the free allowance for all early scenarios.

## Step Functions Standard

Current documented free tier:

- 4,000 state transitions/month.

Public US East example rate:

- `$0.000025 / state transition` after the free tier.

Retries also consume transitions.

Source: https://aws.amazon.com/step-functions/pricing/

## Cognito

Current Cognito Essentials/Lite direct/social sign-in free tier:

- 10,000 MAU/month per account/organization according to current pricing documentation.

For Cognito Essentials, a current public example uses:

- `$0.015 / MAU` above the 10,000 MAU free tier.

Source: https://aws.amazon.com/cognito/pricing/

Because CommerceOS initially uses guest checkout, shopper traffic does not automatically become Cognito MAU.

## CloudFront flat-rate plans

Current published flat-rate plans include:

- Free: `$0/month`, published allowance 1M requests + 100 GB transfer;
- Pro: `$15/month`, published allowance 10M requests + 50 TB transfer;
- Business: `$200/month`, published allowance 125M requests + 50 TB transfer;
- Premium: `$1,000/month`, published default allowance 500M requests + 50 TB transfer.

The plans bundle CloudFront delivery and several related capabilities, including security/DNS/logging/storage-credit features described by AWS.

Sources:

- https://aws.amazon.com/cloudfront/pricing/
- https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/flat-rate-pricing-plan.html

For planning, CommerceOS voluntarily moves to the plan whose published allowance fits expected request volume instead of relying on no-overage behavior.

## CloudWatch Logs

Current CloudWatch documentation includes a free-tier allowance of 5 GB for logs-related usage categories. For rough modeling beyond that, this document uses `$0.50/GB` as a normalized planning rate for log ingestion; verify Singapore pricing at deployment time.

Source: https://aws.amazon.com/cloudwatch/pricing/

## S3

S3 is usage-based. This model uses a normalized `$0.023/GB-month` storage planning rate before any CloudFront-plan S3 credits.

Source: https://aws.amazon.com/s3/pricing/

Crawler raw payloads will use short lifecycle retention, so the largest S3 consumer should eventually be merchant images and exports rather than raw HTML.

---

# 3. Cost-unit assumptions

The model assumes an average Lambda execution for API/background work of:

```text
Memory:   512 MB
Duration: 120 ms
```

Therefore one invocation consumes approximately:

```text
0.5 GB × 0.12 seconds = 0.06 GB-seconds
```

This is deliberately simplistic. Actual cost must later use measured CloudWatch duration/memory metrics.

---

# 4. Scenario A — Personal learning environment

## Business assumptions

- 5 merchant tenants;
- 25 merchant staff MAU;
- 2,000 anonymous shoppers/month;
- 500 orders/month;
- one active learner/developer;
- one or two crawler adapters;
- low-frequency crawler schedule.

## Technical monthly assumptions

- 0.25M API Gateway requests;
- 0.35M Lambda invocations;
- 0.8M DynamoDB reads;
- 0.3M DynamoDB writes;
- 5 GB DynamoDB data;
- 0.03M EventBridge custom events;
- 0.15M SQS requests;
- 10,000 Step Functions transitions;
- CloudFront Free plan;
- 1 GB CloudWatch logs;
- 5 GB S3 data.

## Estimated monthly cost

| Service | Estimate |
|---|---:|
| API Gateway | $0.25 |
| Lambda | $0.00 |
| DynamoDB On-Demand | $0.29 |
| EventBridge | $0.03 |
| SQS | $0.00 |
| Step Functions | $0.15 |
| CloudFront plan | $0.00 |
| CloudWatch Logs | $0.00 |
| S3 storage | $0.11 |
| Cognito | $0.00 |
| **Estimated total** | **~$0.83/month** |

Recommended budget envelope with pricing/usage uncertainty:

> **$0–$2/month**

If DynamoDB provisioned capacity is intentionally kept inside the documented free allowance and introductory/account credits apply, the actual bill can approach `$0`. The architecture should nevertheless be tested as if the free credits did not exist.

---

# 5. Scenario B — Private beta / small real usage

## Business assumptions

- 50 merchant tenants;
- 500 merchant staff MAU;
- 25,000 anonymous shoppers/month;
- 5,000 orders/month;
- multiple crawler sources;
- basic reporting and accounting automation.

## Technical monthly assumptions

- 2.5M API requests;
- 3.5M Lambda invocations;
- 8M DynamoDB reads;
- 3M DynamoDB writes;
- 20 GB DynamoDB data;
- 0.4M EventBridge custom events;
- 0.8M SQS requests;
- 100,000 Step Functions transitions;
- CloudFront Pro plan;
- 4 GB CloudWatch logs;
- 20 GB S3 data.

## Estimated monthly cost

| Service | Estimate |
|---|---:|
| API Gateway | $2.50 |
| Lambda | $0.50 |
| DynamoDB | $2.88 |
| EventBridge | $0.40 |
| SQS | $0.00 |
| Step Functions | $2.40 |
| CloudFront Pro | $15.00 |
| CloudWatch Logs | $0.00 |
| S3 storage | $0.46 |
| Cognito | $0.00 |
| **Estimated total** | **~$24.14/month** |

Recommended planning budget:

> **~$30/month**

At this stage CloudFront's selected flat-rate plan becomes the largest single modeled fixed line item.

---

# 6. Scenario C — Small SaaS business

## Business assumptions

- 500 merchant tenants;
- 5,000 merchant staff MAU;
- 250,000 anonymous shoppers/month;
- 50,000 orders/month;
- scheduled crawlers and richer reporting;
- meaningful event-driven accounting workload.

## Technical monthly assumptions

- 25M API requests;
- 35M Lambda invocations;
- 80M DynamoDB reads;
- 30M DynamoDB writes;
- 60 GB DynamoDB data;
- 5M EventBridge custom events;
- 8M SQS requests;
- 1M Step Functions transitions;
- CloudFront Business plan;
- 20 GB CloudWatch logs;
- 100 GB S3 data.

## Estimated monthly cost

| Service | Estimate |
|---|---:|
| API Gateway | $25.00 |
| Lambda | $35.13 |
| DynamoDB | $37.50 |
| EventBridge | $5.00 |
| SQS | $2.80 |
| Step Functions | $24.90 |
| CloudFront Business | $200.00 |
| CloudWatch Logs | $7.50 |
| S3 storage | $2.30 |
| Cognito | $0.00 |
| **Estimated total** | **~$340.13/month** |

Recommended planning budget with buffer:

> **~$400–$425/month**

This is the first scenario where architectural efficiency of read models, event volume, workflow transition count, and Lambda duration becomes financially visible.

---

# 7. Scenario D — Larger SaaS scale

This scenario exists mainly to expose future cost cliffs rather than to set an early target.

## Business assumptions

- 5,000 merchant tenants;
- 50,000 merchant staff MAU;
- 2.5M anonymous shoppers/month;
- 500,000 orders/month.

## Technical monthly assumptions

- 250M API requests;
- 350M Lambda invocations;
- 800M DynamoDB reads;
- 300M DynamoDB writes;
- 300 GB DynamoDB data;
- 50M EventBridge custom events;
- 80M SQS requests;
- 10M Step Functions transitions;
- CloudFront Premium plan;
- 150 GB CloudWatch logs;
- 500 GB S3 data.

## Estimated monthly cost

| Service | Estimate |
|---|---:|
| API Gateway | $250.00 |
| Lambda | $413.13 |
| DynamoDB | $356.25 |
| EventBridge | $50.00 |
| SQS | $31.60 |
| Step Functions | $249.90 |
| CloudFront Premium | $1,000.00 |
| CloudWatch Logs | $72.50 |
| S3 storage | $11.50 |
| Cognito Essentials | $600.00 |
| **Estimated total** | **~$3,034.88/month** |

Recommended planning budget:

> **~$3,500–$3,700/month**

Important lesson: at larger scale, **Cognito MAU, CDN plan choice, Lambda duration, DynamoDB access patterns, and workflow transition count** become major architectural cost drivers.

---

# 8. Summary

| Scenario | Tenants | Merchant MAU | Shoppers/mo | Orders/mo | Modeled AWS cost |
|---|---:|---:|---:|---:|---:|
| Learning | 5 | 25 | 2,000 | 500 | ~$0.83 |
| Beta | 50 | 500 | 25,000 | 5,000 | ~$24 |
| Small SaaS | 500 | 5,000 | 250,000 | 50,000 | ~$340 |
| Larger scale | 5,000 | 50,000 | 2.5M | 500,000 | ~$3,035 |

The cost curve is the reason the project should keep cost metrics as part of architecture review rather than only checking the bill after deployment.

---

# 9. Costs deliberately excluded from the model

Potential extra charges that must be modeled if introduced:

- AWS Backup / DynamoDB PITR;
- SES email;
- SMS/MFA messaging;
- custom Route 53 usage outside a bundled CloudFront plan configuration;
- third-party domain registration;
- external crawler proxy services;
- CAPTCHA solving or anti-bot bypass services — not planned and not appropriate for the crawler design;
- data transfer patterns not covered by the selected CDN plan;
- Athena/Glue/data-lake analytics;
- OpenSearch;
- real payment processor fees;
- CI/CD runner usage outside included GitHub allowances;
- third-party monitoring;
- tax/VAT.

---

# 10. Cost guardrails to implement from Task 0

1. Create an AWS Budget alert.
2. Tag every stack with `Project=CommerceOS` and `Environment`.
3. Explicit CloudWatch log retention.
4. S3 lifecycle for raw crawler payloads.
5. Crawler concurrency cap.
6. Lambda reserved concurrency for dangerous/bursty workers.
7. DynamoDB max on-demand throughput where useful.
8. No NAT Gateway without an ADR.
9. No always-on database without an ADR.
10. Review Cost Explorer after every new architectural phase.
11. Add a monthly cost dashboard/readme update once real usage replaces assumptions.

---

# 11. Cost experiments this project should run

The project is educational, so cost itself becomes an experiment.

Suggested comparisons:

### Experiment A

DynamoDB provisioned free-tier profile vs On-Demand profile for the same learning workload.

### Experiment B

Synchronous API side effects vs EventBridge/SQS decoupling — compare reliability and request count.

### Experiment C

Step Functions Standard workflow vs application-code orchestration for a small order flow — compare maintainability, observability, and transition cost.

### Experiment D

Public catalog cached at CloudFront vs uncached API reads — compare latency and backend cost.

### Experiment E

Crawler batch size/concurrency vs Lambda/SQS cost and source-site load.

Real measurements should eventually replace the assumptions in this document.
