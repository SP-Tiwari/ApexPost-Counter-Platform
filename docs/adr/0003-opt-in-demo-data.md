# ADR 0003: Opt-in idempotent demo data

- Status: Accepted; the credential clause is superseded by [ADR 0010](0010-demo-logins-from-user-secrets.md)
- Date: 2026-10-25 (updated 2026-10-29: seed sales are dated)

## Context

Development and review need representative data, but the application must not silently populate operator data or seed login credentials. Existing JSON persistence already exposes an atomic `UpdateAsync` operation.

## Decision

Register a startup initializer gated by `DemoData:Initialize`, defaulting to false. If enabled, use the repository's `UpdateAsync` callback to seed a fictional snapshot only when every collection is empty. Use the existing schema and domain constructors. Never include credentials, hashes, secrets, or full identity-document identifiers.

Demo rates, prices, and tax values are illustrative fixtures, not production policy. No parcel limit or money-order threshold is seeded. The 40 seed sales are processed through `SaleProcessingService` (so they deduct stock) and dated every six hours back from seeding time.

## Alternatives considered

- **Seed whenever the app starts.** This would overwrite or merge into real operator data.
- **Load hand-written JSON seed files.** Tried and retired (see ADR 0010): the files drifted from the domain rules and were never loaded.
- **Seed only through tests.** That leaves reviewers with nothing to click through.

## Consequences

Repeated initialization is safe and does not merge data into non-empty snapshots. Operators must disable the setting after a first seed and must replace all demo data/configuration before production use. The repository lock is per instance and does not protect concurrent writers in separate processes.
