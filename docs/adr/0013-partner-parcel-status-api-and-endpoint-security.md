# ADR 0013: Partner parcel-status API and endpoint security

- Status: Accepted
- Date: 2026-10-29

## Context

Courier partners need `/integration/parcel-status`. Without it, the API-key, IP, CORS and partner-limit protections guarded nothing. Protection was configured by URL prefix rather than endpoint metadata, admin pages had no IP restriction, and there were no named policies. The shipping model has no tracking-status vocabulary, and none may be invented.

## Decision

- **Endpoint:** `GET /integration/parcel-status/{shipmentId}` on `IntegrationController` (`[ApiController]`, `[AllowAnonymous]`, `[RequireApiKey]`).
  - The ID must match `^[A-Za-z0-9-]{1,64}$`; otherwise the response is 400.
  - An unknown ID returns 404.
  - The response combines **stored facts** (type, service level, destination zone, accepted-at time, and the existing transaction status Paid/Voided/Refunded) with the **simulated courier result** (configured status text, `TimedOut`, `Failed` or `NotConfigured`). This option was chosen by the author.
  - It never includes prices, staff, sender, recipient or identity data.
- **API key:** `ApiKeyAndIpRestrictionMiddleware` protects any endpoint carrying `RequireApiKeyAttribute`, and configured prefixes too, for compatibility. It returns 401 for a missing or wrong key (constant-time digest compare), 500 if no key is configured, and 403 outside `ApiSecurity:AllowedIpAddresses`. The partner rate limit (429) follows.
- **Admin network:** `[RequireAdminNetwork]` and `AdminIpRestrictionMiddleware` return 403 outside `AdminSecurity:AllowedIpAddresses`.
- **Policies:** `BranchManagerOnly` and `SameBranchOnly` (ADR 0002).

## Alternatives considered

- **Invent parcel statuses (Accepted, In transit, Delivered).** Rejected: nothing in the domain produces those states.
- **Stored facts only, or courier status only.** The author chose both. Stored facts are authoritative; the courier result exercises the timeout and cancellation path.
- **API key as an authentication scheme with `[Authorize]`.** It is idiomatic, but it runs after the cookie scheme, mixes partner and staff identity, and the assignment asks for middleware driven by endpoint metadata.
- **One key per partner.** It would give true per-partner rate buckets, but needs a new configuration shape. It is deferred; there is one shared key and one bucket today.

## Consequences

The protections now guard a real endpoint and are declared on it. An empty allowlist disables that IP check, so production must set both lists. Behind a proxy, IP checks need trusted forwarded headers. Courier lookups run even for voided or refunded shipments.
