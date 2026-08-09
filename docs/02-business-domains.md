# CommerceOS — Business Domains

## 1. Domain map

CommerceOS is organized around business capabilities first, AWS services second.

```text
                           COMMERCEOS
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
   TENANT/IDENTITY         COMMERCE CORE          OPERATIONS
        │                      │                      │
        │                 Catalog / Sales        Inventory
        │                 Customers              Procurement
        │                 Pricing                Fulfillment
        │
        └──────────────────────┬──────────────────────┘
                               │
                               ▼
                           PAYMENTS
                               │
                               ▼
                           ACCOUNTING
                               │
                               ▼
                           REPORTING

Supporting platform domains:
- Product Data Ingestion
- Notification
- Audit
- Files/Media
- Observability/Operations
```

The domains below are logical/business boundaries. They are **not automatically microservices**.

---

## 2. Classification

### Core domains

Capabilities that define the educational/business value of CommerceOS:

1. Sales & Order Management
2. Inventory
3. Procurement
4. Accounting
5. Integration between operational events and accounting effects

### Supporting domains

1. Catalog
2. Customer/CRM
3. Pricing & Promotion
4. Mock Payment
5. Product Data Ingestion
6. Reporting/Analytics

### Generic/platform domains

1. Tenant & Identity
2. Authorization
3. Notification
4. Audit
5. Files/Media
6. Operational monitoring

---

# 3. Tenant & Identity

## Responsibility

Owns SaaS tenancy and merchant-user membership.

## Main entities

- Tenant
- BusinessProfile
- UserMembership
- Role
- Permission
- Invitation

## Invariants

- a merchant user acts inside an authorized tenant context;
- membership must be active;
- platform administration is separate from merchant authorization;
- tenant-scoped resources cannot be accessed using another tenant's identity.

## Commands

- RegisterTenant
- InviteStaff
- AcceptInvitation
- AssignRole
- DisableMembership
- UpdateBusinessProfile

## Events

- TenantCreated
- StaffInvited
- StaffJoined
- RoleAssigned
- MembershipDisabled

## Does not own

- products;
- orders;
- accounting journals;
- inventory.

---

# 4. Catalog

## Responsibility

Defines what a merchant can sell.

## Main entities

- Product
- ProductVariant (later)
- Category
- Brand
- ProductImage
- ProductSpecification
- ExternalProductMapping

## Invariants

- SKU is unique within a tenant according to defined policy;
- archived products cannot be newly published;
- merchant canonical data remains merchant-owned even when mapped to crawler source data;
- source-data changes never silently overwrite merchant overrides.

## Commands

- CreateProduct
- UpdateProduct
- PublishProduct
- UnpublishProduct
- ArchiveProduct
- ImportExternalProduct
- MapExternalProduct

## Events

- ProductCreated
- ProductUpdated
- ProductPublished
- ProductUnpublished
- ProductImported
- ExternalProductMapped

## Does not own

- available stock quantity;
- sales order;
- accounting value.

Catalog can display inventory projections but does not mutate stock.

---

# 5. Storefront

## Responsibility

Public customer-facing projection of tenant catalog and checkout entry point.

## Main concepts

- storefront configuration;
- public catalog projection;
- product detail projection;
- cart session;
- checkout request.

Storefront should remain thin. It orchestrates customer interaction but does not become owner of order, inventory, or payment state.

---

# 6. Sales & Order Management

## Responsibility

Owns the commercial agreement that a customer intends to purchase specific items at captured prices and quantities.

## Main entities

- Cart/CheckoutRequest
- SalesOrder
- SalesOrderLine
- OrderPriceSnapshot
- OrderStatusHistory
- RefundRequest

## Initial order states

```text
Draft
  ↓
PendingPayment
  ↓
Confirmed
  ↓
Allocated
  ↓
Fulfilled
  ↓
Completed
```

Failure/exception states:

- PaymentFailed
- Cancelled
- PartiallyRefunded
- Refunded

## Invariants

