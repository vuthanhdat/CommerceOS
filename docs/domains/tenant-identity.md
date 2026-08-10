# Tenant Management & Merchant Access Domain Baseline

_Reconciled after the 2026-08-10 human product-decision pass. This document incorporates approved decisions `PD-001`, `PD-002`, `PD-003`, `PD-004`, `PD-033`, `PD-034`, `PD-036`, and the relevant Subscription & Billing decisions._

## 1. Boundary and language

CommerceOS keeps four business authorities separate:

1. **Tenant Management** owns the merchant business account, Business Profile, and `TenantStatus`.
2. **Merchant Access** owns Invitations, Memberships, Membership status, and merchant roles.
3. The external authentication authority proves a person's identity. Authentication alone never grants tenant authority.
4. **Subscription & Billing** owns trial/paid subscription state and effective entitlements. Subscription state never becomes Tenant or Membership state by implication.

These are business boundaries only. This document does not choose modules, storage, transports, or cloud services.

## 2. Tenant Management

### Aggregate: Tenant

`Tenant` is the aggregate root.

Owned concepts:

- immutable `TenantId`;
- `TenantStatus`;
- exactly one current `BusinessProfile`;
- status/profile history sufficient to explain the current business state.

### Business Profile

For MVP, successful self-service registration requires:

- merchant display name;
- explicit IANA business timezone.

The initial identity must be authenticated with a verified email. No legal/tax identifier, business-name uniqueness rule, or cross-tenant duplicate-business detection is required in MVP (`PD-034`).

CommerceOS MVP is VND-only (`PD-002`). Money still carries an explicit currency; the Business Profile does not choose another functional currency in MVP.

### Tenant lifecycle

```text
Active ──Suspend──► Suspended
   ▲                    │
   └────Reactivate──────┘
```

Rules:

1. successful onboarding creates an `Active` Tenant;
2. while Suspended, ordinary merchant mutations and public commerce are denied;
3. suspension does not delete Tenant data, Memberships, Subscription, Orders, accounting history, or other evidence;
4. reactivation restores Tenant eligibility only; it does not reactivate a separately Disabled Membership or ended Subscription;
5. Tenant closure, deletion, retention windows, recovery windows, and legal/privacy erasure semantics remain deferred under `PD-004`;
6. exact support/read-only behavior of a Suspended Tenant must not be invented beyond the approved safe constraint.

### Tenant invariants

- `TenantId` is immutable and never becomes authorization merely because it appears in client input.
- An Active Tenant always has at least one Active Owner Membership.
- Tenant suspension never rewrites another context's lifecycle.
- Registration retry is idempotent for the same logical onboarding intent.
- Tenant name or external SubjectId is not a global real-business uniqueness key.

## 3. Merchant onboarding business outcome

MVP registration is open self-service for an authenticated identity with a verified email (`PD-034`). The identity that successfully initiates registration becomes the initial Active Owner.

`PD-043` additionally requires successful Tenant registration to start a **30-day Trial subscription** with dedicated Trial terms/Entitlements and no payment method.

The merchant-facing onboarding outcome is therefore:

```text
verified authenticated identity
        + valid Business Profile
        + accepted registration intent
                    ↓
        Active Tenant
        + Active initial Owner Membership
        + 30-day Trial Subscription / Trial EntitlementSet
```

The three resulting facts have different bounded-context owners. No single context may claim ownership of all three. The product must not report onboarding as complete while knowingly leaving one of the required accepted outcomes absent; the technical consistency/recovery mechanism is a Technical Architect concern.

## 4. Membership model

### Aggregate: Membership

`Membership` is the aggregate root for one authenticated subject's participation in one Tenant.

Owned concepts:

- immutable `MembershipId`;
- immutable owning `TenantId`;
- immutable external `SubjectId` binding after activation;
- exactly one current role in MVP;
- `MembershipStatus`;
- activation/disable/reactivation/role-change history.

Uniqueness invariant:

> At most one Membership exists for the same `(TenantId, SubjectId)` pair.

### Multi-tenant membership and tenant selection (`PD-001`)

One authenticated identity may have Memberships in multiple Tenants.

- If exactly one eligible Tenant exists, CommerceOS may select it automatically.
- If multiple eligible Tenants exist, the person must intentionally select the active Tenant.
- Every protected operation resolves trusted server-validated Tenant context from the selected Tenant plus an eligible Membership.
- `SubjectId` alone does not determine Tenant.
- A client-supplied `tenantId` is never authority by itself.

### Membership lifecycle

```text
Active ──Disable──► Disabled
   ▲                   │
   └────Reactivate─────┘
```

