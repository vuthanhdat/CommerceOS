# CommerceOS — Infrastructure as Code

_Last reviewed: 2026-08-09._

## 1. Decision

**All CommerceOS AWS application infrastructure is defined and deployed as code using AWS CDK.**

The AWS Console is permitted for exploration, viewing logs/metrics, or emergency diagnosis, but not as the source of truth for application infrastructure.

If an experiment proves useful, reproduce it in CDK and remove/reconcile the manually created resource.

```text
Git repository
     │
     ├── application code
     ├── tests
     └── infrastructure code
              │
              ▼
           CDK synth
              │
              ▼
        CloudFormation
              │
              ▼
             AWS
```

---

## 2. Why this is mandatory

CommerceOS is both a serverless-learning project and a Harness Engineering project.

Infrastructure as Code provides:

- reproducible environments;
- reviewable architecture changes;
- cost visibility before deployment;
- deterministic teardown of preview/staging;
- fewer undocumented Console changes;
- agent-readable infrastructure context;
- automated security/policy checks;
- easier architecture audits;
- a direct relationship between a Git commit and deployed resources.

---

## 3. Proposed repository structure

Phase 0 should establish a structure similar to:

```text
infra/
  CommerceOS.Cdk/ or commerceos-cdk/
    app
    config
    stacks
    constructs
    tests

src/
  ...

tools/
  commerceos.py
```

The concrete CDK implementation language is selected in Phase 0 based on the final application/tooling choice. The architecture decision is **AWS CDK**, not a requirement to use a particular CDK language in this document.

---

## 4. Stack strategy

Start with a small number of operational stacks rather than one stack per domain.

Target shape:

```text
FoundationStack
  shared configuration
  shared event infrastructure when justified
  baseline observability

IdentityStack
  Cognito

CommerceStack
  API Gateway HTTP API
  Lambda APIs
  DynamoDB

WebStack
  S3
  CloudFront

AsyncStack
  SQS / DLQ
  async Lambda workers
  Step Functions when justified

CrawlerStack
  Scheduler
  crawler queues/workers
  raw snapshot lifecycle

MockPaymentStack
  independently deployed mock provider
```

A stack is an operational/deployment boundary, not automatically a DDD bounded context.

---

## 5. Environment configuration as code

Environment differences are explicit configuration, not hand-edited resources.

Example conceptual profile:

```text
config/
  dev
  preview
  staging
  prod
```

Each profile can define:

- account/region;
- naming prefix;
- removal policy;
- log retention;
- DynamoDB capacity profile;
- Lambda concurrency limits;
- crawler schedule/enablement;
- failure-injection enablement;
- backup/PITR behavior;
- alarm profile;
- cost tags.

Application business logic must not contain arbitrary environment-specific infrastructure branching.

---

## 6. Naming and tags

Every supported resource should carry consistent attribution tags where AWS supports them.

Minimum intent:

```text
Project       = CommerceOS
Environment   = dev | pr-123 | staging | prod
ManagedBy     = CDK
Owner         = personal-learning
CostProfile   = free-tier | credit-funded | production
Ephemeral     = true | false
```

Tags are part of cost governance and cleanup tooling.

---

## 7. CDK workflow

Developer/agent workflow:

```text
change IaC
   ↓
CDK unit/assertion tests
   ↓
cdk synth
   ↓
cdk diff <environment>
   ↓
review resources / IAM / replacements / cost impact
   ↓
CI deployment role via OIDC
   ↓
cdk deploy
   ↓
post-deploy verification
```

No pipeline should jump directly from source changes to `cdk deploy` without synthesis/tests/diff visibility appropriate to the environment.

---

## 8. Prohibited manual drift

Examples of unacceptable permanent setup:

- creating a Lambda manually in Console and not defining it in CDK;
- changing an SQS redrive policy only in Console;
- adding an EventBridge rule manually;
- changing DynamoDB capacity manually and forgetting the IaC definition;
- giving a CI role AdministratorAccess as a shortcut;
- creating a production bucket/table outside the stack and relying on tribal knowledge.

If emergency/manual action is required:

1. record why;
2. determine whether IaC must change;
3. reconcile or revert drift;
4. add a guardrail if the same failure could repeat.

---

## 9. Stateful resources

IaC does not imply `destroy everything` for all environments.

### Dev/preview

Many resources can use destroy/removal-friendly policies because data is synthetic.

### Staging

Policies depend on whether staging is ephemeral or persistent.

### Production

Stateful resources require explicit protection/migration thinking:

- DynamoDB tables;
- S3 buckets containing business files;
- accounting state;
- event/idempotency records where required.

Production deployments must surface replacements or destructive changes before execution.

---

## 10. Free Tier-aware IaC defaults

Non-production constructs should default to cost-safe values:

- no NAT Gateway;
- no ALB unless accepted by ADR;
- no EC2/RDS/OpenSearch/ElastiCache/MSK by default;
- DynamoDB small provisioned profile where that fits the current Free Tier learning goal;
- short CloudWatch log retention;
- bounded Lambda reserved concurrency for crawler/bursty workers;
- crawler schedules disabled or low cadence in dev/preview;
- S3 lifecycle rules for raw crawler artifacts;
- preview resources tagged ephemeral;
- no high-frequency synthetic load by infrastructure defaults.

A reusable CDK construct should encode these defaults so agents do not have to remember them per stack.

---

## 11. Cost-impact rule

Adding a new AWS managed service, changing a capacity class, or introducing a resource with a non-trivial idle/base charge requires:

1. task `Cost impact` section;
2. `cdk diff` review;
3. update to the cost/free-tier documentation when material;
4. ADR if it changes architecture materially or creates a recurring base cost.

The guiding question is not merely "can AWS do this?" but:

> Can the required learning/business capability be achieved with a serverless/pay-per-use/free-tier-friendly service first?

---

## 12. CI/CD identity

CDK deployment from GitHub Actions uses OIDC federation to AWS IAM roles.

No long-lived AWS deployment access keys should be stored in GitHub secrets.

Deployment roles are separated by environment and use least privilege.

---

## 13. Local deployment convenience

Developers may run CDK locally against the dev account using an authenticated AWS CLI/SSO profile.

Useful commands during development include:

```text
cdk synth
cdk diff
cdk deploy <selected-stack>
cdk destroy <ephemeral-stack>
```

`cdk watch`/hotswap may be used for a personal development loop where appropriate, but normal CI/release deployments must use standard CloudFormation/CDK deployment behavior rather than hotswap semantics.

---

## 14. Drift and reproducibility exit criteria

An environment is considered reproducible when:

- a clean repository checkout can synthesize its infrastructure;
- required bootstrap/config prerequisites are documented;
- deployment does not require hidden Console-created application resources;
- an ephemeral environment can be created and removed through IaC;
- IAM and environment parameters are reviewable;
- the deployed resource set maps back to version-controlled CDK definitions.

---

## 15. References

- AWS CDK Developer Guide: https://docs.aws.amazon.com/cdk/v2/guide/home.html
- CDK bootstrapping: https://docs.aws.amazon.com/cdk/v2/guide/bootstrapping.html
- CDK environments: https://docs.aws.amazon.com/cdk/v2/guide/environments.html
- CDK watch: https://docs.aws.amazon.com/cdk/v2/guide/ref-cli-cmd-watch.html
