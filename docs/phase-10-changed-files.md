# Phase 10 - Changed Files and Migration

Date: 2026-10-29  
Comparison: Phase 9 final source archive vs current working tree.

No source files were removed. The local `.env` and Phase 9 evidence were preserved.

## Database migration

| File | Purpose |
|---|---|
| `backend/src/SecureVoting.Infrastructure/Persistence/Migrations/20261029120236_AddDepartmentsAndScopedAccess.cs` | Adds Departments, DepartmentAssignments, required DepartmentId FKs, audit scope metadata, legacy backfill and indexes/FKs |
| `.../20261029120236_AddDepartmentsAndScopedAccess.Designer.cs` | Generated model snapshot for the migration |
| `.../AppDbContextModelSnapshot.cs` | Updated current model snapshot |

## Domain and persistence

| File | Change |
|---|---|
| `backend/src/SecureVoting.Domain/Departments.cs` | New Department, DepartmentAssignment and DepartmentAccessScope model |
| `backend/src/SecureVoting.Domain/Students.cs` | DepartmentId and DepartmentRef navigation |
| `backend/src/SecureVoting.Domain/Elections.cs` | DepartmentId and DepartmentRef navigation |
| `backend/src/SecureVoting.Domain/Content.cs` | AuditLog DepartmentId visibility metadata |
| `backend/src/SecureVoting.Infrastructure/Persistence/AppDbContext.cs` | Department/assignment DbSets, indexes, relationships and audit FK |
| `backend/src/SecureVoting.Infrastructure/Persistence/DatabaseSeeder.cs` | Three synthetic departments/elections, scoped sample staff, students and candidates |

## Application contracts and services

| File | Change |
|---|---|
| `backend/src/SecureVoting.Application/Contracts/DepartmentContracts.cs` | New department CRUD/assignment DTOs and requests |
| `backend/src/SecureVoting.Application/Contracts/ElectionContracts.cs` | DepartmentId on create, department code/name in summaries/details, candidate department context |
| `backend/src/SecureVoting.Application/Contracts/BallotContracts.cs` | DepartmentId/code in ballot context |
| `backend/src/SecureVoting.Application/Contracts/MetaContracts.cs` | Removed single-department meta field |
| `backend/src/SecureVoting.Infrastructure/Departments/DepartmentAccessService.cs` | Server-side manager/observer/student/result access checks |
| `backend/src/SecureVoting.Infrastructure/Departments/DepartmentService.cs` | Department CRUD, activation and scoped assignment management |
| `backend/src/SecureVoting.Infrastructure/Elections/ElectionAdminService.cs` | Department validation/scope on create, update, positions and lifecycle |
| `backend/src/SecureVoting.Infrastructure/Elections/ElectionReadService.cs` | Scoped admin/observer/review lists, department DTO mapping and announcement filtering |
| `backend/src/SecureVoting.Infrastructure/Students/StudentImportService.cs` | Department name/code validation, cross-department rejection and DepartmentId writes |
| `backend/src/SecureVoting.Infrastructure/Candidates/CandidateService.cs` | Department context and officer scope checks for candidate decisions |
| `backend/src/SecureVoting.Infrastructure/Dashboard/DashboardService.cs` | Scoped admin/observer metrics, charts and recent audit |
| `backend/src/SecureVoting.Infrastructure/Announcements/AnnouncementService.cs` | Scoped creation, recipients and reads |
| `backend/src/SecureVoting.Infrastructure/Auditing/AuditWriter.cs` | Resolves department visibility metadata without changing the audit hash fields |
| `backend/src/SecureVoting.Infrastructure/Identity/UserAdminService.cs` | Removes department assignments when the corresponding role is removed |
| `backend/src/SecureVoting.Infrastructure/Ballots/BallotService.cs` | Department context in ballot DTO |

## API

