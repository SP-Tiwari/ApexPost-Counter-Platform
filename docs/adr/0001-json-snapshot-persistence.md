# ADR 0001: Versioned JSON snapshot persistence

- Status: Accepted
- Date: 2026-10-25

## Context

The initial application needs durable persistence without adding a database or ORM. Domain invariants must remain independent of the storage format, and snapshot schema changes must preserve existing installations.

## Decision

Use `IApexPostRepository` in Application and implement it with Infrastructure's versioned `apexpost.json` snapshot. Reconstruct entities through domain constructors and explicit state transitions. Perform file reads/writes asynchronously, write replacements through a same-directory temporary file, and serialize in-process updates using the repository instance lock.

## Consequences

The JSON implementation can be replaced behind the abstraction. Migrations remain explicit and legacy data is not assigned invented business attributes. The lock coordinates only callers sharing one repository instance; it does not provide cross-process concurrency control.
