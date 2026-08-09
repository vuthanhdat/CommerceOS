# CommerceOS — Product Definition & Functional Scope

## 1. Product statement

CommerceOS is a **multi-tenant SaaS platform for merchants** that connects online selling, back-office operations, inventory, purchasing, customers, lightweight accounting, reporting, and external product-data ingestion in one system.

A merchant should be able to register a business, create or import a catalog, publish products to a storefront, receive customer orders, process mock payments, reserve and move inventory, purchase goods from suppliers, and see the resulting operational and accounting records without re-entering the same transaction in multiple modules.

The central product idea is:

> **Operational business activity should generate the data required by downstream domains.**

Examples:

- A completed sale changes inventory and creates accounting entries.
- A goods receipt increases stock and may create an accounts-payable obligation.
- A refund reverses payment, order, inventory, and accounting effects through explicit business events.
- A product-data crawler produces source snapshots that a merchant may review and import into its canonical catalog.

---

## 2. Primary users

### 2.1 Platform administrator

Operates CommerceOS itself.

Functions:

- view tenants and platform health;
- suspend/reactivate tenants;
- inspect failed jobs, DLQs, workflow failures, and crawler status;
- manage shared platform configuration;
- view aggregate usage and cost indicators;
- inspect audit records for operational support.

### 2.2 Merchant owner

Owns a tenant/business.

Functions:

- company setup;
- invite employees;
- assign roles;
- configure storefront;
- configure accounting defaults;
- see sales, inventory, cash, receivables, payables, and business dashboards;
- approve sensitive business actions where required.

### 2.3 Sales staff

Functions:

- manage customers;
- create and update sales orders;
- inspect order status;
- apply permitted discounts;
- initiate payment through the mock payment service;
- cancel or refund according to permissions.

### 2.4 Warehouse staff

Functions:

- see stock by product/warehouse;
- receive purchased goods;
- reserve/release inventory;
- fulfill orders;
- perform stock adjustments;
- review stock movement history.

### 2.5 Accountant

Functions:

- view chart of accounts;
- inspect system-generated journal entries;
- create permitted manual journals;
- post and reverse journals;
- inspect general ledger and trial balance;
- review receivables/payables;
- inspect source business transaction for each accounting entry.

### 2.6 Shopper/customer

Functions:

- browse a tenant's storefront;
- search/filter products;
- see product details;
- add/remove cart items;
- checkout;
- use mock payment methods;
- see order confirmation/status;
- optionally create a customer account later.

Guest checkout is preferred initially so Cognito is primarily used for merchant staff rather than every anonymous shopper.

---

## 3. Tenant model

Each merchant is a tenant.

Example:

```text
CommerceOS
├── ABC Computer
│   ├── Owner
│   ├── Sales
│   ├── Warehouse
│   └── Accountant
├── XYZ Fashion
└── Book Store
```

Every tenant-owned business entity must carry an immutable tenant context. A client-provided `tenantId` is never sufficient authorization by itself; the trusted tenant context must be derived from authenticated identity and authorization rules.

Initial tenant roles:

- `Owner`
- `Admin`
- `Sales`
- `Warehouse`
- `Accountant`
- `Viewer`

Future role design should move toward permission claims rather than hard-coded role checks.

---

## 4. Functional capabilities

## 4.1 Tenant & company administration

- tenant registration;
- business profile;
- storefront slug/subdomain configuration;
- staff invitation;
- membership activation/deactivation;
- role and permission assignment;
- tenant status and plan metadata;
- basic tenant settings;
- audit history for privileged changes.

## 4.2 Product catalog

- create/edit/archive product;
- category and brand;
- SKU;
- product name and description;
- selling price;
- cost reference;
- product images;
- attributes/specifications;
- publish/unpublish;
- product variants in a later phase;
- map canonical product to external crawled source records;
- import selected fields from crawler snapshots;
- preserve merchant overrides when source data changes.

## 4.3 Storefront

Each tenant gets a public storefront such as:

```text
/{tenantSlug}
/{tenantSlug}/products/{productSlug}
```

Future deployment may support custom merchant domains.

Functions:

- public catalog listing;
- category/filter/search;
- product detail;
- availability indicator;
- cart;
- checkout;
- order confirmation;
- basic storefront configuration;
- responsive web UI;
- static assets and images delivered through CDN.

## 4.4 Cart & checkout

- add/remove/update quantity;
- price snapshot at checkout;
- validate product publish status;
- validate inventory availability;
- calculate subtotal/discount/total;
- create order;
- create mock payment intent;
- tolerate payment timeout and delayed confirmation;
- protect checkout with idempotency key;
- prevent duplicate order creation from repeated browser requests.

## 4.5 Sales order management

Initial order lifecycle:

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

Exceptional paths:

```text
Cancelled
PaymentFailed
PartiallyRefunded
Refunded
```

Functions:

- create sales order;
- view order;
- order history;
- reserve inventory;
- confirm mock payment;
- fulfill;
- cancel;
- refund;
- release stock after cancellation;
- emit domain events for downstream processing.

## 4.6 Inventory

- stock on hand;
- stock reserved;
- stock available;
- warehouse/location;
- stock receipt;
- stock reservation;
- stock release;
- stock issue/fulfillment;
- stock adjustment;
- stock movement ledger;
- low-stock indicator;
- inventory valuation input based on product cost snapshots.

