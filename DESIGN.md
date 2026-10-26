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



The Web project is the composition root for dependency injection and application pipeline configuration. Staff sign-in reads a separate, operator-provisioned JSON credential file and verifies password hashes through the Application service. The file contains no plaintext passwords and is not part of the Domain snapshot. After verification, the service loads the authoritative Staff record; only active staff can sign in, and the `StaffId`, `Role`, and `BranchCode` claims come from that record rather than submitted form values. Cookie authentication uses secure, HTTP-only, SameSite=Lax cookies with an eight-hour sliding lifetime. Each authenticated request revalidates the staff member's active status, role, and home branch against current persisted staff data; a missing or changed staff record revokes the cookie. Cookie sign-out is POST-only and antiforgery-protected. Transaction list, details, and create/process pages require the BranchManager role. The Application service resolves the authenticated `StaffId` against persisted staff and enforces active status, persisted role, and branch ownership; the Domain access and transaction-processing rules enforce BranchManager home-branch scope. The browser does not choose a processing branch. Successful create requests use Post/Redirect/Get; duplicate transaction IDs are rejected inside the repository's atomic update. Details provide antiforgery-protected POST actions for voiding open baskets and refunding paid baskets, with manager, branch, reason, and state-transition checks enforced by Application and Domain rules.



\## 4. SOLID Principles



\- \*\*Single Responsibility Principle:\*\* Keep business rules, persistence, HTTP handling, and auditing in separate components.

\- \*\*Open/Closed Principle:\*\* Add new implementations behind stable abstractions where practical.

\- \*\*Liskov Substitution Principle:\*\* Implementations of an interface must preserve its expected behavior.

\- \*\*Interface Segregation Principle:\*\* Keep interfaces focused on the operations their consumers need.

\- \*\*Dependency Inversion Principle:\*\* High-level application logic depends on abstractions, not concrete JSON implementations.



\## 5. Persistence and Future Database Migration

The Application layer defines `IApexPostRepository`, which loads and replaces an `ApexPostSnapshot` whose collection membership is copied on construction (the Domain objects themselves remain mutable). `TransactionApplicationService` uses the abstraction's atomic `UpdateAsync` operation to create baskets, add line items, process sales, and authorize void/refund actions through existing Domain rules. `MixedBasketApplicationService` validates configured goods and shipping inputs, calculates shipping using persisted configured rates, invokes the parcel weight rule, and processes goods stock, total, payment, transaction branch attribution, shipping records, and basket persistence in one `UpdateAsync` operation. A failed validation or stock check prevents that update from being saved. It rejects duplicate transaction IDs, so a repeated form submission cannot deduct stock twice. Neither service supplies default tax rates or business values. `ApexPost.Infrastructure` implements the repository; Web registers the repository and application services in its composition root. Application code depends on the interface and Domain types, not JSON APIs.

The JSON implementation stores one versioned `apexpost.json` document containing branches and counter terminals, staff, products, branch stock, shipping items and rates, money orders, identity-verification results, and transaction baskets including their IDs, processing branch, and associated shipping line metadata. Schemas 1 through 3 are migrated to version 4 on load: version 1 receives transaction IDs, older records default to no money-order verification association, and legacy baskets have no recorded branch or line-level shipping association. The storage directory is configured by `Storage:Directory`; the checked-in configuration uses the relative `data` directory, resolved against the Web content root. Demo data is not created by default. When `DemoData:Initialize` is explicitly enabled, `DemoDataInitializer` seeds a fictional snapshot only if every collection is empty, using the repository's atomic `UpdateAsync` operation. Its runtime initializer data contains no credentials, hashes, secrets, or full identity-document numbers. The Web project also contains `src/ApexPost.Web/App_Data/SeedData/` with inspectable seed JSON artifacts (`branches.json`, `counters.json`, `staff.json`, `staff-credentials.json`, `products.json`, `inventory.json`, `counter-transactions.json`, `apexpost-demo-seed.json`), but current `DemoDataInitializer` does not load from those files yet. Each processed money order embeds its optional identity-verification record as well as contributing that same masked verification to the report collection, preserving the association through JSON persistence. Reports are derived and are not stored as report snapshots. Low-stock and high-value notification rule results are transient; void/refund notifications are persisted with their basket status and reason.

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

\- Only active Counter Clerks and Branch Managers can process baskets; processing is limited to the staff member's assigned branch.

