\# ApexPost Counter Platform — Design Document



\## 1. Purpose



This document describes the planned architecture and important design decisions for the ApexPost Counter Platform.



The design will be updated as features are implemented and tested. A planned feature must not be treated as complete until its implementation and verification are finished.



\## 2. Layered Architecture



\### Domain



Contains business entities, value objects, domain rules, and domain events. It must not depend on Infrastructure or Web.



\### Application



Coordinates use cases and application services. It defines the interfaces required for persistence and other infrastructure capabilities. It depends on Domain.



\### Infrastructure



Implements JSON repositories and external-service abstractions. It depends on Application and Domain.



\### Web



Contains ASP.NET Core MVC controllers, views, middleware, authentication, authorization, and HTTP configuration. Controllers call application services rather than implementing business rules.



\## 3. Dependency Direction



The intended project references are:



\- Application → Domain

\- Infrastructure → Application and Domain

\- Web → Application and Infrastructure

\- Test projects → the production project they test



The Web project is the composition root for dependency injection and application pipeline configuration.



\## 4. SOLID Principles



\- \*\*Single Responsibility Principle:\*\* Keep business rules, persistence, HTTP handling, and auditing in separate components.

\- \*\*Open/Closed Principle:\*\* Add new implementations behind stable abstractions where practical.

\- \*\*Liskov Substitution Principle:\*\* Implementations of an interface must preserve its expected behavior.

\- \*\*Interface Segregation Principle:\*\* Keep interfaces focused on the operations their consumers need.

\- \*\*Dependency Inversion Principle:\*\* High-level application logic depends on abstractions, not concrete JSON implementations.



\## 5. Persistence and Future Database Migration

The Application layer defines `IApexPostRepository`, which loads and replaces an `ApexPostSnapshot` whose collection membership is copied on construction (the Domain objects themselves remain mutable). `TransactionApplicationService` uses the abstraction's atomic `UpdateAsync` operation to create baskets, add line items, process sales, and authorize void/refund actions through existing Domain rules. Each basket has a stable generated transaction ID so subsequent application calls can select it. The service does not supply default tax rates or business values. `ApexPost.Infrastructure` implements the repository; Web registers the repository and application service in its composition root. Application code depends on the interface and Domain types, not JSON APIs.

The JSON implementation stores one versioned `apexpost.json` document containing branches and counter terminals, staff, products, branch stock, shipping items and rates, money orders, identity-verification results, and transaction baskets including their IDs. Schema version 1 snapshots are migrated to version 2 on load by assigning transaction IDs and atomically saving the upgraded document. The storage directory is configured by `Storage:Directory`; the checked-in configuration uses the relative `data` directory, resolved against the Web content root. No seed data is created. Reports are derived and are not stored as report snapshots. Low-stock and high-value notification rule results are transient; void/refund notifications are persisted with their basket status and reason.

Infrastructure uses persistence DTOs and reconstructs Domain objects through their validating constructors and state transitions. It does not deserialize directly into encapsulated entities. The basket reconstitution API validates line items, tax rates, payment state, and matching void/refund notification reason. Identity records persist only the masked identifier, never the original document number. Currency amounts, enum values, and nested data are validated during load.

A missing file represents an empty snapshot. An empty, malformed, unsupported-version, or Domain-invalid file fails explicitly and is not replaced automatically. Save writes a temporary file in the same directory, flushes it asynchronously, then replaces the document. Load, save, and update use asynchronous streams and accept cancellation tokens. If a write fails and temporary-file cleanup also fails, the original operation exception is rethrown and the cleanup exception is attached in its `Data` dictionary. Cancellation tests deterministically cancel after a completed load and before the subsequent write; cancellation at an arbitrary point during stream serialization is not injected because the repository has no file-stream test seam. Replacement-failure coverage verifies temporary-file cleanup where the test environment permits it. `UpdateAsync` holds the same semaphore across load, caller transformation, and replacement, preventing in-process read-modify-write lost updates when callers use that operation. Web registers a singleton repository so operations share that lock. The lock is instance-local; separate repository instances or application processes sharing a directory are not coordinated. Callers must not concurrently mutate the same Domain objects while saving them. Atomic replacement depends on same-volume filesystem behavior. Cancellation before replacement leaves the prior file intact; cancellation after replacement may arrive only after the operation has committed.

This collection-level repository can be replaced behind `IApexPostRepository` by a database-backed snapshot implementation without changing Domain rules. A future design with per-aggregate database updates may introduce more granular repository interfaces once stable identifiers and use-case boundaries exist. The current model does not associate money orders or identity verifications with branches, nor does it retain transaction business dates; persistence does not invent those links, so report callers still need authoritative context.



\## 6. Business Rules



The implementation must enforce the following rules:



\- Inventory stock must never become negative.
\- Sales validate all branch/product stock requirements before deducting stock and marking the basket Paid; goods lines must reference a Product SKU, while service lines do not consume stock. Employee/branch transaction processing delegates to this sale workflow.

\- Inactive staff cannot perform transactions.

\- Counter clerks can transact only at their assigned branch.