- Active is required for ordinary protected tenant work.
- Disabled denies ordinary protected work even if authentication remains valid.
- Reactivation is explicit.
- Subscription downgrade, cancellation, expiry, or delinquency does not directly disable a Membership.

## 5. MVP roles and authority (`PD-003`)

A Membership has **exactly one role at a time** in MVP.

Initial roles:

| Role | Approved MVP meaning |
|---|---|
| Owner | Full merchant administration authority. May manage ordinary operations, staff, Catalog, and ownership-sensitive administration, subject to owning-domain invariants. |
| Admin | May manage ordinary merchant operations, staff, and Catalog. May not grant/revoke Owner authority or remove/demote the last Active Owner. |
| Staff | May perform ordinary operational work only where the owning domain explicitly allows Staff. May not manage Memberships, roles, or tenant-administration concerns. |
| Viewer | Read-only. No mutating merchant operation. |

Additional rules:

- A Tenant may have multiple Owners.
- An Active Tenant must retain at least one Active Owner.
- Catalog administration and publication require Owner or Admin in MVP.
- A generic role name never bypasses a more specific domain invariant.
- Future permission-granular/custom RBAC is not part of MVP.

### Last-owner invariants

1. the last Active Owner cannot be disabled, demoted, or removed;
2. self-disable/self-demotion is subject to the same rule;
3. a Subscription limit may never automatically disable an Owner or other Membership to force compliance;
4. a Membership cannot move to another Tenant;
5. role change never changes authenticated-subject binding.

## 6. Invitations (`PD-036`)

`Invitation` is a separate Merchant Access aggregate root.

Invitation rules:

- Tenant-bound and addressed to one normalized email;
- acceptance requires an authenticated identity whose **verified email** matches the invitation recipient;
- at most one active Pending Invitation exists for the same Tenant + normalized recipient email;
- resend rotates/reissues the invitation credential and invalidates the prior credential rather than creating another independent invitation;
- invitation credential expires after **7 days** and is single-use;
- accepting for an already Active member is a harmless already-member result;
- an existing Disabled Membership is not silently reactivated; authorized reactivation is a separate Membership action;
- one Invitation may activate at most one Membership;
- acceptance may not cross Tenant boundaries;
- delivery mechanics remain a Notification concern.

Invitation lifecycle:

```text
                 ┌────Accept────► Accepted
Pending ─────────┼────Revoke────► Revoked
                 └────Expire────► Expired
```

Accepted, Revoked, and Expired are terminal for that invitation credential/history.

## 7. Subscription-entitlement interaction

Membership authorization and subscription entitlement are independent eligibility dimensions.

Conceptually, an ordinary mutation may require:

```text
verified identity
+ trusted selected Tenant
+ Active Membership with sufficient role/domain authority
+ TenantStatus allows the operation
+ effective Subscription entitlement allows the capability
+ owning domain invariant accepts the change
```

### Staff-count limits (`PD-048`, `PD-050`)

Subscription & Billing may define `MaxActiveStaff` as a hard growth/activation limit, but Merchant Access remains authoritative for Membership identity/status and active-member count.

Rules:

- creating/activating another Membership may be rejected when authoritative current count plus the proposed activation would exceed the current trusted hard limit;
- existing Memberships are never automatically disabled because of a downgrade;
- if a scheduled downgrade target is below current usage, the downgrade remains `BlockedByUsage/RemediationRequired` until normal Merchant Access actions bring usage into compliance;
- remediation still must preserve the last-owner invariant;
- a stale dashboard/Reporting count cannot authorize a Membership write.

Subscription expiry/end may remove ordinary mutation entitlements while authenticated merchant read/history/export/recovery access remains available under `PD-043`/`PD-049`; it does not delete or disable Memberships.

## 8. Trusted tenant authority

An accepted trusted tenant context contains conceptually:

- authenticated Subject identity;
- intentionally selected/resolved Tenant identity;
- Active Membership identity;
- current single role and resulting approved capability decision;
- whether an explicitly modeled platform-administration path is being used;
- correlation identity for auditability.

Rules:

1. request body, route, query, or header data may identify a target but never grant tenant authority;
2. target aggregate Tenant ownership must match trusted Tenant context;
3. cross-tenant existence is not disclosed merely because an identifier is known;
4. Membership status/role changes apply on subsequent authorization resolution even if an older authentication credential remains cryptographically valid;
5. platform administration is a separate explicit path, not an Owner Membership in every Tenant;
6. client plan/entitlement claims never become subscription authority.

## 9. Privileged Audit relationship (`PD-033`)

