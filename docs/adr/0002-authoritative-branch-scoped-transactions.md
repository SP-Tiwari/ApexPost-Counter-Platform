# ADR 0002: Authoritative branch-scoped transaction access

- Status: Accepted
- Date: 2026-10-25 (updated 2026-10-29: Counter Clerks process sales; `SameBranchOnly` policy)

## Context

Transaction identifiers are guessable object references unless the application verifies that the current staff member may access the transaction. A branch code submitted by a browser, or copied into a cookie claim that may since have gone stale, is not authoritative.

## Decision

Resolve the authenticated `StaffId` against persisted staff data on every use case.

- **Processing:** active Counter Clerks and Branch Managers process sales only at their home branch, and the clerk recorded on the sale must be the signed-in staff member.
- **Management:** listing, details, voids and refunds need an active Branch Manager whose home branch matches the transaction's branch.
- **Clerk read access:** an active Counter Clerk can view the details and shipping label of a sale they processed (the recorded clerk is them) at their home branch, so a completed sale does not end on an access-denied page. They cannot list other sales, void or refund.
- **Web boundary:**
  - `BranchManagerOnly` is a role policy.
  - `SameBranchOnly` is a resource-based policy evaluated against the loaded transaction's `BranchCode`.
  - The `SameBranchOnly` handler also reads the persisted home branch, not the claim, so it repeats the service check as defence in depth.
- **Legacy records:** baskets with no branch attribution are not exposed, and migration does not assign them to an arbitrary branch.

## Alternatives considered

- **Trust the `BranchCode` claim.** No extra read is needed, but a staff member moved to another branch would keep access until the cookie expired.
- **Policy check only at the controller.** It is simpler, but the application services would then be unsafe to call from anywhere else, such as tests or a future API.
- **Return 403 for other branches' IDs everywhere.** That would confirm the ID exists. The services return not found instead; the policy check on details and summary returns 403 only after the transaction is already in scope.

## Consequences

Cross-branch transaction IDs return not found and are left out of lists. Each `SameBranchOnly` check costs one extra staff lookup. Legacy records need separately authorized remediation before branch-scoped access.
