# Tenant Management & Merchant Access Domain Baseline

_Deep baseline for the first delivery frontier. Reconciled by TASK-0087._

## 1. Boundary and language

CommerceOS separates three ideas that were previously grouped under “Tenant & Identity”:

1. **Tenant Management** owns the merchant business account and its business profile.
2. **Merchant Access** owns invitations, tenant memberships, and initial role policy.
3. An **authentication authority** proves a person's external identity. It does not own Tenant or Membership and does not grant tenant authority on its own.

These are business boundaries. TASK-0088 decides whether they share or separate implementation modules.

```text
Authenticated subject
        │ proves identity only
        ▼
Merchant Access ── active membership + approved capabilities ──► Trusted tenant authority
        │                                           │
        └──────── membership belongs to ────────────┘
                                                    ▼
                                            Tenant Management
```

## 2. Tenant Management

### Responsibility

Tenant Management owns whether a merchant business account exists, its immutable tenant identity, its operating status, and the profile the merchant maintains about that business.

### Aggregate: Tenant

`Tenant` is the aggregate root.

Owned concepts:

- `TenantId` — immutable, system-assigned identity;
- `TenantStatus` — current business-account availability;
- `BusinessProfile` — the tenant's single owned profile;
- tenant creation and status history sufficient to explain current state.

`BusinessProfile` is owned by Tenant for the initial product. It is not a free-standing cross-tenant record. Candidate fields such as display name, legal/trading name, locale, contact details, and functional currency must follow `PD-002` and `PD-034`; a Builder must not infer jurisdictional requirements.

### Tenant states

The baseline recognizes:

```text
Active  ──Suspend──► Suspended
   ▲                    │
   └────Reactivate──────┘
```

- A successful initial onboarding outcome creates an `Active` Tenant.
- `Suspended` means ordinary merchant and public commerce activity is unavailable. Exact read-only/support behavior is a pending product decision (`PD-004`).
- Deletion, closure, trial, plan, and billing states are not defined by the current product baseline. They must not be invented as Tenant states.
- A failed registration does not leave a business-visible partial Tenant.

### Tenant invariants

1. `TenantId` is immutable and never supplied as the authority for registration.
2. A Tenant owns exactly one current BusinessProfile.
3. BusinessProfile changes cannot move the profile to another Tenant.
4. An Active Tenant has at least one Active Owner Membership.
5. Suspension does not erase memberships or historical tenant-owned transactions.
6. Reactivation restores tenant status but does not silently reactivate a Membership that was separately disabled.
7. Retrying the same accepted onboarding intent returns/references the same logical Tenant; it does not create another tenant accidentally.

### Onboarding business outcome

The merchant-facing registration promise is one complete outcome:

```text
approved initial Owner binding under PD-034
        + valid business profile
        + accepted registration intent
                    ↓
Active Tenant + Active initial Owner Membership
```

The Tenant and initial Owner Membership have different owners, but the business must never report onboarding as successful while leaving an Active Tenant with no Active Owner.

This resolves a contradiction in the candidate task graph: TASK-0006 excludes membership, TASK-0007 requires an active membership, and TASK-0008 creates memberships after TASK-0007. The Backlog Planner and Technical Architect must reshape that dependency/consistency boundary; a Builder may not bootstrap authority with a client-provided TenantId or a temporary unowned tenant.

### Tenant commands and facts

Business command candidates:

- `RegisterMerchantTenant`
- `UpdateBusinessProfile`
- `SuspendTenant`
- `ReactivateTenant`

Owned fact candidates:

- `MerchantTenantRegistered`
- `BusinessProfileChanged`
- `TenantSuspended`
- `TenantReactivated`

`TenantCreated` is acceptable only if its contract means the completed merchant-tenant registration fact, not an intermediate persistence action.

## 3. Merchant Access

### Responsibility

Merchant Access answers:

> For this authenticated subject, in this explicitly selected tenant context, is there an active membership and which approved capabilities apply now?

It does not authenticate passwords or tokens. It does not own business aggregates in Catalog, Sales, Inventory, Procurement, Payments, or Accounting.

### Aggregate: Membership

`Membership` is the aggregate root for a subject's participation in one Tenant.

Owned concepts:

