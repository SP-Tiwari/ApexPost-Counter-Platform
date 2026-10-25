# ADR 0007: Separate staff credential store

- Status: Accepted
- Date: 2026-10-25

## Context

Staff profile and branch information belongs to the business snapshot, while password-verification material is highly sensitive authentication data. Combining the credential data with ordinary staff records would unnecessarily broaden where credential material is handled and backed up.

## Decision

Keep staff credentials in a separately configured JSON document accessed through `IStaffCredentialRepository`, outside `ApexPostSnapshot`. The authentication service verifies stored password hashes and then resolves the staff profile from the business repository. Only active persisted staff may sign in; role and branch claims are derived from that record. Cookie validation rechecks the current persisted staff status, role, and branch.

The application does not seed or generate credentials. Operators provision the credential file out of band, protect it with filesystem permissions, and must not store plaintext passwords.

## Consequences

Business snapshot backups do not contain password hashes, and demo initialization does not create working accounts. Authentication depends on both the separate credential file and corresponding current staff data; missing or invalid credential data fails closed. Provisioning and credential-file protection remain operational responsibilities.

Relevant implementation: `src/ApexPost.Application/Security/IStaffCredentialRepository.cs`, `src/ApexPost.Application/Services/StaffAuthenticationService.cs`, `src/ApexPost.Application/Security/StaffPasswordHashVerifier.cs`, `src/ApexPost.Infrastructure/Persistence/JsonStaffCredentialRepository.cs`, `src/ApexPost.Web/Authentication/StaffCookieAuthenticationEvents.cs`, and `src/ApexPost.Web/Program.cs`.
