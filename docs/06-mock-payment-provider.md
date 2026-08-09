# CommerceOS — Mock Payment Provider

## 1. Purpose

CommerceOS must learn payment-style distributed-system problems without connecting to a real payment processor or handling real cardholder data.

The Mock Payment Provider is therefore built as an **independent test service** that intentionally behaves like an unreliable third-party provider.

It should create realistic integration problems such as:

- success;
- decline;
- slow response;
- client timeout with ambiguous provider state;
- delayed success;
- duplicate request;
- duplicate webhook;
- webhook arriving before API response is handled;
- retry;
- refund;
- provider-side temporary outage.

The goal is not to simulate banking rules. The goal is to exercise reliable distributed architecture.

---

## 2. Boundary

The mock payment provider must not be implemented as a direct helper method inside the Sales domain.

Desired topology:

```text
CommerceOS
    │
    │ HTTPS
    │ Idempotency-Key
    ▼
Mock Payment API
    │
    ├── Payment state store
    ├── Failure/latency simulator
    └── Webhook dispatcher
              │
              │ signed webhook
              ▼
      CommerceOS Payment Callback API
```

This keeps the integration boundary realistic.

---

# 3. Provider API

Initial conceptual endpoints:

```text
POST /payment-intents
GET  /payment-intents/{id}
POST /payment-intents/{id}/authorize
POST /payment-intents/{id}/capture
POST /payment-intents/{id}/refunds
GET  /refunds/{id}
```

Optional test-control endpoints can exist only in non-production learning environments:

```text
POST /test/failures
POST /test/webhooks/{paymentId}/replay
POST /test/webhooks/{paymentId}/duplicate
POST /test/payments/{paymentId}/complete-delayed
```

---

# 4. Idempotency

State-changing payment calls must accept:

```text
Idempotency-Key: <caller-generated-key>
```

Example:

```text
orderId = ORD-1001
operation = capture

idempotency key:
payment:ORD-1001:capture:v1
```

Provider behavior:

1. first call executes operation and stores result;
2. repeated call with same key and equivalent request returns the previous result;
3. same key with incompatible request is rejected;
4. idempotency result has explicit retention policy.

This allows CommerceOS to safely retry after network ambiguity.

---

# 5. Payment states

```text
Created
   │
   ▼
Authorized
   │
   ▼
Captured
   │
   ▼
PartiallyRefunded / Refunded
```

Alternative states:

```text
Declined
Failed
Pending
Cancelled
```

`TimedOut` should generally be an observation from the caller, not necessarily the provider's durable business state. A client timeout can occur while the provider later becomes `Captured`.

That distinction is important for the learning objective.

---

# 6. Deterministic test scenarios

The provider should support deterministic tokens rather than random failures only.

Example payment methods:

```text
pm_success
pm_declined
pm_timeout_before_commit
pm_timeout_after_commit
pm_delayed_success
pm_provider_500
pm_duplicate_webhook
pm_webhook_before_response
```

Suggested semantics:

### `pm_success`

Immediate successful authorize/capture.

### `pm_declined`

Returns a stable business decline. Retrying should not magically succeed.

### `pm_timeout_before_commit`

Provider intentionally delays/fails before committing payment state.

### `pm_timeout_after_commit`

Provider commits `Captured`, then delays response long enough for caller timeout.

This is one of the most valuable scenarios because CommerceOS must query/reconcile before blindly retrying.

### `pm_delayed_success`

Returns `Pending`; a later webhook confirms success.

### `pm_provider_500`

Returns transient server error for configured number of attempts.

### `pm_duplicate_webhook`

Sends the same signed webhook multiple times.

### `pm_webhook_before_response`

Dispatches a webhook before the original synchronous call response has been fully processed by CommerceOS.

---

# 7. Webhooks

Webhook event examples:

```text
payment.authorized
payment.captured
payment.failed
payment.pending
payment.refunded
```

Envelope:

```json
{
  "id": "wh_evt_...",
  "type": "payment.captured",
  "createdAt": "...",
  "data": {
    "paymentIntentId": "pi_...",
    "merchantReference": "ORD-1001",
    "amount": 2000000,
    "currency": "VND"
  }
}
```

The mock provider signs webhook requests with a test secret.

CommerceOS webhook consumer must:

1. verify signature;
2. reject stale/invalid request according to chosen test policy;
3. deduplicate by webhook event id;
4. map external provider state to internal payment state;
5. publish internal domain event only once;
6. return success fast and do heavier processing asynchronously if necessary.

---

# 8. Failure injection

Provider configuration should support:

```text
latencyMs
failureCountBeforeSuccess
httpStatus
webhookDelayMs
webhookDuplicateCount
webhookOutOfOrder
```

