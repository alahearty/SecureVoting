# Test Results and Verification Report

Date: 2026-10-29  
Phase 10 supersedes the Phase 9 report. The complete Phase 10 evidence is in
[phase-10-test-results.md](phase-10-test-results.md),
[phase-10-implementation.md](phase-10-implementation.md) and
[phase-10-scope-audit.md](phase-10-scope-audit.md). Phase 9 load/security evidence remains historical
in [phase-9-test-results.md](phase-9-test-results.md).

## Environment

| Item | Value |
|---|---|
| OS | Windows 11 Pro, 10.0.26100 x64 |
| .NET | SDK 10.0.403; ASP.NET Core runtime 10.0.14 |
| Node/npm | Node 22.19.0; npm 10.9.3 |
| Database | Installed PostgreSQL 18 (`postgresql-x64-18`) for local verification; Docker 17 fallback remains available |
| Tests | xUnit 2.9.3; Testcontainers 4.16.0 fallback; Vitest 5.0.5; Playwright 1.64.1 |
| Docker | Optional; not required for local verification |
| Physical hardware | None; no hardware result claimed |

## Final automated results

| Suite | Result | Command |
|---|---|---|
| Backend unit | **27 passed, 0 failed, 0 skipped** | `dotnet test ...UnitTests.csproj -c Release --no-build` |
| Backend integration | **33 passed, 0 failed, 0 skipped** | `dotnet test ...IntegrationTests.csproj -c Release --no-build` |
| Frontend unit | **6 passed, 0 failed** | `npm run test:unit` |
| Playwright E2E | **3 passed, 0 failed** | `npx playwright test --project=chromium` |
| **Total automated checks** | **69 passed** | Sum of the four suites |
| Backend Release build | Passed, **0 warnings / 0 errors** | Clean `dotnet build backend\SecureVoting.slnx -c Release --no-restore` |
| Frontend typecheck | Passed | `npm run typecheck` |
| Frontend production build | Passed | `npm run build` |
| Local installed-PostgreSQL stack | Passed; `/health` healthy | `.\run.cmd` |
| Optional Docker runtime | Previously verified; non-root `app`/UID 1654 | `docker compose ...` optional |
| Integration test database | `securevote_test`; non-superuser `securevote_app` | `.\test-local.ps1` |
| NuGet vulnerabilities | 0 reported | `dotnet list ... package --vulnerable --include-transitive` |
| npm production vulnerabilities | 0 reported | `npm audit --omit=dev` |

The earlier frontend unit run under concurrent backend build/test load timed out before executing tests;
the isolated rerun passed 6/6. This is an environment-contention observation, not a weakened pass.

## Commands

```powershell
.\test-local.ps1

# Equivalent explicit backend sequence after loading .env.local:
dotnet clean backend\SecureVoting.slnx -c Release
dotnet build backend\SecureVoting.slnx -c Release --no-restore
dotnet test backend\tests\SecureVoting.UnitTests\SecureVoting.UnitTests.csproj -c Release --no-build
dotnet test backend\tests\SecureVoting.IntegrationTests\SecureVoting.IntegrationTests.csproj -c Release --no-build

cd frontend
npm run typecheck
npm run test:unit
npm run build
# Start installed-PostgreSQL stack with .\run.cmd first
$env:SECUREVOTE_E2E_URL='http://localhost:5173'
npx playwright test --project=chromium
```

## Coverage highlights

### Authentication, authorization and audit

- Unauthenticated and candidate access to administration denied.
- CSRF token required for unsafe requests.
- Observer cannot open an election; role separation preserved.
- Import, activation, token reuse and expired-token rejection tested.
- Audit-chain details tampering and deleted-record detection tested.
- Ballot choices and matriculation numbers are absent from relevant audit/details output.

### Ballot security

