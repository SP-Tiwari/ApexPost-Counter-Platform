# ADR 0002: Authoritative branch-scoped transaction access

- Status: Accepted
- Date: 2026-10-25

## Context

Transaction identifiers are guessable object references unless Application and Domain workflows verify that the current staff member may access the transaction. A branch code submitted by a browser or copied from a stale claim is not authoritative.

## Decision

Resolve the authenticated `StaffId` against persisted staff data. Require an active Branch Manager and exact home-branch match for transaction listing, details, processing, void, and refund operations. Do not expose legacy baskets whose branch attribution is missing; do not assign them to an arbitrary branch during schema migration.

## Consequences

Cross-branch transaction identifiers return not found and are omitted from lists. Branch Managers process only at their home branch. Legacy records require separately authorized remediation before branch-scoped access.
