# CommerceOS — AWS Free Tier & Credit Guardrails

_Last reviewed: 2026-08-09._

## 1. Constraint

CommerceOS starts as a learning project running under AWS Free Tier constraints with approximately **USD 100 of currently available credit**.

This is an architectural constraint, not merely a billing note.

The project therefore prefers:

1. services with meaningful Always Free / monthly free usage;
2. pay-per-request/serverless services with no idle compute cost;
3. tiny/bounded non-production workloads;
4. ephemeral staging/preview environments;
5. explicit cost review before adding recurring/base-cost services.

Do **not** assume the additional USD 100 earnable AWS credit will be available. Planning uses the user's currently stated approximately USD 100 credit envelope.

AWS's current new-customer program provides USD 100 at sign-up and may provide up to USD 100 additional credits through qualifying activities; the Free Plan lasts up to six months or until credits are exhausted. Free Plan accounts have Always Free offers but do not have every Paid-plan trial/feature.

References:

- https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/free-tier-plans.html
- https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/free-tier.html
- https://aws.amazon.com/free/

---

## 2. Budget envelope

Recommended learning budget profiles:

| Profile | Target AWS spend | Purpose |
|---|---:|---|
| Conservative | <= $5/month | normal development; maximize runway |
| Standard learning | <= $10/month | real AWS integration + occasional staging |
| Intensive month | <= $20/month | concentrated Step Functions/event/failure experiments |
| Hard project guardrail | do not intentionally exceed remaining credit / planned horizon | requires explicit human decision |

Initial target: **keep normal months near $0–$5 and spend credits intentionally on learning experiments, not idle infrastructure.**

A USD 100 balance would then support many months of normal development, while still allowing a few deliberate experiments.

---

## 3. Preferred service set

The following are preferred because their architecture fits CommerceOS and their current free allowances/usage economics are favorable for a small learning workload.

### AWS Lambda — preferred

Current monthly free tier includes:

- 1,000,000 requests;
- 400,000 GB-seconds compute.

Use for API handlers, workers, crawler jobs, mock-payment handlers, and projections.

Cost guardrails:

- right-size memory after measurement;
- no provisioned concurrency during initial learning phase;
- bounded reserved concurrency for crawler/failure-prone workers;
- avoid long polling/sleeping inside Lambda.

Reference: https://aws.amazon.com/lambda/pricing/

### Amazon DynamoDB — strongly preferred

DynamoDB is in the Always Free tier with:

- 25 GB storage;
- 25 provisioned WCU;
- 25 provisioned RCU.

For the learning/dev profile, prefer a small **provisioned-capacity configuration within the intended free allowance** when this does not distort the architecture lesson.

On-demand mode may be used intentionally when learning burst behavior or when it materially simplifies a production-like experiment, but it is treated as credit-funded usage.

Reference: https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Introduction.html

### Amazon SQS — preferred

AWS provides the first 1 million SQS requests per month free under the documented free-tier usage model.

Use queues for real asynchronous/backpressure lessons, not merely because a queue service exists.

Cost guardrails:

- small batch/test volume;
- bounded retries;
- DLQ;
- no synthetic high-throughput loops in CI.

Reference pricing: https://aws.amazon.com/sqs/pricing/

### Amazon EventBridge Scheduler — preferred for schedules

Current free tier provides up to 14 million Scheduler invocations per month.

Use for crawler refresh, reconciliation, cleanup, or scheduled learning jobs where scheduling is actually required.

Guardrails:

- dev schedules disabled/manual by default;
- no minute-level crawler schedule merely for demonstration;
- automatic deletion/disablement for one-off experiments where applicable.

Reference: https://aws.amazon.com/eventbridge/pricing/

### Amazon EventBridge event bus — allowed, low-volume

Custom application events are usage-priced rather than assumed free; current public example pricing is around USD 1 per million custom events in common regions.

For CommerceOS learning traffic this should be tiny, but event volume is still counted as **credit-funded** rather than Always Free.

Use it where domain-event decoupling teaches a real architectural concept.

