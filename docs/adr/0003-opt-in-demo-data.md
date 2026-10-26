# ADR 0003: Opt-in idempotent demo data

- Status: Accepted
- Date: 2026-10-25

## Context

Development and review need representative data, but the application must not silently populate operator data or auto-enable login credentials. Existing JSON persistence already exposes an atomic `UpdateAsync` operation.

## Decision

Register a startup initializer gated by `DemoData:Initialize`, defaulting to false. If enabled, use the repository's `UpdateAsync` callback to seed a fictional snapshot only when every collection is empty. Use the existing schema and domain constructors. Runtime initializer data must not include credentials, hashes, secrets, or full identity-document identifiers.

The repository now includes inspectable seed-data artifacts under `src/ApexPost.Web/App_Data/SeedData/`, including a sample hashed credential document. Those files are documentation/development artifacts at present and are not automatically loaded by `DemoDataInitializer`.

Demo rates, prices, and tax values are illustrative fixtures, not production policy. No parcel limit or money-order threshold is seeded. The current schema has no transaction business date, so seed transactions are paid baskets without asserted historical dates.

## Consequences

Repeated initialization is safe and does not merge data into non-empty snapshots. Operators must disable the setting after a first seed and must replace all demo data/configuration before production use. The repository lock is per instance and does not protect concurrent writers in separate processes.
