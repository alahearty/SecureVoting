# Phase 10 - Test Results

Date: 2026-10-29  
Environment: Windows 11 Pro 10.0.26100 x64; .NET SDK 10.0.403 / ASP.NET Core 10.0.14; Node 22.19.0;
installed PostgreSQL 18 service `postgresql-x64-18`. Docker remains available as an optional fallback.

No real student data, production database, local `.env` or real election was used.

## 1. Final counts

| Suite | Phase 9 | Phase 10 final | Result |
|---|---:|---:|---|
| Backend unit tests | 27 | **27** | 27 passed, 0 failed, 0 skipped |
| Backend integration tests | 26 | **33** | 33 passed, 0 failed, 0 skipped |
| Frontend unit tests | 6 | **6** | 6 passed |
| Playwright E2E | 3 | **3** | 3 passed against the installed-PostgreSQL local stack |
| **Total automated checks** | **62** | **69** | All applicable suites passed |

Additional checks:

| Check | Result |
|---|---|
| Clean Release build | Passed, 0 warnings / 0 errors |
| Frontend typecheck | Passed |
| Frontend production build | Passed |
| Local API/SPA stack | `/health` healthy at `localhost:5000`; Vite served at `localhost:5173` |
| Local database | Non-superuser `securevote_app`; `securevote` + `securevote_test` |
| Optional Docker runtime | Previously verified non-root `app`/UID 1654; not used for the local verification |
| NuGet vulnerabilities | 0 reported |
| npm production vulnerabilities | 0 reported |
| Legacy-data migration drill | Passed on a disposable PostgreSQL database |
| Phase 9 300-voter performance | Historical evidence only; not rerun after the department schema change |

## 2. Commands and observed output

### Backend

```powershell
.\test-local.ps1

# Equivalent explicit sequence after loading .env.local:
dotnet clean backend\SecureVoting.slnx -c Release
dotnet build backend\SecureVoting.slnx -c Release --no-restore
dotnet test backend\tests\SecureVoting.UnitTests\SecureVoting.UnitTests.csproj -c Release --no-build
dotnet test backend\tests\SecureVoting.IntegrationTests\SecureVoting.IntegrationTests.csproj -c Release --no-build
```

Observed:

```text
Build succeeded. 0 Warning(s) 0 Error(s)
UnitTests:        Passed! Failed: 0, Passed: 27, Skipped: 0, Total: 27
IntegrationTests: Passed! Failed: 0, Passed: 33, Skipped: 0, Total: 33
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

### Final installed-PostgreSQL E2E

```powershell
.\database\setup-local.ps1
.\run.cmd
Invoke-WebRequest http://localhost:5000/health
$env:SECUREVOTE_E2E_URL='http://localhost:5173'
cd frontend
npx playwright test --project=chromium
```

Observed:

```text
health: Healthy (installed PostgreSQL 18)
Playwright: 3 passed against http://localhost:5173
- electoral officer sees the administration workspace
- observer can open the audit trail and verify the chain
- secure departmental election from setup through publication and student result
```

No Docker container was started for this local run. The application used the non-superuser
`securevote_app` role against the installed PostgreSQL service.

### Dependency audits

```powershell
dotnet list backend\SecureVoting.slnx package --vulnerable --include-transitive
# all projects: no vulnerable packages reported

