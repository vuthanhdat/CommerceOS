# CommerceOS — Product Data Ingestion & Crawling

## 1. Goal

CommerceOS should not depend on hand-written fake catalog data forever. The project needs repeatable external product sources that can bootstrap and enrich the catalog while also creating realistic serverless batch/event workloads.

The ingestion subsystem is therefore a first-class supporting domain.

It must remain **separate from the merchant's canonical catalog**.

```text
External source
      ↓
Source Snapshot
      ↓
Normalize
      ↓
Import Candidate
      ↓
Merchant Review/Rule
      ↓
Canonical Product
```

A source changing its title, price, image, or HTML structure must never silently corrupt a merchant's published product.

---

# 2. Initial source strategy

Desired source adapters:

| Source | Preferred acquisition strategy | Notes |
|---|---|---|
| Amazon | Official Amazon Creators API where account/usage terms permit | Do not make HTML scraping the default Amazon strategy |
| The Gioi Di Dong | Public-page adapter only after implementation-time robots/terms review | Rate-limited, source-specific parser |
| Dien May Xanh | Public-page adapter only after implementation-time robots/terms review | Rate-limited, source-specific parser |
| CellphoneS | Public-page adapter only after implementation-time robots/terms review | Rate-limited, source-specific parser |
| Other merchant/catalog sites | Plug-in source adapter | Must pass source-policy checklist |

Important 2026 Amazon note:

Amazon's old Product Advertising API 5.0 has been deprecated and replaced by **Creators API**. Do not implement a new PA-API 5 integration.

References:

- https://affiliate-program.amazon.com/creatorsapi/docs/en-us/paapiv5-deprecation
- https://affiliate-program.amazon.com/creatorsapi/docs/en-us/introduction
- https://affiliate-program.amazon.com/help/operating/policies

Amazon's product-content APIs are governed by program/license requirements, so CommerceOS must treat Amazon as an official-integration adapter rather than assuming catalog content may be freely copied into an unrelated storefront.

---

# 3. What data should be collected?

Normalized source product fields should focus on structured catalog facts.

Proposed fields:

```text
source
sourceProductId
sourceUrl
capturedAt

name
brand
categoryPath
model
sku/sourceSku

currentPrice
listPrice
currency
availabilityText

rating
ratingCount

primaryImageUrl
imageUrls

specifications[]
  name
  value

sourceDescriptionReference
rawSnapshotId
contentHash
```

Not every source provides every field.

The normalized schema must distinguish:

- absent value;
- parsing failure;
- source explicitly stating unavailable/unknown.

---

# 4. Canonical product vs source snapshot

This distinction is mandatory.

## Source snapshot

Represents what an external source showed at a point in time.

Characteristics:

- source-owned;
- timestamped;
- immutable after creation;
- parser/version traceable;
- useful for price history/change detection;
- may be discarded by retention policy for raw payloads.

## Canonical merchant product

Represents what the tenant actually sells.

Characteristics:

- tenant-owned;
- merchant editable;
- own SKU;
- own selling price;
- own publishing status;
- own inventory relationship;
- can map to zero, one, or multiple external sources.

Example:

```text
Amazon ASIN ───────┐
TGDD product ──────┼──► External mappings ───► Tenant Product P001
CellphoneS item ───┘
```

The external price is reference data. It must not automatically become the merchant's sell price unless an explicit pricing rule says so.

---

# 5. Source adapter contract

Each adapter should implement a stable application contract such as:

```text
Discover(query/category)
Fetch(sourceProductId/url)
Parse(rawPayload)
Normalize(parsedProduct)
Validate(normalizedProduct)
```

Conceptual result:

```json
{
  "source": "thegioididong",
  "sourceProductId": "...",
  "sourceUrl": "...",
  "capturedAt": "...",
  "product": {
    "name": "...",
    "brand": "...",
    "price": 0,
    "currency": "VND",
    "specifications": []
  },
  "parserVersion": "1",
  "contentHash": "..."
}
```

