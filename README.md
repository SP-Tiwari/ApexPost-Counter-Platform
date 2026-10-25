\# ApexPost Counter Platform



\## 1. Project Overview



ApexPost Counter Platform is an ASP.NET Core MVC application built with .NET 10 and C# 14. It supports postal branch counter operations, including retail sales, inventory management, parcel shipping, money orders, identity verification, and reporting.



\## 2. Technology Stack



\- .NET 10

\- C# 14

\- ASP.NET Core MVC

\- JSON-file persistence for the initial version

\- xUnit for automated testing

\- NSubstitute for test doubles

\- Git and GitHub for version control



\## 3. Solution Structure



\- `src/ApexPost.Domain` — Business entities, value objects, and domain rules.

\- `src/ApexPost.Application` — Use cases, application services, and abstractions.

\- `src/ApexPost.Infrastructure` — JSON persistence and external-service implementations.

\- `src/ApexPost.Web` — MVC controllers, views, authentication, and middleware.

\- `tests/ApexPost.Domain.Tests` — Domain unit tests.

\- `tests/ApexPost.Application.Tests` — Application unit tests.

\- `tests/ApexPost.Infrastructure.Tests` — Persistence tests.

\- `tests/ApexPost.Web.IntegrationTests` — HTTP, MVC, middleware, and security tests.

\- `docs` — Architecture documentation, ADRs, and security documentation.

\- `data` — Development JSON data and audit records.



\## 4. Architecture Principles



The application uses a layered architecture and follows SOLID principles.



\- Business rules belong in the Domain or Application layers, not in controllers.

\- Application code depends on abstractions instead of JSON-specific implementations.

\- Infrastructure implements the persistence and service interfaces.

\- Dependency injection connects implementations to their interfaces.

\- Controllers remain thin and delegate work to application services.

\- Validation, authentication, authorization, auditing, and error handling are separated into appropriate components.



\## 5. Persistence Strategy



The initial implementation uses JSON files behind repository interfaces. A future database implementation should replace the JSON repositories without requiring business logic to be rewritten.



All file operations should be asynchronous, support cancellation where appropriate, and protect against conflicting concurrent writes.



\## 6. Security



The application will include secure cookie authentication, role-based and branch-based authorization, password hashing, antiforgery protection, rate limiting, security headers, correlation IDs, request logging, and audit records.



Secrets must not be committed to source control. Passwords, API keys, complete identity document numbers, and payment-sensitive information must not appear in logs.

### Money orders and identity checks

Branch Managers can process money orders through the **Money orders** page. Configure both `MoneyOrders:HighValueThreshold:Amount` and `MoneyOrders:HighValueThreshold:Currency` before enabling the workflow (for example, with environment variables `MoneyOrders__HighValueThreshold__Amount` and `MoneyOrders__HighValueThreshold__Currency`). There is intentionally no default threshold or example business amount. When settings are absent, money-order pages fail closed with HTTP 503; other MVC functionality remains available.

For high-value orders, the signed-in manager must attest that they checked the original identity document in person and record the result. Failed checks and high-value orders without a successful check are rejected. Only the document type and masked identifier are persisted; the full identifier is not retained or logged. This workflow represents staff in-person verification, not an external electronic identity-verification provider.



\## 7. Testing



The solution uses xUnit. NSubstitute will be used when test doubles are useful.



Testing will cover domain business rules, application behavior, JSON persistence, and web integration. The final implementation must include meaningful tests for the assignment's business and security requirements.



\## 8. Build and Test



Run these commands from the directory containing `ApexPost.slnx`:



```powershell

dotnet build ApexPost.slnx

dotnet test ApexPost.slnx

```



\## 9. Implementation Status



The solution includes layered Domain, Application, Infrastructure, and MVC workflows for transactions, money orders, staff authentication, JSON persistence, and request security controls. The README and DESIGN.md document configuration and known limitations. The repository does not seed production data or staff credentials.

### Mixed goods and shipping transactions

Branch Managers can create and process a basket containing goods, shipping, or both from **Transactions → Create transaction**. The processing branch comes from the authenticated staff record, not form input or the branch claim. Processing validates stock and configured shipping rates and parcel limits, calculates tax and total, persists the transaction and shipping item together, and records the processing branch. Successful submissions redirect to transaction details; reusing the submitted transaction ID is rejected to prevent duplicate stock deductions.

Transaction lists, details, voids, and refunds are scoped to the active Branch Manager’s authoritative home branch. The Application layer resolves the `StaffId` claim against persisted staff and enforces the persisted role and active status; Domain rules require the transaction’s recorded branch to match that staff member’s home branch for access and state changes. Cross-branch and branch-unattributed legacy transactions are hidden as not found and are excluded from lists. Legacy snapshots migrate without guessed branch assignments; an operator must establish branch attribution through a separately authorized data remediation before those records can be accessed or changed.

The application intentionally supplies no production tax rates, shipping rates, shipping tax category, or parcel weight limits. Deployment configuration must provide applicable `Transactions:TaxRates:<TaxCategory>` values, `Shipping:TaxCategory` for shipping-line tax treatment, and `Shipping:MaximumParcelWeightKilograms:<ShippingServiceLevel>` limits for parcel services. Shipping base and per-kilogram rates are operator-provisioned in the JSON snapshot by destination zone and service level. Missing configuration does not silently fall back to sample business values; shipping choices require configured rates and a configured shipping tax category. See DESIGN.md for details.
