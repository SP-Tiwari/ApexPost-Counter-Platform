# ADR 0006: Money-order verification and privacy

- Status: Accepted
- Date: 2026-10-25

## Context

High-value money orders require an identity-verification decision. The application does not integrate an external identity-proofing provider, and retaining complete identity-document numbers would create unnecessary sensitive-data exposure.

## Decision

Require both `MoneyOrders:HighValueThreshold:Amount` and `MoneyOrders:HighValueThreshold:Currency` to configure the high-value rule. There is no production threshold default. `MoneyOrderApplicationService` verifies the persisted staff record is an active Branch Manager, constructs the money order, and invokes `MoneyOrderIdentityVerificationRule` before persisting the order and any verification result in the same repository update.

For high-value orders, the MVC workflow records an active manager's attestation that the original document was checked in person and captures the check result. This is **in-person manager attestation, not an external identity-proofing service**. The application persists only the document type and masked identifier; it does not persist or audit the full identifier.

## Consequences

The workflow fails closed when threshold configuration is absent and rejects a failed check or an order requiring verification without successful verification. Deployments requiring provider-verified identity must integrate an approved provider before describing this workflow as electronic identity proofing. Operators must provide the business threshold explicitly.

Relevant implementation: `src/ApexPost.Application/Services/MoneyOrderApplicationService.cs`, `src/ApexPost.Domain/Rules/MoneyOrderIdentityVerificationRule.cs`, `src/ApexPost.Domain/ValueObjects/IdentityVerification.cs`, `src/ApexPost.Web/Controllers/MoneyOrdersController.cs`, `src/ApexPost.Web/Program.cs`, and `src/ApexPost.Infrastructure/Persistence/JsonApexPostRepository.cs`.
