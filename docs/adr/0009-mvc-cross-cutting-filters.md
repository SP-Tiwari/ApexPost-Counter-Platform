# ADR 0009: MVC cross-cutting action filters

- Status: Accepted
- Date: 2026-10-25

## Context

Audit events, MVC model-validation responses, and controller-action timing depend on MVC action metadata and execution. Middleware surrounds HTTP requests more broadly, including requests that may not map to MVC actions, and does not naturally own model binding state or action/controller identifiers.

## Decision

Register the existing `AuditActionFilter` and the MVC validation and performance filters globally through `AddControllersWithViews`. Preserve the audit store and audit record behavior. The validation filter returns a validation problem for invalid model state on `[ApiController]` endpoints, including controller- and assembly-level attributes; ordinary MVC form actions continue to render their existing views and validation messages. The performance filter times action execution with `try/finally` and logs a structured warning only when elapsed time exceeds `Performance:SlowActionThresholdMilliseconds`.

The threshold defaults to the checked-in 500 ms setting, is configurable, and must convert to a positive representable `TimeSpan`; those conversion constraints are technical, not business policy. The filters do not inspect or log bound values or credentials. Existing controller authorization and antiforgery attributes remain in place.

## Consequences

Action-level concerns remain scoped to MVC while request-wide concerns remain in middleware. Invalid API model state is handled consistently without replacing MVC form redisplay behavior. Performance logs contain controller/action identifiers and timing fields rather than request or personal data. Global registration means these action filters apply to MVC actions unless specifically excluded by MVC filter configuration.

Relevant implementation: `src/ApexPost.Web/Filters/AuditActionFilter.cs`, `src/ApexPost.Web/Filters/ValidationActionFilter.cs`, `src/ApexPost.Web/Filters/PerformanceActionFilter.cs`, `src/ApexPost.Web/Program.cs`, and `src/ApexPost.Web/appsettings.json`.
