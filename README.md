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



The initial solution and eight projects have been created. The solution currently builds, and the generated template tests pass.



Business features, security controls, meaningful tests, seed data, and final documentation remain to be implemented and verified.
