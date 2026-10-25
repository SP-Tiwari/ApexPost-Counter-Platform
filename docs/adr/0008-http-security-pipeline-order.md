# ADR 0008: HTTP security pipeline order

- Status: Accepted
- Date: 2026-10-25

## Context

Several protections operate at different points in an HTTP request. Correlation identifiers must exist before exception responses are generated; security headers need to cover downstream responses; routing must identify endpoints before path-scoped API protection and rate limiting; authentication and authorization must run before protected MVC actions.

## Decision

Keep the configured order in `src/ApexPost.Web/Program.cs`: correlation ID middleware, exception handling, HSTS outside Development, security headers, maintenance mode, HTTPS redirection, routing, API-key/IP restriction, rate limiting, authentication, authorization, and mapped MVC endpoints.

The partner protection middleware is path-scoped and configured separately; an empty protected-prefix list does not protect arbitrary MVC pages. API keys and allowed IP addresses are deployment configuration, not source-controlled values. The code does not register CORS or health-check services/endpoints, and this ADR does not claim those features exist.

## Consequences

Error responses produced downstream can retain correlation information and receive security headers. Routing is available to endpoint-sensitive middleware before it evaluates request policy. Partner API protections apply only to configured prefixes. Deployments behind reverse proxies must not assume forwarded client IP handling unless trusted forwarded-header processing is separately configured.

Relevant implementation: `src/ApexPost.Web/Program.cs`, `src/ApexPost.Web/Middleware/CorrelationIdMiddleware.cs`, `src/ApexPost.Web/Middleware/SecurityHeadersMiddleware.cs`, `src/ApexPost.Web/Middleware/MaintenanceModeMiddleware.cs`, and `src/ApexPost.Web/Middleware/ApiKeyAndIpRestrictionMiddleware.cs`.
