# Security Documentation

This document summarizes security controls, assumptions and residual risks. The detailed threat model
and privacy analysis are in [docs/threat-model.md](docs/threat-model.md) and
[docs/architecture.md](docs/architecture.md).

## Verification status

The controls below are implemented and exercised by 27 unit, 33 integration, 6 frontend unit and 3
Playwright end-to-end checks (69 total), plus the Phase 10 department-migration drill, Docker,
dependency and restore evidence in [docs/phase-10-test-results.md](docs/phase-10-test-results.md). Physical hardware, institutional
identity systems and a real election are not validated. The status is **software-verified, not
production-certified**. The audit decision is **ready for academic demonstration and controlled multi-department laboratory
validation, not ready for a real departmental election**.

## Baseline controls

- Data-driven Departments with unique normalized codes, optional faculty, active state and historical
  election preservation.
- Election/student department foreign keys and explicit department-scoped Electoral Officer/Observer
  assignments; Super Administrators retain documented university-wide oversight.
- Department-scoped election lists, details, voter imports, candidate decisions, results, photos,
  announcements, dashboards and observer audit visibility.
- ASP.NET Core Identity password hashing, strong password policy, lockout and security stamps.
- Secure HttpOnly authentication cookie; SameSite Strict; Secure outside Development.
- Antiforgery token required for all unsafe API methods.
- Server-side RBAC with fallback authenticated policy and deny-by-default endpoint policies.
- Parameterized EF Core queries; no user-controlled SQL.
- Vue template escaping; no `v-html` for untrusted data; restrictive CSP when SPA is served by API.
- PostgreSQL transactions, unique constraints, foreign keys and conditional entitlement update.
- Ballot model with no voter/user key on the ballot or selections.
- One accepted ballot per entitlement using an atomic conditional update; trusted time is captured
  after the election row lock.
- Rate limiting for login and ballot submission; ballot limits partition by authenticated voter;
  bounded request/upload sizes.
- Result snapshots use a full-field SHA-256 integrity hash and are verified on read; tampering returns
  a conflict instead of exposing an altered result.
- Structured logging with no request-body, ballot-choice or activation matriculation logging.
- Hash-chained audit log with append-only API; verification endpoint for authorized roles. The chain is
  tamper-evident, not tamper-proof against a privileged database administrator.
- SMTP optional; no credentials/ballot choices in email or in-app notifications.
- Environment/secret-store configuration; no real credentials in source.

## Department scope limitations

- Access depends on Super Administrators assigning the correct department scopes. Misassignment is an
  institutional operational risk; assignments are audited but not automatically verified against HR
  records.
- Audit `DepartmentId` is visibility metadata and is not part of the hash chain. A privileged database
  administrator can change scope metadata; event content remains covered by the existing hash.
- Legacy rows may receive generated `LEGACY-...` department codes during migration. Institutions must
  review legacy department identity and assignments before production use.
- Faculty/school is an optional display string, not an independent entity.
- The application remains a single-institution, single-database design; it is not a multi-tenant SaaS.

## Security-critical assumptions

- TLS is terminated by a trusted reverse proxy in any deployed environment.
- The server clock is trusted/synchronized.
- The operating system, PostgreSQL, reverse proxy and Data Protection key store are protected by the
  institution.
- Activation tokens are distributed through a controlled electoral-office process.
- No cryptographic end-to-end verifiable voting is claimed.

## Ballot secrecy statement

The schema intentionally separates identity-linked eligibility/entitlement data from anonymous
ballots. The ballot, selections and internal receipt have no student, user, token, IP or user-agent
field. The confirmation reference is stored only as a hash in the entitlement and is not stored on the
ballot. Ordinary APIs, charts, CSV exports and audit records never expose a voter-to-choice mapping.

This does **not** provide absolute anonymity. Residual risks include:

- timing correlation between entitlement consumption and ballot insertion;
- access by a privileged database administrator who can inspect transactions, backups and WAL;
- access by a compromised application process that handles a submission;
- infrastructure logs outside the application;
- coercive client-side observation or device compromise;
- database-level tampering that also rewrites aggregate/audit evidence.

Separate database roles, encryption, external audit checkpoints, stricter transaction/timing padding
and independent security review are production hardening requirements.

## Audit chain limitation

The audit log uses a SHA-256 hash chain with a serialized chain-state row. It detects accidental or
partial alteration. A database administrator can rewrite the entire chain and chain state. External
periodic checkpoints (signed/exported hashes or a write-once store) are required for a stronger
non-repudiation claim and are not implemented.

## Identity verification limitation

Possession of a matriculation number is not proof of identity. Accounts are provisioned from an
officer-controlled register using one-time activation tokens. The system does not integrate with a
university identity provider or biometric verification. Institutions must verify registration through
their approved process.

## Load and availability limitation

The 300-voter synthetic load was clean at 12-way concurrency (p95 submission 1.1 s) and exceeded the
2-second p95 target at 32-way concurrency. Final-image runs at 16-way concurrency showed intermittent
client-side activation transport failures without application 5xx responses. Do not claim 16-way or
32-way capacity; see [docs/performance-test-report.md](docs/performance-test-report.md). A single
API/PostgreSQL instance is not high availability.

## Deployment requirements

- Use HTTPS and HSTS at the reverse proxy.
- Set strong environment secrets; never use demo credentials in a real election.
- Demo seeding is permitted only in Development/Testing by the application; set `SEED_ENABLED=false`
  elsewhere.
- Run the container as the supplied non-root `app` user; keep `/data` and the database volumes
  restricted.
- Enable `Security__UseForwardedHeaders` only when the API is unreachable except through a trusted
  proxy that strips client-supplied forwarded headers; otherwise rate limiting sees the proxy address.
- Create departments and assign Electoral Officers/Observers to their authorised departments before creating elections; review legacy `LEGACY-...` codes after migration.
- Use a least-privilege PostgreSQL user for the application and separate backup credentials.
- Encrypt database backups, restrict access and test restoration.
- Protect and back up Data Protection keys; losing them invalidates cookies/antiforgery tokens.
- Restrict candidate photo volume permissions.
- Add monitoring, log retention, intrusion detection and an incident-response procedure.
- Obtain independent penetration testing, legal review and an election dispute procedure before real
  use.

## Reporting security issues

This is an academic project. Report suspected vulnerabilities privately to the project owner; do not
test against real student data or a live election.