Initial implementation may use a single warehouse per tenant while keeping the model multi-warehouse capable.

## 4.7 Procurement

- supplier master;
- purchase order;
- purchase-order lines;
- purchase status;
- goods receipt;
- partial goods receipt later;
- supplier invoice reference;
- supplier payment status;
- connection from purchase transactions to inventory and accounting.

Initial lifecycle:

```text
Draft PO
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

## 4.8 Customer management

- customer profile;
- contact information;
- order history;
- total spend projection;
- receivable balance where applicable;
- customer notes later;
- no unnecessary sensitive personal data.

## 4.9 Mock payment

No real money is processed.

Capabilities:

- create payment intent;
- authorize;
- capture;
- decline;
- timeout;
- delayed success;
- refund;
- payment status query;
- signed webhook simulation;
- duplicate webhook simulation;
- retry behavior;
- idempotency enforcement;
- deterministic test scenarios.

See `06-mock-payment-provider.md`.

## 4.10 Accounting

Initial bookkeeping scope:

- chart of accounts;
- journal entry;
- journal lines;
- debit/credit validation;
- journal posting;
- immutable posted journals;
- journal reversal rather than destructive edit;
- business-transaction reference;
- general ledger;
- trial balance;
- accounts receivable projection;
- accounts payable projection;
- simple profit-and-loss projection later;
- simple balance-sheet projection later.

System-generated postings initially cover:

- sale recognized;
- inventory/COGS movement;
- purchase receipt/invoice;
- customer refund;
- supplier payment;
- selected inventory adjustment cases.

Out of initial scope:

- statutory Vietnamese tax filing;
- legally compliant e-invoices;
- payroll;
- fixed-asset depreciation;
- certified jurisdiction-specific accounting compliance.

## 4.11 Pricing & promotion

Initial scope:

- base selling price;
- manual discount with authorization rule;
- simple scheduled promotion later.

Later:

- coupons;
- promotion rules;
- price lists;
- customer segments;
- flash sale using scheduled events.

## 4.12 Reporting & dashboards

Merchant dashboard should derive from real transactional data.

Examples:

- revenue;
- order count;
- average order value;
- gross profit projection;
- cash balance projection;
- accounts receivable;
- accounts payable;
- inventory value;
- low-stock products;
- top products;
- failed payments;
- failed business workflows.

Platform dashboard:

- active tenants;
- active merchant users;
- API volume;
- Lambda volume/duration;
- event volume;
- queue backlog;
- DLQ count;
- workflow failure count;
- crawler success/failure;
- estimated AWS cost indicators.

## 4.13 Product-data ingestion

External product sources are used to bootstrap and enrich catalog data.

Initial desired adapters:

- Amazon;
- The Gioi Di Dong;
- Dien May Xanh;
- CellphoneS;
- additional sources via plug-in adapter contract.

Functions:

- source registry;
- crawl scheduling;
- source-specific rate limit;
- fetch raw public product page/API response where permitted;
- save source snapshot;
- normalize product fields;
- deduplicate candidate products;
- merchant review/import;
- source attribution;
- historical price snapshot;
- crawl failure/retry/DLQ.

A crawled record is **not** the canonical merchant product. The source record is immutable evidence/snapshot; the merchant imports or maps it into a canonical product it owns.

## 4.14 Notification

Initial:

- in-app operational notifications;
- email optional later.

Events of interest:

- order created;
- payment failed;
- payment delayed;
- low stock;
- purchase goods received;
- workflow failed;
- crawler failed;
- journal posting rejected;
- DLQ message detected.

## 4.15 Audit

Audit records for security/operational actions:

- actor;
- tenant;
- action;
- entity;
- entity id;
- timestamp;
- correlation id;
- before/after summary where safe;
- source IP/user agent when appropriate;
- no secrets/card data.

---

## 5. Business event examples

```text
TenantCreated
StaffInvited
ProductPublished
ProductImported
CartCheckedOut
OrderPlaced
OrderConfirmed
OrderCancelled
OrderFulfilled
OrderReturned
PaymentRequested
PaymentAuthorized
PaymentCaptured
PaymentFailed
PaymentRefunded
StockReserved
StockReleased
StockReceived
StockIssued
StockAdjusted
PurchaseOrderCreated
GoodsReceived
SupplierInvoiceRecorded
SupplierPaid
JournalPosted
JournalReversed
ProductSourceCrawled
ProductSourceChanged
```

Events are business facts, not generic database change notifications.

---

## 6. MVP definition

The first end-to-end useful slice should include:

1. tenant onboarding;
2. merchant staff authentication;
3. role-based access;
4. catalog CRUD;
5. one external catalog-source adapter;
6. merchant review/import from crawled source;
7. storefront product list/detail;
8. cart and checkout;
9. mock payment success/failure/timeout;
10. sales order lifecycle;
11. single-warehouse inventory;
12. stock reservation and fulfillment;
13. basic supplier and purchase order;
14. goods receipt;
15. basic chart of accounts;
16. automatic journal creation for sale and inventory movement;
17. general ledger and trial balance;
18. CloudWatch logging/metrics;
19. retry/DLQ for at least one asynchronous workflow;
20. Infrastructure as Code.

The MVP is intentionally a vertical business slice, not a collection of disconnected AWS demonstrations.
