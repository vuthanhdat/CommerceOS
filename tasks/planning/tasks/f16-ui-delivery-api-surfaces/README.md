# F16 — UI Delivery & API Surfaces

Canonical feature plan: `tasks/planning/plans/f16-ui-delivery-api-surfaces.md`.

Tasks `TASK-0244` through `TASK-0264` convert the implemented CommerceOS capabilities into Storefront, Merchant Backoffice and Platform Admin user journeys with explicit HTTP/read-model delivery surfaces.

Do not treat browser state, client `tenantId`, client prices/totals, or UI composition as business authority. Preserve owner-module contracts and LocalStack-only runtime rules.