cd frontend
npm audit --omit=dev
# found 0 vulnerabilities
```

## 3. New Phase 10 integration coverage

The new `DepartmentIsolationTests` class covers seven scenarios:

1. `SuperAdminManagesDepartmentsAndDuplicateCodesAreRejected` — create department, reject duplicate
   normalized code, deactivate, reject election creation for inactive department, reactivate.
2. `OfficerCanCreateOnlyForAssignedDepartments` — CS officer denied for Math; Math officer denied for
   CS; each can create within its assigned department.
3. `CrossDepartmentReadsAndWritesAreRejected` — cross-department detail/voter/action/observer/result
   review/student election/ballot/published-result requests return 403; admin and observer lists omit
   the other department.
4. `VoterImportRejectsMismatchedDepartmentWithoutLeakingTokens` — mismatched CSV department produces
   a row-level error, zero processed rows and no activation tokens.
5. `ObserverAuditAndReviewListsAreDepartmentScoped` — an observer's audit and result-review lists
   contain only assigned-department elections; another department's observer sees its event.
6. `ScopedObserverCanVerifyOnlyAssignedDepartmentResult` — CS observer gets 403 on Math verification;
   Math observer verifies its election successfully.
7. `IndependentElectionsEnforceEligibilityAndOneVote` — CS and Math elections open independently; each
   student can vote only in its own election; duplicate CS submission returns 409; each election has
   exactly one ballot.

All pre-existing Phase 9 security, ballot, result, audit, activation, photo, CSV and rate-limit tests
continue to pass. No test was removed or weakened.

## 4. Migration compatibility drill

A disposable PostgreSQL container was started on host port 55434. The database was migrated to the
prior `InitialCreate` migration, then synthetic legacy data was inserted:

- a synthetic officer account;
- an election with `Department = 'Computer Science'`;
- a student with `Department = 'Computer Science'`.

The latest migration was applied:

```powershell
$env:ConnectionStrings__Default='Host=localhost;Port=55434;Database=securevote;Username=securevote;Password=...'
dotnet ef database update 20261028224226_InitialCreate --project ..\SecureVoting.Infrastructure\SecureVoting.Infrastructure.csproj --startup-project SecureVoting.Api.csproj
dotnet ef database update --project ..\SecureVoting.Infrastructure\SecureVoting.Infrastructure.csproj --startup-project SecureVoting.Api.csproj
```

Observed after migration:

```text
Departments: Computer Science / LEGACY-80C18801 / active
Legacy election: DepartmentId resolved to the same department
Legacy student: DepartmentId resolved to the same department
Audit rows without department: 0
```

The container was removed after the drill. This verifies preservation of synthetic legacy rows and
backfill on a disposable database. It does not replace an institution-specific upgrade rehearsal or
acceptance of legacy department codes.

## 5. Explicitly not verified / not claimed

| Area | Status |
|---|---|
| Real departmental election | Not run |
| Real student data | Not used |
| Institutional identity provider / biometrics | Not integrated |
| SMS OTP/MFA | Not implemented |
| SMTP password-recovery delivery | Not E2E tested |
| Independent external penetration test | Not performed |
| Physical modem/SIM/Nigerian mobile network | Not tested |
| Multi-node high availability/failover | Not tested |
| Post-Phase-10 300-voter load | Not rerun; Phase 9 results remain historical |
| Institutional acceptance of legacy department codes | Not performed |
| Signed installer / clean institutional Windows image | Not performed |
| Real election legal/electoral procedure | Not performed |

## 6. Local PostgreSQL verification

Installed PostgreSQL 18 was configured with `database/setup-local.ps1`, which created the
non-superuser `securevote_app` role, the `securevote` and `securevote_test` databases, and ignored
`.env.local` connection settings. `test-local.ps1` then ran the full backend suite without Docker:

```text
UnitTests:        Passed! 27 passed
IntegrationTests: Passed! 33 passed
```

The local API and Vite SPA were started with `run.cmd`; `/health` returned healthy and the three
Playwright tests passed against `http://localhost:5173`.

## 7. Final conclusion

The software passes 69 automated checks, a clean Release build, the complete synthetic E2E election
flow, the expanded cross-department isolation tests, dependency audits and a legacy-data migration
drill. It supports independent departmental elections within the implemented software scope with
synthetic data and server-side department assignments.

It is suitable for a controlled academic demonstration and further laboratory validation. It is not
certified, not hardware/institution-integrated and not ready for a real university election.
