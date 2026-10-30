# ADR 0009: MVC filters — opt-in audit, validation and performance

- Status: Accepted
- Date: 2026-10-25 (revised 2026-10-29: opt-in audit with final status codes; validation filter handles MVC forms)

## Context

Auditing, model validation and action timing depend on MVC action metadata, bound `ModelState` and the action result, none of which middleware can see. The earlier filters had three defects (flagged in review):

- The audit recorded every action, page views included.
- It logged redirects as 200, because it read the status before the result ran.
- The validation filter acted only on `[ApiController]` endpoints, and there were none.

## Decision

Register three global filters through `AddControllersWithViews` (`ServiceFilter`, scoped):

- **`AuditActionFilter`**
  - Records only actions marked `[Audited]`: sign-in and sign-out, sales (including money orders and identity checks), voids, refunds, stock adjustments and administration changes.
  - Implements both the action filter and the always-run result filter, and writes after the result has executed, so the record has the status code actually sent (302 for redirects). An unhandled exception is recorded as 500.
  - Outcome is `Succeeded` below 400, `Rejected` for 4xx and `Failed` for 5xx.
  - The write is not cancelled if the client disconnects. The store is `IAuditStore` (append-only JSON Lines).
- **`ValidationActionFilter`**
  - For `[ApiController]` endpoints it returns `ValidationProblemDetails` (400); the built-in automatic 400 is suppressed so this filter is the single place.
  - For MVC forms it asks the controller's `IInvalidModelStateHandler` to redisplay the view. The controller rebuilds select lists and clears passwords and document numbers.
  - The new-sale form opts out (returns null) so the clerk sees binding and basket errors together.
- **`PerformanceActionFilter`** logs a warning when an action exceeds `Performance:SlowActionThresholdMilliseconds`.

## Alternatives considered

- **Audit in middleware.** Middleware sees the final status but not the controller, action or route values, and cannot tell a business action from a page view without duplicating routing.
- **Audit everything and filter later.** It produces noise and stores more personal browsing data than needed. The assignment asks for business actions.
- **Audit in the application services.** It would record business detail, such as the transaction ID, but not the HTTP outcome or correlation ID in one place. It may be added later for richer records.
- **Writing the audit from `Response.OnCompleted`.** The status is certainly final, but the write would race the client and its failures could not be surfaced.
- **A generic MVC redisplay (`View(model)`) in the filter.** It cannot rebuild view-model lists and would echo sensitive fields back.

## Consequences

The audit is smaller and accurate. Requests rejected before MVC (authorization, antiforgery, rate limit, IP checks) are not in the audit; the framework logs them. A sale's transaction ID arrives in the form, not the route, so the sale action adds it with `AuditActionFilter.AddAuditValue`, which keeps only allow-listed keys. [data/AUDIT-SAMPLE.json](../../data/AUDIT-SAMPLE.json) contains real records from a local run.

Relevant implementation: `src/ApexPost.Web/Filters/` and `src/ApexPost.Web/Program.cs`.