Source-specific HTML selectors, API credentials, or parsing details must stay inside adapter infrastructure rather than leak into Catalog domain logic.

---

# 6. Serverless ingestion architecture

```text
                      Source Registry
                            │
                            ▼
                   EventBridge Scheduler
                            │
                            ▼
                    Crawler Dispatcher
                            │
                            ▼
                         SQS Queue
                            │
           ┌────────────────┼────────────────┐
           ▼                ▼                ▼
      Crawler Lambda   Crawler Lambda   Crawler Lambda
           │                │                │
           ▼                ▼                ▼
       Adapter A        Adapter B        Adapter C
           │
           ▼
       Raw Response
           │
           ▼
      S3 short-retention
           │
           ▼
    Parse + Normalize
           │
           ▼
 Source Product Snapshot
        DynamoDB
           │
           ▼
      Compare hash/data
           │
      changed / new?
           │
           ▼
 ProductSourceChanged event
           │
           ▼
 Import candidate / price history / review
```

---

# 7. Why SQS is required

Crawler workload is bursty and external systems are outside our control.

SQS gives:

- backpressure;
- controlled concurrency;
- retry;
- separation between schedule/discovery and actual fetch;
- DLQ for poisoned URLs/parser failures;
- ability to pause workers without losing work.

The crawler must never scale uncontrolled simply because Lambda can scale.

Source politeness is more important than maximum AWS concurrency.

---

# 8. Source registry

Proposed configuration:

```text
DataSource
────────────────────────
id
name
baseHost
mode = Api | Html
status = Active | Paused | Disabled
requestsPerSecond
maxConcurrency
crawlWindow
rawRetentionDays
adapterVersion
policyReviewedAt
policyReviewNote
```

This turns source compliance/rate configuration into explicit system state rather than hidden constants.

---

# 9. Crawl modes

## 9.1 Manual import

User pastes a supported product URL.

```text
URL
 ↓
validate source
 ↓
queue crawl
 ↓
show import candidate
```

This should be the first feature because scope is easy to control.

## 9.2 Seed list

Repository/configuration contains a bounded list of source URLs used for development/demo.

## 9.3 Scheduled refresh

Refresh known mapped source products, for example daily or every few hours depending on source policy and project needs.

## 9.4 Discovery crawl

Category/search crawling is a later phase because it multiplies request volume, pagination complexity, duplicate detection, policy risk, and bot-protection friction.

Do not start here.

---

# 10. Pipeline states

```text
Scheduled
  ↓
Queued
  ↓
Fetching
  ↓
Fetched
  ↓
Parsing
  ↓
Normalized
  ↓
Validated
  ↓
Stored
```

Failure examples:

```text
BlockedByPolicy
RobotsDisallowed
Http429
Http403
Timeout
SourceUnavailable
ParserBroken
ValidationFailed
UnexpectedContent
```

`403`, CAPTCHA, or anti-bot challenge is **not** interpreted as an engineering invitation to bypass protection.

---

# 11. Retry policy

Retries are suitable for transient failures:

- temporary network failure;
- 5xx;
- selected 429 response with long backoff where source rules permit.

Do not blindly retry:

- stable 404;
- known policy restriction;
- CAPTCHA/anti-bot challenge;
- parser validation proving page shape has changed;
- authentication-required resource not supported by the adapter.

After bounded retries:

```text
crawler-jobs
    ↓
   DLQ
    ↓
Operational Review
```

---

# 12. Rate limiting

Rate limits are per source, not only global.

Example development defaults (placeholders, not claims about what a site permits):

```text
source A: max concurrency 1
source B: max concurrency 1
source C: max concurrency 2
```

Actual rates are configured only after checking the current source policy and observing acceptable behavior.

The worker should include jitter between requests when applicable so all scheduled targets do not hit an external site at the same instant.