Example:

```json
{
  "scenario": "capture-after-two-500s",
  "failureCountBeforeSuccess": 2,
  "httpStatus": 500,
  "webhookDelayMs": 1000
}
```

The system should avoid purely random chaos in automated tests; deterministic scenarios make tests repeatable.

Randomized failure mode can be added for manual resilience experiments.

---

# 9. CommerceOS payment orchestration

A possible mature flow:

```text
Checkout
   │
   ▼
Create Order(PendingPayment)
   │
   ▼
Reserve Stock
   │
   ▼
Create/Capture Mock Payment
   │
   ├──── success ─────► Confirm Order
   │
   ├──── decline ─────► Release Stock + PaymentFailed
   │
   └──── timeout ─────► PaymentUnknown
                           │
                           ▼
                    Query provider / wait webhook
                           │
                 ┌─────────┴─────────┐
                 ▼                   ▼
              Captured              Failed
                 │                   │
                 ▼                   ▼
          Confirm Order         Release Stock
```

A timeout is not automatically a failure.

---

# 10. Step Functions learning path

Do not begin by forcing the entire payment flow into Step Functions.

Recommended progression:

### Version 1

Application code calls mock payment synchronously.

Learn the pain:

- timeout ambiguity;
- retries;
- duplicated side effects;
- complex state branching.

### Version 2

Move the stateful payment-confirmation workflow to Step Functions.

Learn:

- Retry;
- Catch;
- Wait;
- Choice;
- callback/event continuation;
- timeout;
- execution history.

### Version 3

Add compensation/refund workflow and operational tooling.

This sequence makes the architectural value visible rather than treating Step Functions as mandatory ceremony.

---

# 11. Refund flow

```text
RefundRequested
      │
      ▼
Validate refundable amount
      │
      ▼
Mock provider refund
      │
      ├── success
      │      ↓
      │  PaymentRefunded
      │      ↓
      │  Accounting reversal/contra posting
      │      ↓
      │  Inventory return if business conditions apply
      │
      └── transient failure
             ↓
          retry / operational review
```

Refund idempotency is mandatory.

---

# 12. Data model

Conceptual provider entities:

```text
PaymentIntent
────────────────────
id
merchantReference
amount
currency
status
scenario
createdAt
updatedAt

PaymentOperation
────────────────────
idempotencyKey
paymentIntentId
operation
requestHash
result
createdAt
expiresAt

Refund
────────────────────
id
paymentIntentId
amount
status
createdAt

WebhookDelivery
────────────────────
id
eventId
targetUrl
attempt
status
nextAttemptAt
lastHttpStatus
```

No real PAN/card/CVV fields should exist.

---

# 13. AWS deployment

Suggested independent stack:

```text
MockPaymentStack
  ├── API Gateway HTTP API or Function URL
  ├── Lambda API
  ├── DynamoDB payment state
  ├── SQS webhook-delivery queue
  ├── Lambda webhook dispatcher
  ├── DLQ
  └── CloudWatch alarms/logs
```

Using a queue for webhook delivery creates another realistic integration problem: the provider can commit payment successfully even when merchant callback delivery is temporarily failing.

---

# 14. Metrics

Provider metrics:

- payment intents created;
- captures attempted;
- capture success/decline/error;
- timeout scenarios;
- duplicate requests suppressed by idempotency;
- webhook deliveries;
- webhook retry count;
- webhook DLQ count;
- refund attempts/success;
- latency distribution.

CommerceOS metrics:

- orders in `PaymentUnknown`;
- payment reconciliation duration;
- duplicate webhook ignored count;
- payment workflow failure count.

---

# 15. Reconciliation

A scheduled reconciliation job should eventually inspect orders stuck in payment-ambiguous states.

```text
EventBridge Scheduler
       │
       ▼
Find PaymentUnknown orders
       │
       ▼
Query Mock Payment Provider
       │
       ├── Captured → repair internal projection
       ├── Failed   → release stock/fail order
       └── Pending  → keep waiting / escalate
```

This demonstrates why distributed systems need reconciliation even when webhooks and retries exist.

---

# 16. Definition of done for the mock provider

The provider is useful when automated tests can prove at least these cases:

- [ ] successful payment;
- [ ] deterministic decline;
- [ ] 500 then retry success;
- [ ] timeout before provider commit;
- [ ] timeout after provider commit;
- [ ] delayed webhook success;
- [ ] duplicate webhook does not duplicate order/payment effects;
- [ ] duplicate capture request with same idempotency key is safe;
- [ ] refund retry is idempotent;
- [ ] failed webhook eventually reaches DLQ;
- [ ] reconciliation can repair an ambiguous order state.