| File | Change |
|---|---|
| `backend/src/SecureVoting.Api/Program.cs` | Registers department services and grants Super Administrator election management under explicit oversight |
| `AdminDepartmentsController.cs` | SuperAdmin department CRUD/activate/deactivate |
| `DepartmentsController.cs` | Authenticated active department list scoped to current user |
| `AdminUsersController.cs` | Scoped department assignment GET/PUT |
| `AdminElectionsController.cs` | Department scope on detail/voters/candidates/decisions |
| `ObserverController.cs` | Observer-scoped lists/details |
| `ResultReviewController.cs` | Scoped review list/detail |
| `ResultsController.cs` | Eligible/scoped published-result/privileged reads |
| `DashboardController.cs`, `AnnouncementsController.cs`, `AuditController.cs` | Scoped dashboard, announcement and audit data |
| `CandidateController.cs` | Eligibility check on candidate election detail |
| `CandidatesController.cs` | Department/eligibility-scoped photo reads |
| `MetaController.cs`, `AuthController.cs` | Remove single-department default; return actual student department context |

## Frontend

| File | Change |
|---|---|
| `frontend/src/api/types.ts` | Department DTOs, assignments, DepartmentId/code fields, candidate department fields |
| `frontend/src/views/admin/DepartmentsView.vue` | New department management UI |
| `frontend/src/views/admin/CreateElectionView.vue` | Department dropdown and no CS default |
| `frontend/src/views/admin/ManageElectionView.vue` | Department edit dropdown and department context |
| `frontend/src/views/admin/AdminElectionsView.vue` | Department filter/code display |
| `frontend/src/views/admin/UsersView.vue` | Department scope assignment dialog |
| `frontend/src/views/admin/AdminDashboardView.vue`, `SettingsView.vue` | University-wide/scope navigation and removal of single-department setting |
| `frontend/src/components/AppShell.vue`, `AuthLayout.vue` | University-wide branding |
| `frontend/src/router/index.ts` | Department route and SuperAdmin election routes |
| Student/observer/result/candidate views | Department context and scoped empty states |
| `frontend/e2e/election.spec.ts` | Fetches the CSC department and creates the E2E election by DepartmentId |

## Local PostgreSQL support

| File | Change |
|---|---|
| `database/setup.sql` | Creates the non-superuser `securevote_app` role and `securevote`/`securevote_test` databases |
| `database/setup-local.ps1` | Finds `psql`, applies setup, and writes ignored `.env.local` connection/test settings |
| `run-local.ps1`, `run.cmd` | Starts API + Vite against installed PostgreSQL; no Docker required |
| `test-local.ps1`, `test.cmd` | Loads local test connection and runs backend suites without Docker |
| `.env.example` | Documents local connection strings and optional Docker variables |
| `backend/src/SecureVoting.Api/wwwroot/.gitkeep` | Keeps the API web-root directory present for direct local startup |
| `docs/local-postgresql.md` | Local setup, run, test and troubleshooting instructions |
| `backend/tests/SecureVoting.IntegrationTests/IntegrationApiFixture.cs` | Uses `SECUREVOTING_TEST_CONNECTION` when set; safe `_test` database guard; Docker fallback retained |

## Tests

| File | Change |
|---|---|
| `backend/tests/SecureVoting.IntegrationTests/DepartmentIsolationTests.cs` | New 7-test department/isolation suite |
| `IntegrationApiFixture.cs` | Department/election lookup helpers |
| `CandidateWorkflowTests.cs` | DepartmentId election creation |
| `DashboardAndAnnouncementTests.cs` | Election-scoped announcement test |

## Documentation

Added:

- `docs/phase-10-scope-audit.md`
- `docs/phase-10-implementation.md`
- `docs/phase-10-test-results.md`
- `docs/phase-10-demo-scenario.md`
- `docs/phase-10-changed-files.md`

Updated: `README.md`, `SECURITY.md`, `TESTING.md`, `CHANGELOG.md`, `IMPLEMENTATION_PLAN.md`,
`docs/architecture.md`, `docs/database.md`, `docs/threat-model.md`, `docs/deployment.md`,
`docs/requirements.md`, `docs/administrator-guide.md`, `docs/test-report.md`, `docs/security-findings.md`,
`docs/defence-outline.md`, `docs/project-report-outline.md`, `docs/demo-script.md`.

## Packaging

Final source archive: `artifacts/SecureVoting-source.zip`. It excludes `.env`, `.env.local`, build output,
`node_modules`, `dist`, test results, local databases and `artifacts/phase10` test configuration. The
final SHA-256 is reported in the delivery response.