---

# 13. Change detection

Normalize before comparing where possible.

Useful hashes:

```text
contentHash       raw/meaningful page content
normalizedHash    normalized structured product
priceHash         price/availability subset
specHash          specifications subset
```

Events can then be more meaningful:

```text
ProductSourceCrawled
ProductSourcePriceChanged
ProductSourceAvailabilityChanged
ProductSourceSpecsChanged
ParserBehaviorChanged
```

This creates real event-driven data without manually inventing a dataset.

---

# 14. Import workflow

MVP import should be human-reviewed.

```text
External Snapshot
       ↓
Import Candidate
       ↓
Merchant sees diff/fields
       ↓
Choose fields to import
       ↓
Create/Update Canonical Product
```

Example:

```text
Name                 [x] import
Brand                [x] import
Specifications       [x] import
Source price         [ ] reference only
Description          [ ] do not copy
Images               [ ] reference/review
```

This avoids hidden source-to-storefront coupling.

---

# 15. Images and descriptive content

Product images/descriptions may carry licensing/copyright restrictions.

Therefore the initial design should prefer:

- storing source URLs and attribution/reference metadata;
- importing structured merchant-safe facts;
- only copying/cache-hosting images when the relevant source/license permits it;
- merchant-owned images for canonical storefront content where possible.

Do not assume that because a browser can download an image, CommerceOS may republish it.

---

# 16. Raw-data retention

Raw payloads are primarily for debugging parser changes.

Proposed learning retention:

```text
raw HTML/API payload: 7 days
normalized snapshots: longer/configurable
price history: configurable
crawler logs: 7–14 days
```

S3 lifecycle removes expired raw objects automatically.

---

# 17. Parser versioning

Every normalized snapshot should record:

```text
adapterName
adapterVersion
schemaVersion
```

When a site changes HTML:

```text
old parser
   ↓
validation failures increase
   ↓
alarm
   ↓
new parser version
   ↓
reprocess retained raw samples if useful
```

This turns crawler maintenance into a visible engineering workflow.

---

# 18. Observability

Per source metrics:

- fetch count;
- success rate;
- status-code distribution;
- 429 rate;
- 403 rate;
- parser failure rate;
- average duration;
- queue depth;
- oldest queue age;
- DLQ count;
- products changed;
- bytes downloaded;
- estimated Lambda/SQS cost.

---

# 19. Source-policy checklist

Before enabling a new HTML source adapter:

- [ ] Check current robots policy.
- [ ] Check current site terms relevant to automated access/reuse.
- [ ] Check whether an official API/feed exists.
- [ ] Define allowed URL patterns.
- [ ] Define prohibited URL patterns.
- [ ] Define rate and concurrency limits.
- [ ] Define raw-data retention.
- [ ] Decide what fields may be stored/reused.
- [ ] Decide image/content handling.
- [ ] Define identification/user-agent strategy where appropriate.
- [ ] Add adapter-specific tests using saved permitted fixtures rather than repeatedly hitting the live source.
- [ ] Add kill switch (`Paused`/`Disabled`).

---

# 20. MVP ingestion scope

Start small:

### Phase 1

- Source Registry;
- manual URL import;
- **one Vietnamese electronics source adapter** chosen after implementation-time policy review;
- SQS crawler queue + DLQ;
- raw S3 snapshot lifecycle;
- normalized DynamoDB source snapshot;
- merchant review/import page;
- parser fixtures/tests.

### Phase 2

- second Vietnamese source;
- scheduled refresh;
- price-history snapshots;
- change events.

### Phase 3

- Amazon Creators API adapter if project account/onboarding/license usage is appropriate;
- product matching across sources;
- source comparison.

### Phase 4

- controlled discovery/category ingestion if still useful and permitted.

This sequencing gives the project genuine crawler/event workloads without making scraping complexity block the core commerce MVP.