- repeated checkout with same idempotency key cannot create multiple business orders;
- order price is a snapshot, not a live pointer to current product price;
- completed order lines are not retroactively rewritten because product metadata later changes;
- invalid order-state transitions are rejected.

## Commands

- CheckoutCart
- CreateOrder
- ConfirmOrder
- CancelOrder
- FulfillOrder
- CompleteOrder
- RequestRefund

## Events

- OrderPlaced
- OrderConfirmed
- OrderCancelled
- OrderAllocated
- OrderFulfilled
- OrderCompleted
- RefundRequested
- OrderRefunded

## Dependencies

Consumes catalog information and inventory availability through explicit contracts/read models.

Publishes business facts used by payment, inventory, accounting, notification, and analytics.

---

# 7. Inventory

## Responsibility

Owns physical stock state and stock movement.

## Main entities

- Warehouse
- StockItem
- StockReservation
- StockMovement
- InventoryAdjustment

## Quantities

```text
OnHand
Reserved
Available = OnHand - Reserved
```

## Movement types

- Receive
- Reserve
- Release
- Issue
- Return
- AdjustmentIncrease
- AdjustmentDecrease

## Invariants

- reservation cannot reduce available stock below permitted threshold;
- every stock change has a movement record;
- stock movement is traceable to business source where applicable;
- duplicate events cannot create duplicate stock movements.

## Commands

- ReserveStock
- ReleaseReservation
- ReceiveStock
- IssueStock
- ReturnStock
- AdjustStock

## Events

- StockReserved
- StockReservationFailed
- StockReleased
- StockReceived
- StockIssued
- StockReturned
- StockAdjusted
- LowStockDetected

## Does not own

- selling price;
- customer order lifecycle;
- journal entries.

---

# 8. Procurement

## Responsibility

Owns merchant purchasing from suppliers.

## Main entities

- Supplier
- PurchaseOrder
- PurchaseOrderLine
- GoodsReceipt
- SupplierInvoiceReference

## Initial lifecycle

```text
Draft
  ↓
Submitted
  ↓
GoodsReceived
  ↓
Invoiced
  ↓
Paid
  ↓
Closed
```

## Invariants

- goods receipt quantity cannot exceed allowed PO quantity unless explicitly approved;
- received items become inventory through an explicit inventory command/event;
- supplier payment does not directly edit accounting tables.

## Commands

- CreatePurchaseOrder
- SubmitPurchaseOrder
- ReceiveGoods
- RecordSupplierInvoice
- MarkSupplierPaid
- ClosePurchaseOrder

## Events

- PurchaseOrderCreated
- PurchaseOrderSubmitted
- GoodsReceived
- SupplierInvoiceRecorded
- SupplierPaid
- PurchaseOrderClosed

---

# 9. Customer / CRM

## Responsibility

Owns customer profile and merchant-facing customer relationship data.

## Main entities

- Customer
- CustomerContact
- CustomerAddress
- CustomerNote (later)

Order history is a projection/reference to Sales, not duplicated ownership.

## Events

- CustomerCreated
- CustomerUpdated

---

# 10. Pricing & Promotion

## Responsibility

Owns rules that transform catalog base price into a sellable offer.

Initial scope:

- base price reference;
- authorized manual discount;
- scheduled promotion later.

Future concepts:

- Promotion
- Coupon
- PriceList
- DiscountRule
- Campaign

## Events

- PromotionScheduled
- PromotionActivated
- PromotionExpired
- PriceRuleChanged

---

# 11. Mock Payment

## Responsibility

Simulates an external payment provider boundary.

The mock provider should behave like a third-party integration rather than a helper class inside Order.

## Main entities

- PaymentIntent
- PaymentAttempt
- Refund
- WebhookDelivery

## States

- Created
- Authorized
- Captured
- Declined
- Pending
- TimedOut
- Refunded

## Events

Provider-facing webhook events:

- payment.authorized
- payment.captured
- payment.failed
- payment.pending
- payment.refunded