Reference: https://aws.amazon.com/eventbridge/pricing/

### AWS Step Functions — use selectively

Standard Workflows currently include 4,000 free state transitions per month.

This is sufficient for targeted learning and low-volume dev workflows but easy to exceed with noisy automated tests/retries.

Guardrails:

- do not wrap CRUD in Step Functions;
- count expected transitions before load/failure campaigns;
- keep CI workflow executions small;
- prefer unit/definition tests locally and real cloud executions for selected scenarios;
- monitor retries because retry states also consume transitions.

Reference: https://aws.amazon.com/step-functions/pricing/

### Amazon Cognito — preferred for merchant identity

Cognito Lite and Essentials currently include 10,000 directly/socially authenticated MAUs per month in the free tier. Enterprise SAML/OIDC federation has a much smaller free allowance.

For initial CommerceOS merchant staff this is comfortably sufficient.

Guardrails:

- use Lite/Essentials initially;
- do not enable paid advanced capabilities casually;
- avoid SMS MFA during learning unless intentionally testing it, because messaging has separate costs;
- prefer email/authenticator-based learning paths where appropriate.

Reference: https://aws.amazon.com/cognito/pricing/

### Amazon CloudFront — preferred for public static delivery

AWS currently lists CloudFront as an Always Free networking/content-delivery service with monthly allowances including:

- 1 TB data transfer out;
- 10 million HTTP/HTTPS requests.

This is suitable for a learning storefront with tiny traffic.

**Important distinction:** CloudFront also has a newer Flat-Rate Free plan ($0/month), but AWS documentation currently states that accounts using AWS Free Tier are not eligible for CloudFront Flat-Rate Pricing Plans. Therefore CommerceOS must **not** rely on the flat-rate plan while the account is still an AWS Free Tier account.

Use normal CloudFront Free Tier / pay-as-you-go behavior and revisit flat-rate pricing after the account plan changes.

References:

- https://aws.amazon.com/free/networking/
- https://docs.aws.amazon.com/PricingPlanManager/latest/UserGuide/plans.html

### Amazon API Gateway HTTP API — allowed/preferred for API boundary

Current AWS pricing documentation provides new customers a free tier of up to 1 million HTTP API calls per month for up to 12 months.

This fits the project learning traffic.

Guardrails:

- use HTTP API rather than REST API unless a REST-only capability is justified;
- avoid high-volume synthetic API load in CI;
- keep data-transfer payloads small.

Reference: https://aws.amazon.com/api-gateway/pricing/

### Amazon CloudWatch — required but aggressively bounded

CloudWatch currently has free allowances including, among other items:

- 5 GB logs data;
- 10 custom/detailed metrics;
- 3 custom dashboards (within documented limits);
- 10 standard-resolution alarm metrics;
- free basic service metrics.

CloudWatch can still become a surprise cost if verbose logs/custom metrics grow.

Default non-prod policy:

- structured but concise logs;
- `dev`: 7-day retention unless a task needs otherwise;
- `preview`: 1–3 day retention / delete with stack;
- `staging`: 7–14 days during learning phase;
- avoid high-cardinality custom metrics;
- use built-in AWS metrics before creating custom metrics;
- no continuous Live Tail in automation.

Reference: https://aws.amazon.com/cloudwatch/pricing/

### Amazon S3 — allowed, keep tiny and lifecycle-managed

S3 has no idle compute charge and current Free Tier credits apply to eligible S3 usage. The project should nevertheless treat S3 storage/requests as credit-funded usage rather than assuming an indefinite storage allowance.

Use cases:

- frontend origin/static assets;
- product images;
- raw crawler snapshots;
- temporary exports.

Guardrails:

- raw crawler snapshots get short lifecycle expiration;
- previews/staging clean up buckets/objects where safe;
- no large media/video dataset;
- avoid duplicating crawl snapshots across environments.

Reference: https://aws.amazon.com/s3/pricing/

---

## 4. Services not allowed by default

These are not necessarily bad AWS services; they are poor defaults for this project's learning/cost objective because they can create recurring/base cost or unnecessary operational complexity.