\- Money orders contain an amount, sender, recipient, and fee; amount and fee use the same currency, and the amount must be positive.
\- Money orders above a configured threshold require successful identity verification; the threshold is supplied as configuration, not a domain default.
\- Identity verification records retain only a masked document identifier showing its last four characters, never the full identifier.

\- Parcel weight must not exceed the configured maximum for the selected service; letters are not subject to parcel limits.
\- Shipping rates are configured per destination zone and service level using a base price and per-kilogram charge in the same currency.
\- Maximum parcel weights are configured per service level; application configuration supplies these values, and domain code provides no production prices or weight limits.
\- Voiding or refunding a transaction requires an active Branch Manager and a non-empty reason, and must respect the basket's valid payment-status transition.

\- Full identity document numbers must never be stored.



\## 7. Mixed Transactions



A mixed transaction can contain retail products and shipping services. Money orders continue to use their separate application workflow and are not transaction-basket lines.



A transaction basket can contain goods and services. It calculates subtotal, tax, and total using explicitly configured rates by tax category; the Domain provides no tax-rate defaults or rounding policy.

The BranchManager-only **Create transaction** page can process one goods line, one shipping item, or both in the same sale. Goods are selected from persisted products, shipping rates are selected from the persisted snapshot, and the existing sale rule validates stock and staff/branch eligibility before payment. The processing branch is resolved from the active staff record and cannot be changed by form values or claims. Branch Managers are scoped to their home branch for transaction processing and access. Shipping charges use the configured rate formula `base price + weight in kilograms * per-kilogram charge`. The shipping item is retained both with its transaction service line and in the shipping-item snapshot collection. A server-generated transaction ID is submitted with the antiforgery-protected form and checked atomically to reject duplicate submissions. Successful processing redirects to transaction details.

Transaction list queries return only baskets whose recorded branch matches the active persisted Branch Manager's home branch. Detail reads, voids, and refunds first resolve the requested basket and enforce the same branch match; inaccessible IDs (including guessed IDs from another branch) return not found to avoid disclosing their existence. Void/refund additionally retain the active Branch Manager and valid-state/reason requirements. The browser's role and branch claims are not used as the source of transaction authorization; `StaffId` identifies the persisted staff record, whose current role, active status, and home branch are authoritative. Older snapshots may contain baskets with no branch attribution. Such records are excluded from every branch list and cannot be viewed or voided/refunded through this workflow. They are not assigned to a default branch; operators need a separate trusted reconciliation before any explicit remediation.

There are no production defaults for taxes, shipping prices, shipping tax treatment, or parcel limits. Configure `Transactions:TaxRates:<TaxCategory>` values for applicable product tax categories; values are decimal multipliers (for example, an operator-configured `0.1` represents 10%), and the Domain applies no rounding policy. Shipping additionally requires `Shipping:TaxCategory` to select the configured tax category applied to shipping service lines. Configure `Shipping:MaximumParcelWeightKilograms:<ShippingServiceLevel>` for each service level that accepts parcels; letters do not require a parcel maximum. These settings may be provided by deployment configuration (for example, `Transactions__TaxRates__Standard`, `Shipping__TaxCategory`, and `Shipping__MaximumParcelWeightKilograms__Standard`). Add actual `ShippingRates` entries to the operator-managed JSON snapshot for each supported destination-zone/service-level pair, with base and per-kilogram amounts in one currency. The application does not seed prices, tax rates, or limits. Transaction processing is disabled in the UI when no eligible configured catalog items or active branches are available; requests are still validated server-side.

Basket payment status transitions are Open → Paid or Open → Voided, and Paid → Refunded. Only non-empty Open baskets can be paid.

Transaction totals, stock changes, payment processing, and transaction status changes must be handled through explicit application workflows. The design must prevent partial updates from leaving inconsistent business data.

\## 7.1 Reports and Notifications

Report inputs supply branch and business-date associations because transaction baskets and service records do not own that context. The caller must provide authoritative, unique activity records; the Domain cannot validate fabricated branch/date attribution or deduplicate repeated report inputs. Daily sales include baskets that are Paid when the report takes its basket snapshot and classify line totals, including configured tax, as goods or services; mixed currencies for one branch/date are rejected rather than converted. Low stock means quantity less than or equal to the caller-supplied threshold and is evaluated independently for every supplied branch-stock record. Shipping volume counts each supplied shipping-item record once, grouped by exact destination zone and service level. Auditor reports count caller-associated money orders and identity-verification outcomes per branch.