Internal CommerceOS events after verified webhook handling:

- PaymentAuthorized
- PaymentCaptured
- PaymentFailed
- PaymentRefunded

See `06-mock-payment-provider.md`.

---

# 12. Accounting

## Responsibility

Owns the financial ledger representation of business activity.

Accounting does **not** own order fulfillment or inventory operations. It consumes explicit business facts and applies accounting rules.

## Main entities

- Account
- JournalEntry
- JournalLine
- AccountingPeriod (later)
- PostingRule
- SourceDocumentReference

## Initial chart-of-account concepts

- Cash
- Accounts Receivable
- Inventory
- Accounts Payable
- Sales Revenue
- Cost of Goods Sold
- Refund/Contra Revenue where needed
- Inventory Adjustment Expense/Gain where needed

## Core invariants

1. Posted journal is immutable.
2. Sum(debit) equals sum(credit).
3. Posting source/event is traceable.
4. Same idempotency/source key cannot be posted twice.
5. Correction uses reversal and replacement rather than direct mutation.

## Example — sale

For a product sold at 2,000,000 with cost 1,200,000:

```text
Dr Cash                    2,000,000
    Cr Sales Revenue                   2,000,000

Dr Cost of Goods Sold      1,200,000
    Cr Inventory                       1,200,000
```

The exact timing of revenue/COGS recognition will be a documented project accounting policy and kept consistent.

## Commands

- CreateManualJournal
- PostJournal
- ReverseJournal
- GeneratePostingFromBusinessEvent

## Events

- JournalCreated
- JournalPosted
- JournalRejected
- JournalReversed

## Reporting projections

- General Ledger
- Trial Balance
- Accounts Receivable
- Accounts Payable
- Profit & Loss later
- Balance Sheet later

---

# 13. Reporting & Analytics

## Responsibility

Produces read-optimized projections and aggregates. It should not become a transactional source of truth.

Examples:

- daily revenue;
- order count;
- average order value;
- gross-profit projection;
- inventory value;
- top products;
- outstanding receivables/payables;
- failed payment rate;
- crawler changes;
- tenant/platform usage.

Input primarily comes from domain events and scheduled aggregation.

---

# 14. Product Data Ingestion

## Responsibility

Collects, snapshots, normalizes, and proposes external product data without making external sources the canonical commerce database.

## Main entities

- DataSource
- CrawlTarget
- CrawlRun
- RawSourceSnapshot
- NormalizedSourceProduct
- SourcePriceSnapshot
- ImportCandidate

## Events

- CrawlScheduled
- CrawlStarted
- CrawlSucceeded
- CrawlFailed
- ProductSourceCrawled
- ProductSourceChanged
- ImportCandidateCreated

See `05-product-data-ingestion.md`.

---

# 15. Notification

Consumes events and creates non-critical user notifications.

Examples:

- payment failure;
- low stock;
- failed workflow;
- crawl failure;
- DLQ alert;
- order status change.

Notification failure must not roll back already committed business transactions.

---

# 16. Audit

Owns append-oriented security/operation audit records.

Audit is different from business event storage:

- **business event** describes a domain fact;
- **audit record** describes who performed an operation and the security/operational context.

---

# 17. High-level domain interaction

```text
External Product Sources
          │
          ▼
 Product Data Ingestion
          │
          ▼
        Catalog ───────────────┐
          │                    │
          ▼                    │
      Storefront               │
          │                    │
          ▼                    │
        Sales                  │
      /       \                │
     ▼         ▼               │
Payment     Inventory ◄────────┘
               ▲
               │
          Procurement

Sales / Payment / Inventory / Procurement
                 │
                 ▼
            Domain Events
                 │
                 ▼
             Accounting
                 │
                 ▼
             Reporting
```

---

# 18. Integration rule

A useful review question for every implementation change:

> **Which domain owns this fact, and why is another domain allowed to know it?**

If the answer is "because both modules use the same DynamoDB table/item", the boundary is probably wrong.
