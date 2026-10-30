# ADR 0006: Money orders as basket lines with system identity verification

- Status: Accepted
- Date: 2026-10-25 (revised 2026-10-29; replaces the separate manager-attested money-order form)

## Context

A money order above a configurable threshold must not complete without a successful identity verification *in the same transaction*. The earlier design used a separate Branch-Manager-only form where the user ticked "check passed" and typed the fee. That meant counter clerks could not issue money orders, the verification was not part of the sale, and the result was self-declared. Full identity-document numbers must never be stored.

## Decision

- **Basket lines:** money orders are lines of the counter basket. Clerks and managers can issue them in a sale alongside goods and shipping.
- **Fee:** a flat fee per order from `MoneyOrders:Fee:Amount` and `MoneyOrders:Fee:TaxCategory`, added as its own line. The principal counts in the transaction total but not in sales revenue, and is not taxed.
- **Verification:** above `MoneyOrders:HighValueThreshold` (amount and currency, no default), the system calls `IIdentityVerificationService` before the repository lock is taken.
  - The simulated provider accepts only the configured `MoneyOrders:Verification:AcceptedDocumentTypes` and has a timeout (`:TimeoutSeconds`).
  - A failed check rejects the sale. A timeout or outage returns 503 and nothing is saved.
  - `MoneyOrderIdentityVerificationRule` enforces the result inside the atomic sale.
- **Privacy:** only the document type and masked identifier (last four characters) are stored. The redisplayed form clears the identifier, the request record redacts it in `ToString`, and the audit never records form values.
- **Reversal:** voiding or refunding the sale marks its money orders `Cancelled`. They stay on record and are shown in the auditor report.

## Alternatives considered

- **Keep the separate money-order form and link it to a sale.** Two submissions cannot guarantee "in the same transaction", and clerks still could not use it.
- **Manager attestation (the previous design).** It is honest about being in-person, but the result is user-declared, which the trainer flagged. It also does not exercise a timeout or failure path.
- **A percentage fee or fee bands.** The assignment does not specify a fee model, so a flat configured fee is the smallest assumption. It was confirmed with the author.
- **Deleting money orders on void.** That would lose the audit trail; cancelling keeps the record.

## Consequences

A high-value money order cannot complete without a passing system check in the same atomic sale. The workflow is disabled until threshold, fee and verification settings are configured. The simulated provider is not real identity proofing; a production deployment must plug in an approved provider behind `IIdentityVerificationService`. The old form's `MoneyOrderApplicationService.CreateAndProcessAsync` has been removed; that service now only reads the branch's money orders for the list page.

Relevant implementation: `src/ApexPost.Application/Services/MixedBasketApplicationService.cs`, `src/ApexPost.Application/Services/MoneyOrderBasketSettings.cs`, `src/ApexPost.Application/Verification/IIdentityVerificationService.cs`, `src/ApexPost.Infrastructure/Verification/SimulatedIdentityVerificationService.cs`, `src/ApexPost.Domain/Rules/MoneyOrderIdentityVerificationRule.cs`, and `src/ApexPost.Domain/ValueObjects/IdentityVerification.cs`.