Domain notification rules produce low-stock and high-value money-order notification values from explicit thresholds; the money-order application workflow requires an explicitly configured `MoneyOrders:HighValueThreshold:Amount` and `MoneyOrders:HighValueThreshold:Currency`, then invokes `MoneyOrderIdentityVerificationRule` before atomically persisting the order and any successful verification through `IApexPostRepository.UpdateAsync`. There is no invented default threshold. If the settings are absent, money-order pages return HTTP 503 while other application functionality remains available. An active BranchManager must attest in the protected MVC workflow that the original identity document was inspected in person. A failed check or a high-value order without a successful check is rejected without persisting the order. The submitted document identifier is used only to construct the masking value object; the JSON record stores only its document type and masked value, and the audit filter does not record form values. This is an in-person staff attestation, not an external identity-proofing provider; deployments needing provider-verified identity must integrate an approved provider before treating the workflow as electronic identity verification. Notification rules do not deliver or deduplicate notifications. Successful basket void/refund transitions are available only through the authorization rule, require a non-empty reason, and append typed notification records containing that reason before changing state under the shared mutation lock. Those records are included in a saved basket snapshot, but are not durable unless the application saves the updated snapshot; external dispatch and delivery guarantees remain outside the Domain.



\## 8. Security Design



Staff credentials are stored separately from the staff/domain snapshot at `Authentication:CredentialFile` (default relative path: `data/staff-credentials.json`, resolved from the Web content root). No credential file or account is seeded by the application runtime initializer. Operators must provision every credential used in active environments through a trusted out-of-band process, protect the file with appropriate filesystem permissions, and never store plaintext passwords. A sample hashed-credential JSON file now exists under `src/ApexPost.Web/App_Data/SeedData/staff-credentials.json` for inspection only; it is not automatically consumed by runtime authentication unless explicitly configured and integrated. The version 1 JSON document has a `schemaVersion` integer and a `credentials` array. Each record contains `staffId`, `hashVersion`, `iterations`, `salt`, and `passwordHash`; salt and hash values are Base64. Hash version 1 uses PBKDF2-HMAC-SHA256, a unique cryptographically random 16-byte salt per credential, and a 32-byte derived hash. Credential records must use at least 600,000 iterations; the verifier rejects values below that minimum or above 2,000,000. `DefaultIterations` is 210,000 and is used only for dummy password verification when no credential is found or supplied; it does not generate production credentials or satisfy the minimum for credential records. The verifier compares hashes in constant time. Full identity-document numbers and credential-generation endpoints must not be added to this store or application. Missing, malformed, or unsupported credential data fails closed; sign-in reports the credential service as unavailable rather than issuing a ticket.

Sign-in POST requests use the named ASP.NET Core fixed-window rate-limit policy. `Authentication:SignInRateLimit:PermitLimit` and `Authentication:SignInRateLimit:WindowSeconds` configure the request count and window (defaults: 5 and 60); excess requests receive HTTP 429. Partitions use the connection's remote IP address, not submitted identity values, and do not permanently lock staff accounts. Deployments behind a reverse proxy must configure and trust forwarded headers appropriately before relying on the client-IP partitioning; forwarded headers are not enabled by this application configuration.

Partner API protection is opt-in and path-scoped through `ApiSecurity:ProtectedPathPrefixes`. The default is an empty list, so regular MVC pages and other requests are unaffected. Every request under a configured prefix must provide the single `X-API-Key` header matching `ApiSecurity:ApiKey`; a missing or invalid key, including a missing configured key, returns HTTP 401 without a response body. Supply the expected key through deployment configuration or a secret store (for example, the `ApiSecurity__ApiKey` environment variable); do not commit it to appsettings or source control. Key verification compares fixed-size SHA-256 digests with a constant-time comparison.

`ApiSecurity:AllowedIpAddresses` optionally contains exact IP addresses allowed for those same protected prefixes. An empty list disables IP filtering; a non-empty list rejects requests with no remote address or an address not in the list using HTTP 403. IPv4-mapped IPv6 addresses are normalized. The middleware uses the connection's `RemoteIpAddress` and does not trust `X-Forwarded-For`; deployments behind a proxy must configure trusted forwarded headers before relying on this restriction. The API security middleware runs after routing and maintenance/HTTPS handling, but before rate limiting, authentication, and authorization. No partner API route currently exists; add the middleware scope only to explicitly configured partner/API path prefixes.

