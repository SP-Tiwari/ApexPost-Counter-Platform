# ADR 0008: HTTP pipeline order

- Status: Accepted
- Date: 2026-10-25 (updated 2026-10-29: CORS, admin IP restriction, partner limit, endpoint metadata, headers via `OnStarting`)

## Context

Protections run at different points in a request. Correlation IDs must exist before errors are produced. Security headers must reach every response. Endpoint metadata (`[RequireApiKey]`, `[RequireAdminNetwork]`) exists only after routing. Cheap rejections should happen before authentication and MVC.

## Decision

Keep this order in `src/ApexPost.Web/Program.cs`:

1. `CorrelationIdMiddleware`, which also does request logging and `try/finally` timing
2. `UseExceptionHandler` (Problem Details)
3. `UseHsts` (outside Development)
4. `SecurityHeadersMiddleware`, with headers set in `Response.OnStarting`
5. `MaintenanceModeMiddleware` (503 with Retry-After; `/health` exempt)
6. `UseHttpsRedirection`
7. `UseRouting`
8. `UseCors("ParcelTracking")`
9. `ApiKeyAndIpRestrictionMiddleware`
10. `AdminIpRestrictionMiddleware`
11. `PartnerRateLimitingMiddleware`
12. `UseRateLimiter` (sign-in policy)
13. `UseAuthentication`
14. `UseAuthorization`
15. Endpoints (`/health`, static assets, MVC)

The reason for each position is in DESIGN.md §5. API keys and allowlists are deployment configuration, never source values. An empty allowlist disables that check.

## Alternatives considered

- **Security headers set directly in middleware (before `next`).** Headers can be lost when the exception handler clears the response; `OnStarting` applies them to whatever response is finally sent.
- **API-key protection by URL prefix only (the earlier design).** It is fragile: a route rename silently unprotects an endpoint. Endpoint metadata is now primary, and prefixes remain as an option.
- **API-key check as an authorization handler or MVC filter.** It would run after authentication and only for MVC. Middleware rejects earlier and also covers non-MVC endpoints.
- **Partner rate limit before the key check.** Unauthenticated callers could then exhaust a partner's quota.

## Consequences

Error responses carry correlation IDs and security headers. Endpoint-aware middleware sees the routed endpoint. Behind a reverse proxy, IP checks and sign-in rate partitions see the proxy's address until trusted forwarded-header handling is configured. CORS is registered globally, so the tracking-widget origin is also allowed on `/health`. That is harmless, but broader than needed.

Relevant implementation: `src/ApexPost.Web/Program.cs` and `src/ApexPost.Web/Middleware/`.