- One accepted ballot per voter; duplicate and concurrent attempts return conflicts.
- Ten simultaneous submissions from one voter produce exactly one accepted ballot.
- Distinct concurrent voters are all counted; no lost updates.
- Forged candidate IDs do not insert ballots or consume entitlements.
- Submissions after closure or outside the trusted window are rejected.
- Generic lifecycle review cannot bypass snapshot creation.
- Ballot and selection tables contain no student/user identity columns.

### Results

- Aggregate result derives from stored ballots; turnout and candidate sums reconcile.
- Observer verification precedes publication; published results are versioned.
- Full-field integrity hash detects modified result lines and returns 409.
- Published result endpoint exposes aggregate-only data.

### Candidate photos and exports

- Ownership check blocks cross-student candidate photo upload.
- Type/size validation and generated filenames are exercised.
- CSV export escaping covers formula markers, leading whitespace and controls.

### Frontend/E2E

- Officer administration workspace.
- Observer audit-chain verification.
- Full synthetic election from setup through student result publication.

## Load and rate evidence

See [performance-test-report.md](performance-test-report.md). Key points:

- 300 voters and 32-way concurrency: 299 accepted, one activation transport failure, submission p95
  2,479.6 ms.
- 300 voters and 16-way concurrency on the early image: 300 accepted, p95 1,215.6 ms.
- Final-image 16-way runs: 296/300 and 288/300 accepted with activation transport failures; no server
  5xx.
- Final-image 12-way run: 300/300 accepted, p95 1,107.2 ms.
- Reduced ballot-limit check: 12/12 accepted with a limit of 10, confirming per-user rate limiting.

The clean demonstrated load point is 12-way concurrency. No capacity claim is made for 16-way or
32-way production use.

## Backup/restore

A custom-format PostgreSQL dump was restored into a separate database on the isolated stack. Restored
counts were 288 ballots, 583 audit entries, 1 election result and 6 result lines; the temporary database
was then dropped. The dump is retained at `artifacts/phase9/backup-check.dump` (373,293 bytes). This is a
synthetic-data drill, not an institutional recovery test.

## Explicitly not verified

| Area | Status |
|---|---|
| Physical modem/SIM/Nigerian mobile network | Not tested |
| Real SMS delivery | Not tested |
| Institutional identity/biometrics | Not integrated |
| SMS OTP/MFA | Not implemented |
| SMTP password-recovery delivery | Not E2E tested |
| Real student/candidate data | Not used |
| Real departmental election/legal procedure | Not performed |
| Independent external penetration test | Not performed |
| Multi-node high availability/failover | Not tested |
| Signed installer/clean Windows profile | Not performed |
| GitHub Actions execution from this workspace | Not run; workflow reviewed |
| Cryptographic end-to-end verifiable voting | Not implemented |
| DBA whole-chain rewrite resistance | Not solved; documented |
| Legacy result-snapshot migration | Not implemented |

## Residual risks

1. Ballot timing and privileged database/server access can still correlate entitlement consumption with
   ballot insertion.
2. A database administrator can rewrite the audit chain and chain state; external signed checkpoints are
   absent.
3. Database administrator or application compromise can modify election evidence; integrity hashes are
   not signatures.
4. SMTP recovery is optional and untested; there is no app-level MFA/OTP.
5. A single API/PostgreSQL deployment is not high availability; 16-way load was not reliably clean.
6. Possession of a matriculation number or activation token is not real-world identity proof.
7. Compromised voter devices/coercion remain outside application control.
8. Pre-Phase-9 result snapshots use the old hash format and are not migrated.

## Conclusion

The implemented software passes 69 automated checks, a clean Release build, a full synthetic Docker E2E
election, dependency audits, a database restore drill, a disposable-PostgreSQL legacy migration drill,
and a 300-voter demonstration at 12-way concurrency under the historical Phase 9 schema. It is suitable
for controlled academic demonstration and further multi-department laboratory validation. It is not
certified, not hardware-validated and not ready for a real departmental election.