- immutable `MembershipId`;
- immutable owning `TenantId`;
- immutable external `SubjectId` binding once activated;
- current `MembershipStatus`;
- current assigned role identifier(s), with cardinality governed by `PD-003`;
- activation, disablement, reactivation, and role-change history.

Initial uniqueness invariant:

> There is at most one Membership for the same `(TenantId, SubjectId)` pair.

The product decision about whether one subject may belong to several tenants is deliberately pending (`PD-001`). Nothing may assume that a subject identifier alone determines one tenant.

### Membership states

```text
Active ──Disable──► Disabled
   ▲                   │
   └────Reactivate─────┘
```

- `Active` is required for ordinary protected tenant work.
- `Disabled` denies protected work even when the authentication credential remains otherwise valid.
- Disabling a Membership does not delete the person's authored business history or audit evidence.
- Reactivation is explicit; authentication alone never reactivates it.

### Aggregate: Invitation

`Invitation` is a separate aggregate root because it has identity, expiry, recipient binding, and a lifecycle before a Membership exists.

Owned concepts:

- immutable Invitation identity and Tenant ownership;
- intended recipient identifier/contact value;
- proposed initial role assignment, with cardinality governed by `PD-003`;
- inviter identity;
- expiry;
- acceptance/revocation history.

Invitation states:

```text
                 ┌────Accept────► Accepted
Pending ─────────┼────Revoke────► Revoked
                 └────Expire────► Expired
```

`Accepted`, `Revoked`, and `Expired` are terminal for that invitation.

Invitation invariants:

1. Only an authorized Active Membership in the same Tenant may issue or revoke an invitation.
2. The accepting authenticated subject must satisfy the human-approved recipient-binding rule in `PD-036`; no email/subject matching default is implied.
3. One invitation can activate at most one Membership.
4. Acceptance after expiry/revocation/previous acceptance is rejected deterministically and creates no additional Membership.
5. Invitation acceptance cannot create or modify a Membership in another Tenant.
6. An invitation to an already Active member is a conflict, not a second Membership.
7. Reactivating an existing Disabled Membership is a separate privileged decision; accepting a new invitation does not silently bypass disablement.

How an invitation is delivered is a Notification concern and may be out-of-band initially. Delivery failure does not change the fact that Merchant Access accepted or rejected the invitation command.

### Initial role vocabulary

Roles are named groupings of business capabilities. Whether one Membership initially holds exactly one role or a set of roles, and the complete role-to-capability mapping, are human product decision `PD-003`; the plural product wording and singular candidate-task wording conflict, so neither is treated as approved. Later permission-granular authorization may replace the role mapping without changing Membership identity.

The following names carry only the directional persona intent already present in the product definition. This table is not an authorization matrix:

| Role | Directional persona intent |
|---|---|
| Owner | accountable tenant owner and ownership-sensitive actor |
| Admin | tenant-administration persona whose exact staff/domain authority is pending |
| Sales | customer/order work subject to explicit capability grants |
| Warehouse | inventory/receiving/fulfillment work subject to explicit capability grants |
| Accountant | accounting and financial-report work subject to explicit sensitive-action grants |
| Viewer | read-only persona; exact readable data still requires explicit policy |

The UI may hide unavailable actions, but the business authorization decision remains authoritative outside the UI.

`tenant.profile.manage`, staff administration, Catalog management/publication, discounts, refunds, inventory adjustment, journal posting/reversal, and other sensitive capabilities all require the approved `PD-003` matrix plus domain refinements. A role name alone does not authorize an operation, and a generic “Admin can do everything” rule is not part of this baseline.

### Ownership and last-owner invariants

1. An Active Tenant must retain at least one Active Owner Membership.
2. The last Active Owner cannot be disabled, demoted, or removed.
3. An Owner cannot transfer away the last ownership claim unless another Active Owner exists as part of the accepted business outcome.
4. A Membership cannot change Tenant.
5. A role-assignment change never changes the authenticated subject binding.
6. Self-disable/self-demotion is subject to the same last-owner rule.

### Merchant Access commands and facts

Business command candidates:

- `InviteStaffMember`
- `AcceptStaffInvitation`
- `RevokeStaffInvitation`
- `DisableMembership`
- `ReactivateMembership`
- `ChangeMembershipRoleAssignment`
- `ResolveTenantAuthority`

