# Phase 9 Test Results

Date: 2026-10-29  
Status: final verification completed for this audit phase.  
Environment: Windows 11 Pro (10.0.26100 x64), .NET SDK 10.0.403, ASP.NET Core 10.0.14, Node 22.19.0,
npm 10.9.3, Docker Desktop 29.2.1 (Linux containers), PostgreSQL 17.11 (`postgres:17-alpine`).

No real student data, production database or local `.env` was used. Integration tests used
Testcontainers PostgreSQL; Docker/E2E/load tests used isolated Compose projects with synthetic data.

## 1. Final automated results

| Suite | Baseline | Final | Result |
|---|---:|---:|---|
| Backend unit tests | 12 | **27** | 27 passed, 0 failed, 0 skipped |
| Backend integration tests | 21 | **26** | 26 passed, 0 failed, 0 skipped |
| Frontend unit tests | 6 | **6** | 6 passed, 0 failed |
| Playwright E2E | 3 | **3** | 3 passed, 0 failed |
| **Total automated checks** | **42** | **62** | All applicable suites passed |

Other final checks:

| Check | Result |
|---|---|
| Clean Release build | Passed, 0 warnings / 0 errors |
| Frontend typecheck | Passed |
| Frontend production build | Passed |
| Final Docker image | Passed; served `/health` |
| Runtime user | `uid=1654(app)`; non-root |
| `/data/keys` and `/data/photos` | Writable by `app` |
| NuGet vulnerable packages | 0 reported |
| npm production vulnerabilities | 0 reported |
| Backup/restore | Passed; 288 ballots, 583 audit entries, 1 election result, 6 result lines restored |

The first frontend unit run in the earlier audit window timed out before tests started while backend
builds/tests were consuming resources. It passed 6/6 when rerun in isolation; the final successful run
is reported above. This is recorded as test-environment contention, not an application failure.

## 2. Commands and observed output

### Backend build and tests

```powershell
dotnet clean backend\SecureVoting.slnx -c Release
dotnet build backend\SecureVoting.slnx -c Release --no-restore
dotnet test backend\tests\SecureVoting.UnitTests\SecureVoting.UnitTests.csproj -c Release --no-build
dotnet test backend\tests\SecureVoting.IntegrationTests\SecureVoting.IntegrationTests.csproj -c Release --no-build
```

Observed:

```text
Build succeeded. 0 Warning(s) 0 Error(s)
UnitTests:     Passed! Failed: 0, Passed: 27, Skipped: 0, Total: 27
IntegrationTests: Passed! Failed: 0, Passed: 26, Skipped: 0, Total: 26
```

### Frontend

```powershell
cd frontend
npm run typecheck
npm run test:unit
npm run build
```

Observed:

```text
vue-tsc --noEmit: passed
Test Files: 2 passed (2)
Tests: 6 passed (6)
Vite production build: passed
```

### Final Docker E2E

A fresh final Compose project was built and checked:

```powershell
docker compose -p securevoting-final --env-file artifacts\phase9\final.env up -d --build
Invoke-WebRequest http://localhost:8095/health
$env:SECUREVOTE_E2E_URL='http://localhost:8095'
cd frontend
npx playwright test --project=chromium
```

Observed:

```text
health: Healthy
Playwright: 3 passed (35.0s)
- electoral officer sees the administration workspace
- observer can open the audit trail and verify the chain
- secure departmental election from setup through publication and student result
```

The final image reported `uid=1654(app)` and allowed writes to the key and photo directories.

## 3. Regression coverage added or strengthened

| Defect | Regression evidence |
|---|---|
| Generic lifecycle review bypass | Integration test rejects generic `review`; dedicated result review still creates a snapshot |
| Rate limiter ordering | 12 distinct voters accepted with ballot limit 10; load harness at concurrency 12, no 429 |
| Ballot trusted time | Existing closed-election tests plus code review; no deterministic fake-clock race test |
| Activation token reuse | `ActivationTokenCannotBeReused` passed |
| Expired activation token | `ExpiredActivationTokenIsRejected` passed |
| Candidate photo IDOR | `StudentCannotUploadPhotoForAnotherCandidate` passed |
| Candidate photo overwrite | Existing upload/validation test plus unique-filename implementation review |
| Result snapshot integrity | `ResultIntegrityTests`; integration test tampers a result line and receives 409 |
| CSV formula safety | `CsvEscaperTests` |
| Seed environment guard | `SeedPolicyTests` (6 cases) |
| Audit record deletion | `AuditChainDetectsDeletedRecord` passed |
| Audit details tampering | Existing `AuditChainIsVerifiableAndDetectsDirectDatabaseTampering` passed |