\- Closed branches cannot process transactions.

\- Only active Counter Clerks and Branch Managers can process baskets; Counter Clerks are limited to their assigned branch, while Branch Managers have no branch assignment restriction.

\- Money orders contain an amount, sender, recipient, and fee; amount and fee use the same currency, and the amount must be positive.
\- Money orders above a configured threshold require successful identity verification; the threshold is supplied as configuration, not a domain default.
\- Identity verification records retain only a masked document identifier showing its last four characters, never the full identifier.

\- Parcel weight must not exceed the maximum for the selected service.
\- Shipping rates are configured per destination zone and service level using a base price and per-kilogram charge in the same currency.
\- Maximum parcel weights are configured per service level; domain code provides no production prices or weight limits.
\- Voiding or refunding a transaction requires an active Branch Manager and a non-empty reason, and must respect the basket's valid payment-status transition.

\- Full identity document numbers must never be stored.



\## 7. Mixed Transactions



A single counter transaction can contain retail products and services such as shipping and money orders.



A transaction basket can contain goods and services. It calculates subtotal, tax, and total using explicitly configured rates by tax category; the Domain provides no tax-rate defaults or rounding policy.

Basket payment status transitions are Open → Paid or Open → Voided, and Paid → Refunded. Only non-empty Open baskets can be paid.

Transaction totals, stock changes, payment processing, and transaction status changes must be handled through explicit application workflows. The design must prevent partial updates from leaving inconsistent business data.

\## 7.1 Reports and Notifications

Report inputs supply branch and business-date associations because transaction baskets and service records do not own that context. The caller must provide authoritative, unique activity records; the Domain cannot validate fabricated branch/date attribution or deduplicate repeated report inputs. Daily sales include baskets that are Paid when the report takes its basket snapshot and classify line totals, including configured tax, as goods or services; mixed currencies for one branch/date are rejected rather than converted. Low stock means quantity less than or equal to the caller-supplied threshold and is evaluated independently for every supplied branch-stock record. Shipping volume counts each supplied shipping-item record once, grouped by exact destination zone and service level. Auditor reports count caller-associated money orders and identity-verification outcomes per branch.

Domain notification rules produce low-stock and high-value money-order notification values from explicit thresholds; the application money-order workflow must invoke the high-value rule when processing an order. They do not deliver or deduplicate notifications. Successful basket void/refund transitions are available only through the authorization rule, require a non-empty reason, and append typed notification records containing that reason before changing state under the shared mutation lock. Those records are included in a saved basket snapshot, but are not durable unless the application saves the updated snapshot; external dispatch and delivery guarantees remain outside the Domain.



\## 8. Security Design



The application will use secure cookie authentication and claims for staff identity, role, and branch. Authorization must enforce both role permissions and applicable branch restrictions.



Additional controls include password hashing, antiforgery validation, login and integration rate limits, API key validation, IP allowlists, security headers, HTTPS, and safe exception handling.



Secrets must be provided through configuration or an appropriate secret store rather than committed to source control.



\## 9. Middleware and Filters



Middleware will handle cross-cutting HTTP concerns such as exception handling, correlation IDs, request timing, maintenance mode, and security headers.



MVC action filters will handle action-level concerns such as business-action auditing, validation behavior, and slow-action warnings.



Request timing and action timing measure different parts of the request lifecycle and should be logged separately where useful.



\## 10. Auditing and Privacy



Audit records should capture relevant business actions, staff identity, role, branch, timestamp in UTC, result, reason where applicable, and correlation ID.



Audit records must not contain passwords, API keys, full identity document numbers, or sensitive payment data.



The JSON audit implementation should be append-oriented and protected by appropriate file permissions. JSON files alone do not guarantee tamper-proof audit storage; integrity protection and secure backups must be considered.



\## 11. Asynchronous Operations and Concurrency



I/O operations should use async/await and propagate CancellationToken where appropriate.

JSON repository reads and writes share a `SemaphoreSlim`; `UpdateAsync` keeps the lock for the full read-modify-write sequence. Concurrent access is coordinated only within the singleton repository instance/process. File content is streamed asynchronously, and replacement is performed only after serialization and flush succeed. Independent dashboard queries may run concurrently when doing so is safe.



Advanced C# features must solve real problems and be justified in the design documentation rather than added only to meet a checklist.



\## 12. Testing Strategy



\- Domain unit tests verify business rules and value objects.

\- Application unit tests verify use cases and service behavior.

\- Infrastructure tests verify JSON serialization, persistence, and concurrency behavior.

\- Web integration tests verify middleware, authentication, authorization, HTTP responses, and security requirements.



The final solution must include meaningful business-rule tests and at least six WebApplicationFactory integration tests required by the assignment.



\## 13. Outstanding Design Artifacts



The following artifacts must be added and maintained as implementation progresses:



\- Architecture and domain-model diagrams.

\- Middleware pipeline diagram and ordering rationale.

\- Architecture Decision Records (ADRs).

\- Threat model and security mitigations.

\- Audit sample data.

\- Evidence of required C# 14 features and their justification.