When partner prefixes are configured, both `ApiSecurity:PartnerRateLimit:PermitLimit` and `ApiSecurity:PartnerRateLimit:WindowSeconds` must be positive deployment settings; there are no production defaults. The partner rate-limit middleware runs immediately after API-key/IP validation. It partitions by the SHA-256 digest of the validated configured API key, never by a caller-supplied identity header, and rejects excess requests with HTTP 429. Since the current API-key configuration contains one shared key, all callers using it share one bucket; distinct per-partner buckets require a future validated multi-key identity configuration.

`ParcelTracking:AllowedOrigins` is an exact-origin configuration array for CORS. It defaults to empty, rejects wildcard/path-based values, permits only GET and the `Accept` request header, and does not enable credentials. The setting must be populated by deployment configuration with the approved origin; no origin is invented in source control. `/health` is a liveness response containing only `{"status":"Healthy"}` and does not expose configuration or dependency details. There is no parcel-status endpoint: `ShippingItem` has no tracking status, tracking number, or event history, so returning a parcel status would require unsupported business data.

The sign-in form and sign-out action require antiforgery validation. Return URLs are restricted to local destinations. Passwords and other secrets must never be logged. Transaction pages require the BranchManager role and enforce branch scope through the persisted staff record and basket attribution.



Secrets must be provided through configuration or an appropriate secret store rather than committed to source control.



\## 9. Middleware and Filters



Middleware handles HTTP request/response concerns that apply before MVC action selection or across non-MVC endpoints, including exception handling, correlation IDs, request timing, maintenance mode, and security headers. It is intentionally not responsible for MVC model validation or timing an individual controller action: at that layer MVC has not exposed the bound `ModelState`, selected action metadata, or action execution boundary.

MVC action filters run around selected MVC actions and can inspect the bound model state, controller/action metadata, and action completion. `AuditActionFilter` continues to append business-action records through the existing `IAuditStore`; `ValidationActionFilter` converts invalid model state to a validation problem response for `[ApiController]` endpoints while leaving regular MVC form actions to preserve their existing validation messages and view reconstruction; `PerformanceActionFilter` measures action execution only. This separation keeps action-specific concerns in MVC and request-wide concerns in middleware. Request timing and action timing measure different portions of the lifecycle and should not be conflated.

`Performance:SlowActionThresholdMilliseconds` configures the slow-action warning threshold. The checked-in operational starting value is 500 ms; it is not a business rule and may be overridden by deployment configuration (for example, `Performance__SlowActionThresholdMilliseconds`). The value must convert to a positive `TimeSpan`: zero, negative, NaN, infinite, values too small to survive `TimeSpan` tick precision, and values outside the representable `TimeSpan` range fail during startup service configuration. These limits are technical representability requirements, not business thresholds. An action is warned when its measured execution time is greater than the configured threshold. Structured warning fields include controller, action, elapsed milliseconds, and threshold milliseconds; request parameters, model values, identity claims, and exception details are excluded.



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



\## 13. Design Artifacts and Known Limitations

Architecture and middleware pipeline diagrams, ADRs, C# 14 implementation evidence, and an audit sample matching the current `AuditRecord` schema are maintained under `docs/` and `data/`; see [docs/architecture.md](docs/architecture.md), [docs/csharp-14-evidence.md](docs/csharp-14-evidence.md), [docs/adr/](docs/adr/), and [data/AUDIT-SAMPLE.json](data/AUDIT-SAMPLE.json).

Demo initialization is opt-in (`DemoData:Initialize`, default `false`) and runs only against an entirely empty snapshot. Seeded branch/staff/product labels and rates are fictional examples. Demo tax rates are zero-valued fixtures only. The runtime initializer still does not create demo credentials, password hashes, secrets, parcel maximums, or money-order thresholds. Demo staff cannot authenticate from initializer data alone until an operator separately provisions credentials. Separately, `src/ApexPost.Web/App_Data/SeedData/` now includes inspectable sample seed files (including a sample hashed credential document) that are not yet auto-loaded by `DemoDataInitializer`.

The current schema does not store transaction business dates. The 40 demo transactions are persisted paid baskets but cannot be treated as dated historical events. The original Week 2 assignment document was not present in the repository; rubric-specific counts and requirements beyond those quoted in the task remain unverified. The audit sample is one JSON object matching `AuditRecord`; the runtime append-only file is newline-delimited JSON.