The decisive security/load checks were:

- ten simultaneous submissions from one voter produced exactly one accepted ballot;
- distinct concurrent voters all produced accepted ballots and the aggregate count matched accepted
  ballots;
- a forged candidate did not create a ballot or consume an entitlement;
- a generic lifecycle review did not create a result bypass;
- a deleted audit record and a modified audit record both caused verification failure;
- a tampered result line caused the published/reviewed result endpoint to return 409;
- a candidate photo IDOR attempt was rejected;
- a 12-voter reduced-limit run accepted all 12 ballots, confirming per-user ballot rate limiting.

## 4. Load and rate evidence

Detailed measurements, environment and variability are in
[performance-test-report.md](performance-test-report.md). Summary:

| Run | Voters | Concurrency | Accepted | Errors | Submission p95 |
|---|---:|---:|---:|---:|---:|
| Early load run | 300 | 32 | 299 | 1 activation transport | 2,479.6 ms |
| Early load run | 300 | 16 | 300 | 0 | 1,215.6 ms |
| Final-image run | 300 | 16 | 288 | 12 activation transport | 1,031.1 ms |
| Final-image run | 300 | 16 | 288 | 12 activation transport | 2,129.5 ms |
| Final-image run | 300 | 12 | 300 | 0 | 1,107.2 ms |

The 12-way final-image run is the clean demonstration point. The 16-way final runs had intermittent
client-observed activation transport failures and no server 5xx; this is an open reliability finding,
not a passing capacity claim. The 32-way run exceeded the 2-second p95 target.

## 5. Backup and restore drill

On the isolated final-load stack:

1. `pg_dump -Fc` was written inside the database container.
2. The dump was copied out to `artifacts/phase9/backup-check.dump` (373,293 bytes).
3. A new `restore_check` database was created.
4. `pg_restore` completed successfully.
5. Restored counts were: ballots 288, audit entries 583, election results 1, result lines 6.
6. The temporary restore database was dropped.

This verifies the documented PostgreSQL recovery approach on synthetic data. It does not test encrypted
off-host storage, partial restore, point-in-time recovery or institutional recovery approval.

## 6. Dependency and configuration checks

```powershell
dotnet list backend\SecureVoting.slnx package --vulnerable --include-transitive
# all projects: no vulnerable packages reported

cd frontend
npm audit --omit=dev
# found 0 vulnerabilities
```

Compose defaults are Production and seed-disabled. `.env.example` remains a local demonstration profile.
Resource snapshots for the load runs are in `artifacts/phase9/*-stats.txt`.

## 7. What was not run or not proven

| Area | Status |
|---|---|
| Real departmental election | Not run |
| Real student data | Not used |
| Physical modem/SIM/Nigerian mobile network | Not tested |
| Institutional identity provider/biometrics | Not integrated |
| SMS OTP/MFA | Not implemented |
| SMTP password-recovery delivery | Not E2E tested |
| Independent external penetration test | Not performed |
| Multi-node high availability/failover | Not tested |
| Clean institutional Windows install/signed installer | Not performed |
| GitHub Actions CI execution from this workspace | Not run; workflow reviewed only |
| Legacy result-snapshot migration | Not implemented |
| 16-way activation reliability | Not stable in final-image runs |
| 32-way p95 under 2 s | Not met |

## 8. Test-data and cleanup rules

- Synthetic matriculation numbers, names and votes only.
- Testcontainers databases and Compose projects are disposable.
- The local `.env` was not read into test output, committed or packaged.
- Docker E2E/load projects were removed after verification; generated evidence remains under
  `artifacts/phase9` for audit reproducibility.
- No test weakened an assertion to obtain a pass.
