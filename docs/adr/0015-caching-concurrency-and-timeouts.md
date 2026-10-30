# ADR 0015: Caching, concurrency and timeouts

- Status: Accepted
- Date: 2026-10-29

## Context

Pages read the product catalogue and shipping rates often, and each read deserializes the whole JSON snapshot. The dashboard combines independent data (reference data and courier lookups). External lookups (courier tracking, identity verification) can be slow or hang, and a timeout must not be confused with the user cancelling the request.

## Decision

- **`ReferenceDataCache`:**
  - Caches products and shipping rates for `Caching:ReferenceDataSeconds` (30 s), and is invalidated when an administrator adds a product.
  - `GetAsync` returns `ValueTask`, so a hit completes synchronously with no `Task` allocation and no file read.
  - It is for display only: sale pricing always reads the authoritative snapshot inside the locked update.
- **Dashboard:** `BranchDashboardApplicationService` starts the reference-data load and the recent-shipment courier lookups together and awaits `Task.WhenAll`.
- **Courier lookups:** `SimulatedCourierTrackingService` bounds concurrency with `SemaphoreSlim` (`Courier:MaxConcurrentLookups`). It applies `Courier:TimeoutSeconds` through `CancellationTokenSource(timeout, TimeProvider)` linked to the caller's token. A timeout becomes `TimedOut` and an unexpected error becomes `Failed`, while caller cancellation still throws `OperationCanceledException`.
- **Identity verification:** uses the same pattern. A timeout surfaces as "verification unavailable" (503), never as a failed check.

## Alternatives considered

- **`IMemoryCache`.** It is allowed, but the custom cache keeps one typed entry with an explicit invalidation point, and its `ValueTask` fast path is easy to see. `IMemoryCache` would be preferred once there are many keys.
- **Caching the whole snapshot.** It would be stale for stock and transactions, where correctness matters.
- **`Parallel.ForEachAsync` for lookups.** That bounds concurrency at the call site; the semaphore in the service protects the "external" courier however many callers there are.
- **Polly for timeouts and retries.** Not in the allowed libraries.

## Consequences

Repeat page loads within 30 s skip the file read for reference data. The dashboard waits for its slowest part rather than the sum of all of them. No benchmark has been recorded yet, so the benefit is explained rather than measured. Adding a simple timing comparison is a follow-up.
