# Frontend delivery conventions

The Storefront and Backoffice use the internal `@commerceos/frontend-foundation` workspace package. It provides small, shared building blocks and does not contain business behavior, authentication, or tenant authority.

## Routes and features

- Add a route definition in the owning app's `App.tsx` until a feature group justifies a local route module.
- Put feature API DTOs, request functions and UI components under `src/features/<feature-name>/` in the owning app.
- Public storefront routes carry a storefront slug. Merchant routes never select a Tenant by client-supplied ID. A later authenticated-session task owns trusted merchant Tenant selection.
- Keep Platform Admin routes and merchant routes separate. Presentation guards only hide unavailable navigation or actions. The server is always the authorization authority.

## API requests and errors

- Read `VITE_COMMERCEOS_API_BASE_URL` from the app configuration. It may be empty for same-origin API delivery. Do not hard-code LocalStack, AWS, ports or credentials in frontend source.
- Create requests with `apiClient` from `src/config.ts` or a feature-local client built with `createApiClient`.
- The client maps RFC 7807 responses into `ApiError`. Render validation, unauthenticated, forbidden, not-found, revision conflict and service-unavailable states explicitly. Do not convert authorization errors to an empty list.
- Pass an `AbortSignal` to a request that should stop when its screen is left.

## Commands and list data

- Create one `MutationAttempt` per user submission and reuse its idempotency key for a retry of that same attempt. A fresh user action gets a fresh attempt.
- Commands that require optimistic concurrency pass the server revision through `expectedRevision`. A 409 or 412 must show a refresh-and-retry path, never overwrite silently.
- Use `CursorPage`, `appendCursorPage` and `canLoadMore` for cursor-based lists. Cursor state is a delivery concern, not a permission or business rule.

## UI and tests

- Use `AppShell`, `PageState`, `AlertPanel`, `FormField`, `Dialog`, `StatusBadge` and `LoadMore` before creating a feature-specific equivalent.
- All inputs have visible labels. Dialogs must support Escape and an explicit close action. Keep the skip link, focus styles and responsive layout intact.
- Test API mapping and helper behavior in the foundation package. Add feature-level tests for success, loading, empty, validation, forbidden, conflict and unavailable states as each screen is introduced.
- Do not log ProblemDetails bodies, Customer PII, invitation credentials, tenant identifiers used for authorization, payment data, or Audit evidence to the browser console.
