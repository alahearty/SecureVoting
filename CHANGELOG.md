# Changelog

All notable implementation changes are recorded here. Dates use the development environment date.

## [1.2.0] - 2026-10-29 - University-wide department scope

### Added

- `Department` and `DepartmentAssignment` model with unique codes, optional faculty, active state and
  historical preservation.
- Required department foreign keys on elections/students and scoped server-side access for officers and
  observers.
- Super Administrator department management and scoped staff assignment UI.
- Department selection/filtering and context across election, candidate, student, observer and result UI.
- Three synthetic departments/elections (Computer Science, Mathematics, Physics) and scoped demo staff.
- Seven cross-department isolation integration tests and a legacy-data migration drill.
- Installed-PostgreSQL local setup/launcher and no-Docker test path; Docker Compose remains optional.

### Changed

- Election creation/update/import/candidate/result/announcement/audit/dashboard paths now enforce
  department scope.
- Published results are limited to eligible students or scoped staff.
- Audit entries carry non-hashed department visibility metadata; the hash chain is unchanged.
- Documentation and demo scenario now describe a university-wide platform rather than a CS-only system.

### Verified

- Clean Release build; 27 unit, 33 integration, 6 frontend unit and 3 E2E checks (69 total).
- Disposable PostgreSQL migration preserved synthetic legacy election/student rows and backfilled FKs.
- Installed PostgreSQL 18 local API/SPA and three Playwright checks passed without Docker.
- Optional Docker image remains non-root and healthy; dependency audits report no known vulnerabilities.

### Known limitations

- No institutional identity provider/MFA; audit scope metadata is not hashed; legacy department codes
  require institutional review; Phase 10 schema was not re-load-tested; no real election or external
  penetration test.

## [1.1.0] - 2026-10-29 - Phase 9 security audit release

### Added

- Phase 9 audit, findings, test-results, performance and modified-files evidence.
- Full-field result-snapshot integrity hash with read verification and tamper regression.
- Unit-tested seed environment policy, CSV escaping, load-test harness and expanded security tests.

### Fixed

- Generic lifecycle `review` bypass of result snapshot creation.
- Ballot rate limiter running before authentication, which made the ballot limit global.
- Ballot trusted time being captured before the election row lock.
- Activation-token reuse race through non-atomic consumption.
- Candidate photo ownership/IDOR and failed-update overwrite risk.
- Demo seeding permitted outside Development/Testing.
- Root Docker runtime and unsafe Compose defaults.
- Activation logging of matriculation numbers.

### Verified

- Clean Release build, 27 unit + 26 integration + 6 frontend unit + 3 E2E checks (62 total).
- Final image runs as non-root `app` and serves a healthy stack.
- 300-voter synthetic load is clean at 12-way concurrency; 16-way final-image activation transport
  failures and 32-way p95 above 2 seconds are documented.
- PostgreSQL backup/restore and dependency audits completed on synthetic data.

### Known limitations

- Not ready for a real departmental election.
- No external signed audit checkpoint, MFA/OTP, cryptographic end-to-end verifiability or biometric
  identity proof.
- Pre-Phase-9 result snapshots use the old hash format and are not migrated.
- No independent penetration test, real hardware or high-availability test.

## [1.0.0] - 2026-10-29 - Academic demonstration release

### Added

- Complete ASP.NET Core 10/PostgreSQL API and Vue 3 SPA.
- Identity auth, role policies, activation, password reset interface, user administration and lockout.
- Election lifecycle, positions, candidate nomination/approval, CSV voter import and one-time activation tokens.
- Anonymous ballot model, atomic one-vote entitlement, concurrent submission protection and non-revealing confirmation.
- Versioned aggregate results, observer verification, Super Administrator publication and recount workflow.
- Announcements, in-app notifications and optional SMTP password recovery.
- Hash-chained audit log with verification endpoint.
- CSV/PDF/print result outputs, dashboards, role navigation and responsive UI.
- Unit, integration and Playwright end-to-end tests, Docker Compose and GitHub Actions CI.
- Architecture, ERD, threat model, user/administrator guides, deployment/backup and academic report documents.

### Verified

- Backend unit and integration suites pass; frontend typecheck/unit/build pass; Playwright suite passes
  against the Docker stack.
- PostgreSQL dump/restore round trip verified with synthetic data.
- No physical hardware or real election data was used.

### Known limitations

- Physical hardware and real institutional identity verification are not tested.
- No cryptographic end-to-end voting, MFA/OTP or ranked-choice voting.
- Residual ballot timing/correlation and privileged database administrator risks are documented.

## [0.1.0] - 2026-10-28 - Foundation

### Added

- Initial solution structure and Phase 1 requirements/architecture/threat model.
- Technology selection: .NET 10 API, Vue 3 + TypeScript, PostgreSQL, Docker Compose.
- Ballot privacy model separating eligibility, entitlement, anonymous ballot, internal receipt and
  aggregate result snapshots.
- One-vote enforcement plan using a unique entitlement and conditional atomic update.

### Not yet implemented

- Application endpoints, migrations, frontend and automated tests are delivered in subsequent phases.
- Physical/institutional identity verification and cryptographic end-to-end verifiability are out of
  scope for this baseline; see security documentation.