Owned fact candidates:

- `StaffInvitationIssued`
- `StaffInvitationAccepted`
- `StaffInvitationRevoked`
- `StaffInvitationExpired`
- `MembershipActivated`
- `MembershipDisabled`
- `MembershipReactivated`
- `MembershipRolesChanged`

`StaffJoined` should mean Membership activation, not merely successful authentication.

## 4. Trusted tenant authority

An accepted trusted context contains, conceptually:

- authenticated Subject identity;
- selected Tenant identity;
- active Membership identity;
- current role/capability decision;
- whether the actor uses an explicitly modeled platform-administration path;
- correlation information for auditability.

This is a business trust statement, not a token schema or application type.

Rules:

1. Request body, route, query, or header data may identify a target but cannot replace selected trusted tenant authority.
2. A protected tenant operation requires the target aggregate's TenantId to equal the trusted TenantId.
3. Missing, expired, invalid, or otherwise unauthenticated identity is different from authenticated identity with no Active Membership; both deny access without exposing tenant data.
4. Membership status/role changes take effect on subsequent authorization resolution even if an older authentication credential is still cryptographically valid.
5. Platform-admin access is a separate, explicit, audited path and is not an Owner Membership in every tenant.

## 5. Business error semantics

| Code | Meaning and required effect |
|---|---|
| `TENANT_REGISTRATION_CONFLICT` | the same uniqueness claim represents a different tenant intent; no second tenant is created |
| `TENANT_SUSPENDED` | ordinary operation is blocked by tenant status |
| `MEMBERSHIP_REQUIRED` | authenticated subject has no applicable Membership; tenant existence is not disclosed |
| `MEMBERSHIP_INACTIVE` | applicable Membership exists but is Disabled |
| `MEMBERSHIP_TENANT_MISMATCH` | target and trusted tenant differ; response remains non-disclosing |
| `INVITATION_EXPIRED` | acceptance deadline passed; no Membership is created/reactivated |
| `INVITATION_NOT_ACCEPTABLE` | invitation is accepted/revoked/recipient-mismatched or otherwise terminal |
| `MEMBERSHIP_ALREADY_EXISTS` | same Tenant and Subject already have a Membership |
| `LAST_OWNER_REQUIRED` | change would leave an Active Tenant without an Active Owner |
| `ROLE_ASSIGNMENT_FORBIDDEN` | actor may not assign or target the requested role |
| `STALE_MEMBERSHIP_REVISION` | concurrent change won; no role/status update is lost |

Transport status, exception form, and information-disclosure mapping belong to technical architecture.

## 6. Audit relationship for privileged access changes

Audit owns append-oriented evidence; Tenant Management or Merchant Access still owns the action and result.

Minimum audit evidence for a covered action contains conceptually:

- actor Subject and Membership identity, or explicit platform-admin actor path;
- trusted actor Tenant;
- action and target type/identity where safe;
- accepted/rejected outcome and safe reason category;
- occurrence time and correlation identity;
- safe before/after summary where needed, excluding invitation secrets and sensitive inputs.

Tenant status/profile changes, invitation issuance/revocation, Membership activation/disablement/reactivation, and role changes are privileged action families. Exact rejected-attempt coverage and tenant-visible readers/details remain `PD-033`.

Audit records are immutable evidence, not domain events. An action designated auditable must not report completed success while silently omitting its required evidence; TASK-0088 decides the consistency/recovery mechanism.

## 7. Inputs to TASK-0088 and TASK-0089

Technical Architecture must preserve:

- the separation between external authentication and Membership authority;
- the onboarding all-or-honestly-incomplete business outcome;
- fresh-enough Membership status for disablement to deny access;
- non-disclosing cross-tenant behavior;
- last-owner and invitation single-acceptance invariants;
- explicit platform-admin separation.

Backlog Planning must reconcile:

- TASK-0006/0007/0008's circular owner bootstrap;
- any task that treats a token claim or client TenantId as sufficient authority;
- any task that converts a role name into authority without the approved capability matrix;
- task readiness against pending `PD-001` through `PD-004`, `PD-033`, `PD-034`, and `PD-036` at their stated decision gates.
