# Implementation Plan

## Delivery principles

- Security-critical rules are server-side and database-enforced.
- Build small runnable slices; never leave a phase with a failing build or hidden test.
- Use synthetic data only.
- Clearly distinguish implemented/verified controls from proposed advanced controls.
- Keep requirements, architecture, schema, tests and user documentation synchronized.

## Phase 1 - Requirements and architecture

- [x] Inspect workspace and tooling.
- [x] Define scope, roles, assumptions, acceptance criteria.
- [x] Create architecture, ERD, threat model and privacy boundary.
- [x] Establish git-ignored local environment and Docker approach.
- [x] Define development/demonstration account provisioning strategy.

**Verification:** documents reviewed against the required feature list; no application code is claimed
before build/test.

## Phase 2 - Backend foundation

- [x] Create layered .NET 10 solution.
- [x] Add domain enums/entities and state-machine rules.
- [x] Configure PostgreSQL DbContext, Identity schema, constraints/indexes.
- [x] Add initial migration and startup migration/seed path.
- [x] Add ProblemDetails/exception handling, structured logging, health endpoint and OpenAPI.
- [x] Configure cookies, antiforgery, security headers, rate limiting and CORS.
- [x] Build and smoke-test API with a disposable PostgreSQL container.

## Phase 3 - Authentication and voter management

- [x] Implement Identity roles, login/logout/me, activation, password reset, suspension.
- [x] Implement controlled CSV voter-register import and one-time activation tokens.
- [x] Implement role administration and profile permissions.
- [x] Add authentication/authorization/import audit.
- [x] Unit/integration tests for authentication, lockout, RBAC, import and activation.

## Phase 4 - Election, positions and candidate management

- [x] CRUD/lifecycle APIs with valid transitions and optimistic concurrency.
- [x] Position/rules management and candidate nominations/approval/rejection.
- [x] Eligibility and nomination deadline enforcement.
- [x] Announcements/notifications foundation.
- [x] State-machine and integration tests.

## Phase 5 - Ballot security

- [x] Seed entitlements for eligible voters.
- [x] Implement anonymous ballot model and atomic entitlement consumption.
- [x] Validate candidate/position/selection/abstention rules server-side.
- [x] Return non-revealing confirmation; no choice/identity linkage.
- [x] Vue voting interface with review/confirmation.
- [x] Duplicate, forged, closed-election and concurrency tests.
- [x] Privacy tests asserting no student/choice link in ballot tables/logs.

## Phase 6 - Results and reporting

- [x] Tally service with turnout, percentages, abstentions and tie handling.
- [x] Snapshot versioning, observer verification and Super Admin publication.
- [x] Immutable published results; recount/correction version.
- [x] CSV and print-to-PDF outputs; aggregate charts.
- [x] Role dashboards and published-result views.
- [x] Integration tests for approval/publication/reconciliation.

## Phase 7 - Security and automation

- [x] Security headers, CSRF, rate limits and upload validation tests.
- [x] Audit hash-chain verification and tamper tests.
- [x] Playwright end-to-end scenarios.
- [x] Frontend unit tests and typecheck.
- [x] Dependency/vulnerability review and CI workflow.

## Phase 8 - Deployment and documentation

- [x] Docker multi-stage API/SPA image and Docker Compose PostgreSQL.
- [x] Clean-install, migration, seed, backup and restore verification.
- [x] README, security, testing, user/officer guides, diagrams, project report outline, demo script.
- [x] Final test report with actual results and explicit untested items.

## Phase 9 - Independent security audit and readiness review

- [x] Re-establish baseline from source checkpoint without touching the user's `.env` or real data.
- [x] Reproduce builds, unit/integration/frontend/E2E checks and record baseline counts.
- [x] Audit authorization, ballot, activation, result, audit, upload, CSV, rate-limit, seed and Docker paths.
- [x] Fix confirmed defects with regression tests; preserve corrupt-edit evidence and rebuild cleanly.
- [x] Run 300-voter load, reduced-rate-limit partition check, non-root Docker check and backup/restore.
- [x] Record findings, test evidence, performance variability, residual risks and readiness decision.
- [x] Update README, security, testing, deployment, architecture, threat-model and changelog documents.
- [x] Regenerate a final source archive excluding `.env`, dependencies, build output and local data.

**Decision:** Ready for academic demonstration; not ready for a real departmental election.

## Phase 10 - University-wide departmental scope

- [x] Audit all department-specific assumptions and write the scope/gap analysis.
- [x] Add Department model, scoped assignments, migration/backfill and synthetic three-department seed.
- [x] Enforce server-side department scope for elections, imports, candidates, results, photos, audit,
  announcements and dashboards.
- [x] Add department selection/management/assignment UI and update university-wide branding.
- [x] Add cross-department positive/negative integration tests and migration compatibility drill.
- [x] Run Release build, backend/frontend/E2E suites and dependency audits; record actual results.
- [x] Add installed-PostgreSQL local setup/run/test path; make Docker optional.
- [x] Update architecture, database, threat, testing, deployment, README and academic documents.

**Decision:** independent departmental elections are supported within the implemented software scope;
the system remains an academic/controlled-laboratory platform, not a real-election certification.
## Definition of done for a phase

- Code compiles with zero errors.
- Relevant tests pass.
- No secret or real personal data is committed.
- Documentation matches behavior.
- Unresolved issues are listed in `CHANGELOG.md`/final report, not hidden.
