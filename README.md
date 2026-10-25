# ApexPost Counter Platform

ApexPost is a .NET 10 / C# 14 ASP.NET Core MVC application for postal branch counter operations. It uses a layered Domain, Application, Infrastructure, and Web architecture with asynchronous JSON snapshot persistence.

## Projects

- `src/ApexPost.Domain` — entities, value objects, rules, and reporting.
- `src/ApexPost.Application` — use cases and persistence/security/audit abstractions.
- `src/ApexPost.Infrastructure` — JSON persistence and audit implementations.
- `src/ApexPost.Web` — MVC controllers and views, authentication, middleware, and composition root.
- `tests/*` — Domain, Application, Infrastructure, and Web integration tests.

## Implemented workflows and protections

- Goods and shipping can be processed in one transaction. Stock, configured shipping rates, parcel limits, tax configuration, branch attribution, and duplicate submission checks are applied before persistence.
- Transaction lists, details, voids, and refunds are limited to the active Branch Manager's persisted home branch. Legacy transactions with no branch attribution are not exposed.
- Money orders use an explicitly configured high-value threshold. High-value orders require successful manager-attested, in-person identity verification; only a masked document identifier is retained.
- Staff sign-in uses a separately provisioned JSON credential file and secure cookie authentication. Demo data does not create credentials or enable sign-in.
- The Web pipeline includes correlation IDs, exception handling with Problem Details, security headers, maintenance mode, configured API-key/IP restrictions, staff and partner rate limiting, authentication, authorization, and MVC action auditing. `/health` returns a non-sensitive liveness response.
- Parcel-tracking CORS is restricted to exact deployment-configured origins, allows read-only GET requests, and does not allow credentials. No tracking-status endpoint is implemented because persisted shipping data has no tracking state or event history.
- JSON snapshot persistence validates domain data, supports schema migration, and uses atomic updates within a repository instance.

## Configuration

`src/ApexPost.Web/appsettings.json` documents non-secret settings. The storage directory is `Storage:Directory`; credentials are stored separately at `Authentication:CredentialFile`. The application does not create credentials or seed by default.

Production operators must explicitly provide business configuration as applicable:

- `Transactions:TaxRates:<TaxCategory>`
- `Shipping:TaxCategory`
- `Shipping:MaximumParcelWeightKilograms:<ShippingServiceLevel>`
- shipping rates in the JSON snapshot, per destination zone and service level
- both `MoneyOrders:HighValueThreshold:Amount` and `MoneyOrders:HighValueThreshold:Currency`
- partner API key and protected-IP configuration, supplied outside source control

No production prices, tax rates, parcel limits, or money-order thresholds are supplied as defaults. Missing required settings fail closed for the affected workflow.

Partner integration prefixes are opt-in through `ApiSecurity:ProtectedPathPrefixes`. When any are configured, set positive `ApiSecurity:PartnerRateLimit:PermitLimit` and `ApiSecurity:PartnerRateLimit:WindowSeconds` values; startup rejects missing or invalid limits. Requests must first pass the existing API-key and IP checks, then share a fixed-window bucket keyed by the SHA-256 digest of the validated API key. The current configuration supports one shared API key, so it provides one partner bucket rather than separate buckets for distinct partners. Client-supplied partner identity headers are ignored. `ParcelTracking:AllowedOrigins` is an exact-origin array; it defaults to empty (no cross-origin access) and must be populated by deployment configuration with the approved HTTP or HTTPS origin. Wildcards and credentialed CORS are not supported.

### Optional fictional demo data

Set `DemoData:Initialize` to `true` to initialize an entirely empty JSON snapshot with fictional demo branches, staff records, products, stock, shipping rates, and paid transaction baskets. The setting defaults to `false`. Initialization uses the repository's locked `UpdateAsync` operation and only writes when every snapshot collection is empty; it does not merge demo values into an existing store. Turn the setting off after initialization.

All seeded people and business values are explicitly marked or named `DEMO`. The seed contains no passwords, password hashes, secrets, or identity-document numbers. Demo staff cannot sign in; any real staff credential must be provisioned separately through the configured protected credential file. Demo prices and zero tax rates are examples only and must not be used as production business values. Demo parcel limits and money-order thresholds are not seeded.

The repository's transaction schema has no transaction business date. The 40 demo baskets are persisted in `Paid` state but cannot be assigned verifiable historical dates without a schema change. The original assignment document was not present in this checkout, so rubric counts beyond those explicitly supplied in the task remain unverified.

## Documentation and sample records

- [Architecture, data flow, and request pipeline](docs/architecture.md)
- [C# 14 evidence](docs/csharp-14-evidence.md)
- [Architecture Decision Records](docs/adr/)
- [Audit sample](data/AUDIT-SAMPLE.json) — one safe record matching the JSONL audit record schema; not application state.

## Build and test

From the repository root:

```powershell
dotnet build ApexPost.slnx
dotnet test ApexPost.slnx
```