Audit owns append-oriented evidence, while Tenant Management/Merchant Access own the business action.

MVP Audit covers successful and rejected privileged mutations, including:

- Tenant administration;
- Membership/role/security administration;
- security-significant tenant-isolation denials.

Tenant Audit is readable by Owner/Admin only. Tenant-visible denial evidence must never reveal another Tenant/entity's existence or identifiers. More sensitive cross-tenant investigation details, if ever retained, belong to protected platform-security evidence rather than merchant-visible Audit.

Audit acknowledgement/evidence is not a substitute for the underlying business fact.

## 10. Commands, queries, and owned facts

### Tenant Management commands

- `RegisterMerchantTenant`
- `UpdateBusinessProfile`
- `SuspendTenant`
- `ReactivateTenant`

### Tenant Management facts

- `MerchantTenantRegistered`
- `BusinessProfileChanged`
- `TenantSuspended`
- `TenantReactivated`

### Merchant Access commands

- `InviteStaffMember`
- `ResendStaffInvitation`
- `AcceptStaffInvitation`
- `RevokeStaffInvitation`
- `DisableMembership`
- `ReactivateMembership`
- `ChangeMembershipRole`
- `ResolveTenantAuthority`

### Merchant Access facts

- `StaffInvitationIssued`
- `StaffInvitationCredentialRotated`
- `StaffInvitationAccepted`
- `StaffInvitationRevoked`
- `StaffInvitationExpired`
- `MembershipActivated`
- `MembershipDisabled`
- `MembershipReactivated`
- `MembershipRoleChanged`

Fact names describe accepted business meaning, not transport/event schemas.

## 11. Business error semantics

| Outcome | Meaning |
|---|---|
| `TENANT_REGISTRATION_CONFLICT` | same logical registration identity is reused incompatibly; no second logical Tenant is created |
| `TENANT_SUSPENDED` | Tenant status blocks ordinary operation |
| `MEMBERSHIP_REQUIRED` | authenticated subject has no eligible Membership in the selected Tenant context |
| `MEMBERSHIP_INACTIVE` | applicable Membership exists but is Disabled |
| `MEMBERSHIP_TENANT_MISMATCH` | target and trusted Tenant differ; response remains non-disclosing |
| `TENANT_SELECTION_REQUIRED` | more than one eligible Tenant exists and the actor has not intentionally selected one |
| `INVITATION_EXPIRED` | 7-day acceptance window passed |
| `INVITATION_NOT_ACCEPTABLE` | invitation is terminal, recipient mismatched, or otherwise not eligible |
| `MEMBERSHIP_ALREADY_EXISTS` | Active Membership already exists for the Tenant/Subject |
| `LAST_OWNER_REQUIRED` | requested change would leave an Active Tenant without an Active Owner |
| `ROLE_ASSIGNMENT_FORBIDDEN` | actor may not perform the requested role/ownership change |
| `MEMBERSHIP_ENTITLEMENT_LIMIT_REACHED` | current trusted hard staff limit rejects growth/activation; existing Memberships remain unchanged |
| `STALE_MEMBERSHIP_REVISION` | concurrent Membership state won; newer accepted state is preserved |

Transport mapping belongs to Technical Architecture.

## 12. Remaining human product decision

Only this Tenant/Access product-policy area remains intentionally unresolved:

- `PD-004` — exact Suspended-tenant read/support behavior plus closure/deletion/retention/recovery/privacy semantics.

Its approved interim constraint is already encoded above and must not be expanded by implementation guesswork.

## 13. Downstream reconciliation handoff

### Technical Architect

Reconcile the already-completed technical baseline against these newly approved business semantics, especially:

- multi-Tenant Membership selection and trusted Tenant resolution;
- one-role `Owner/Admin/Staff/Viewer` authority model;
- invitation verified-email binding, 7-day expiry, single active pending invitation, and resend rotation;
- onboarding spanning Tenant + initial Owner + automatic 30-day Trial without collapsing bounded-context ownership;
- fresh-enough Membership/entitlement checks for hard growth limits;
- non-disclosing tenant-isolation and Audit requirements.

Do not resolve `PD-004` through API, persistence, or infrastructure convenience.

### Backlog Planner

Reconcile candidate tasks so Builders no longer see obsolete pending assumptions for `PD-001`, `PD-002`, `PD-003`, `PD-034`, or `PD-036`. Keep any closure/privacy/suspension-detail work gated by `PD-004`.

**Stop condition: DOMAIN BASELINE READY for approved Tenant/Merchant Access MVP semantics; HUMAN PRODUCT DECISION REQUIRED only where `PD-004` is explicitly reached.**
