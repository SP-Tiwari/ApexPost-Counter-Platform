# ADR 0004: Atomic transaction processing

- Status: Accepted
- Date: 2026-10-25

## Context

Processing a transaction can create basket lines, change stock, set payment status, and add shipping records. Saving these changes independently could leave inconsistent data if a later validation or persistence step fails. Repeated form submissions must also not deduct stock twice.

## Decision

Coordinate mixed-basket creation and transaction processing within the existing `IApexPostRepository.UpdateAsync` operation. The repository holds its instance lock while it loads the current snapshot, applies the update callback, and saves the resulting snapshot. The workflow validates stock before deduction through `SaleProcessingService`, applies payment and branch attribution, and returns the updated snapshot with transaction and shipping records together. It rejects a transaction ID already present in the snapshot.

The application and domain layers enforce transaction and stock rules; the MVC controller handles input binding, authorization, antiforgery, and Post/Redirect/Get rather than performing persistence or inventory mutations itself.

## Consequences

A failed callback does not save the new snapshot, and duplicate transaction IDs do not cause a second successful processing update. This is in-process coordination for callers sharing the same repository instance only; it is not a multi-process transaction guarantee. The persisted snapshot remains a whole-document JSON store rather than a database transaction.

Relevant implementation: `src/ApexPost.Application/Services/MixedBasketApplicationService.cs`, `src/ApexPost.Application/Services/TransactionApplicationService.cs`, `src/ApexPost.Domain/Services/SaleProcessingService.cs`, `src/ApexPost.Infrastructure/Persistence/JsonApexPostRepository.cs`, and `src/ApexPost.Web/Controllers/TransactionsController.cs`.
