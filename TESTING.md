# Testing Strategy

## Test layers

| Layer | Tooling | Scope |
|---|---|---|
| Backend unit | xUnit | State transitions, eligiblity, ballot validation, tally/tie/turnout, audit hashing |
| Backend integration | xUnit + WebApplicationFactory + installed PostgreSQL (Testcontainers fallback) | API auth/RBAC, import/activation, lifecycle, ballots, results, audit, department isolation, concurrency |
| Frontend unit | Vitest + Vue Test Utils + jsdom | Stores, API helper, critical components/composables |
| End-to-end | Playwright + Chromium | Login, election administration, ballot, confirmation, result publication |
| Build/typecheck | `dotnet build`, `npm run typecheck`, `npm run build` | Compile and strict TypeScript |
| Security checks | Integration tests + dependency audit | 401/403, CSRF, injection/forgery, duplicate/concurrent ballots, log/audit leakage |

## Required commands

```powershell
# Backend, installed PostgreSQL (no Docker)
.\test-local.ps1

# Or manually after loading .env.local:
dotnet clean backend/SecureVoting.slnx -c Release
dotnet build backend/SecureVoting.slnx -c Release --no-restore
dotnet test backend/tests/SecureVoting.UnitTests/SecureVoting.UnitTests.csproj -c Release --no-build
dotnet test backend/tests/SecureVoting.IntegrationTests/SecureVoting.IntegrationTests.csproj -c Release --no-build

# Load harness (explicit; not part of the normal suite)
dotnet run --project backend/tests/SecureVoting.LoadTest/SecureVoting.LoadTest.csproj -c Release --no-build

# Frontend
cd frontend
npm ci
npm run typecheck
npm run test:unit
npm run build

# E2E against local run.cmd stack (no Docker)
$env:SECUREVOTE_E2E_URL='http://localhost:5173'
npx playwright install chromium
npm run test:e2e
```

## Integration database

Integration tests use the installed `securevote_test` database when `SECUREVOTING_TEST_CONNECTION` is
set (the default for `.\test-local.ps1`). The fixture refuses any external database whose name does not
end in `_test`. If the variable is absent, the suite retains the `Testcontainers.PostgreSql` Docker
fallback. Tests never use development or production data.

The suite must verify:

- unauthenticated access denied;
- student/candidate cannot call admin endpoints;
- ineligible student cannot submit;
- one accepted ballot and second HTTP 409;
- simultaneous submissions yield exactly one accepted ballot;
- forged/foreign candidate and position IDs rejected;
- after-close and outside-window submissions rejected;
- entitlement consumed only with committed ballot;
- ballot tables contain no student/user/identity fields;
- result totals derive from accepted ballots;
- verification/publication separation and immutable published results;
- audit chain verification, audit deletion and no ballot choices in audit/log output;
- CSV import validation/duplicate detection/activation, including expired/reused tokens;
- candidate photo ownership and safe replacement;
- result-snapshot integrity verification;
- CSV export formula-injection escaping;
- CSRF and per-authenticated-voter rate-limit behavior where testable.

## Concurrency tests

1. Ten parallel submissions for one eligible voter -> exactly one 201 and nine 409s; one ballot row.
2. Twenty distinct eligible voters submit concurrently -> all accepted; tally equals accepted ballots;
   no lost updates.
3. Result publication attempts without observer verification -> 409/403.

## Actual results

The current actual results, timings, environment and explicitly unperformed checks are recorded in
[docs/test-report.md](docs/test-report.md) and
[docs/phase-9-test-results.md](docs/phase-9-test-results.md):

- **27 backend unit tests passed.**
- **33 backend integration tests passed** against the installed PostgreSQL 18 `securevote_test` database (no Docker), including the Phase 10 cross-department isolation suite.
- **6 frontend unit tests passed.**
- **3 Playwright end-to-end tests passed** against the local installed-PostgreSQL API and Vite SPA.
- Clean Release build, frontend typecheck/build, dependency audits and PostgreSQL backup/restore were
  verified separately. Optional Docker runtime evidence remains historical.
- A legacy-data migration drill was run on a disposable PostgreSQL database: old string departments were
  backfilled to department foreign keys without deleting historical rows.
- A synthetic 300-voter load was performed: clean at 12-way concurrency; 16-way final-image runs had
  intermittent activation transport failures; 32-way p95 exceeded 2 s. See
  [docs/performance-test-report.md](docs/performance-test-report.md).

Do not claim unrun hardware, institutional identity, real-election or higher-concurrency capacity. The
clean demonstrated load point for the historical Phase 9 schema is 12-way concurrency; the expanded
department schema was not re-load-tested in Phase 10.

## Test data rules

- Synthetic student names, matriculation numbers, candidates and votes only.
- No real credentials or production database.
- Test passwords/tokens are local and not reused in deployment.
- Local test database `securevote_test` is reset through its isolated schema; the development database is never used.
- Docker test containers and temporary files are disposed when the optional fallback is used.

## Definition of a passing phase

- Build/typecheck passes.
- All applicable tests pass.
- Failures are fixed, not skipped or weakened.
- Known untested items are recorded.