Do not introduce without an accepted ADR + explicit monthly estimate:

```text
NAT Gateway
Application / Network Load Balancer
EC2 application servers
RDS / Aurora provisioned databases
OpenSearch domains
ElastiCache
MSK / managed Kafka
EKS
always-on ECS/Fargate services
AWS WAF paid configuration (unless later justified)
paid third-party marketplace services
```

Also scrutinize services with per-resource base charges such as customer-managed KMS keys or paid secret-management patterns before introducing them.

The architecture should prefer serverless/pay-per-use alternatives first.

---

## 5. Environment cost policy

### Local

Expected AWS cost: **$0**.

Normal code/test cycles must not require AWS calls.

### Dev

Persistent but tiny.

Target normal spend: **close to $0/month**, using free allowances where possible.

Cost-safe defaults:

- low DynamoDB provisioned capacity;
- no crawler recurring schedule unless actively learning it;
- low Lambda concurrency;
- short logs;
- tiny synthetic dataset;
- no always-on compute.

### Preview

Expected lifetime: hours, not weeks.

Target cost: effectively negligible per PR.

Only create when cloud semantics require it and destroy automatically.

### Staging

Initially ephemeral/on-demand.

Target: cents/small-dollar experiments, not a second permanent production system during Free Tier learning.

### Production

No production cost target is binding until production is intentionally enabled. Before production, update `docs/04-cost-model.md` from real dev Cost Explorer/CloudWatch measurements.

---

## 6. Credit allocation strategy

Treat the approximately USD 100 credit as a learning fund.

Suggested allocation:

```text
$20  serverless workflow/event experiments
$15  staging/release rehearsals
$15  crawler/integration experiments
$10  observability/failure-injection experiments
$10  API/data-transfer/storage headroom
$20  future phases not yet known
$10  safety reserve
```

This is not a required spending plan. The important principle is to **spend credits intentionally on experiments that teach something**, rather than lose them to idle infrastructure.

---

## 7. Budget/usage monitoring

Before deploying business workloads:

- enable AWS Free Tier usage monitoring/alerts;
- create AWS Budget notifications suitable for the account;
- inspect credit balance regularly;
- tag resources by Project/Environment;
- use Cost Explorer/billing views where available;
- periodically compare actual spend against `docs/04-cost-model.md`.

Recommended alert thresholds for the stated ~USD 100 learning envelope:

```text
monthly forecast: $5
monthly actual:   $10
credit remaining: review at $75 / $50 / $25
```

Exact budget configuration depends on the AWS account plan and available billing features, but cost monitoring must exist before unattended schedules/workloads are enabled.

Reference: https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/tracking-free-tier-usage.html

---

## 8. Task-level cost rule

Every non-trivial task asks:

1. Does this add a new AWS service/resource type?
2. Does it run when no user is active?
3. Does it create recurring scheduled work?
4. Could retries multiply requests/transitions?
5. Could logs/storage grow without bound?
6. Is there a free-tier allowance?
7. If not free, what is the expected learning-scale monthly cost?
8. Can the resource be ephemeral or disabled by default?

A new managed service with material recurring cost requires an ADR.

---

## 9. CI/CD cost rule

CI/CD must not consume the credit envelope unnecessarily.

Default:

```text
PR
  local/mechanical CI only

cloud-sensitive PR
  small ephemeral preview
  ↓
  verify
  ↓
  destroy

main
  persistent tiny DEV

release candidate
  ephemeral/on-demand STAGING
```

Load tests and high-volume failure campaigns require a written estimated request/transition/log cost before execution.

---

## 10. Architecture review trigger

Perform a cost review whenever:

- monthly spend exceeds the selected budget profile;
- credit consumption is materially faster than expected;
- a free allowance is consistently exceeded;
- a service with idle/base cost is proposed;
- staging is proposed to become persistent;
- data transfer/logging/storage begins dominating cost;
- the application transitions from learning to real external users.

At that point the design may change. Free Tier is an initial constraint, not a reason to permanently distort a real production architecture.
