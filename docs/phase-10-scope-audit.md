# Phase 10 - University-Wide Department Scope Audit

Date: 2026-10-29  
Repository: `C:\SyncGrass Limited\Topics\SecureVoting`  
Comparison point: `artifacts/SecureVoting-source.zip` (Phase 9 final archive) and the live working tree.

## 1. Purpose

The project title is *Design and Implementation of a Secure Electronic Voting System for
Departmental Elections*. Computer Science is the discipline in which the project is being undertaken,
not the only department the software may serve. This audit identifies assumptions that make the
current implementation single-department and defines a proportionate design for independent
departmental elections across authorised departments of Ignatius Ajuru University of Education.

No real election, real student data or production credentials were used. The local `.env` and Phase 9
evidence were preserved.

## 2. Department-specific assumptions found

| Area | Existing assumption | Evidence |
|---|---|---|
| Student model | Department is a required free-text string | `Student.Department`; `Students` table |
| Election model | Department is a required free-text string | `Election.Department`; `Elections` table |
| Department identity | No department table, unique code, faculty association, active flag or timestamps | `AppDbContext`; initial migration |
| Election creation | Any officer may enter any department text; no server-side assignment check | `CreateElectionRequest.Department`; `ElectionAdminService.CreateAsync` |
| Election listing | Admin and observer lists return every election | `ElectionReadService.ListForAdminAsync` / `ListForObserverAsync` |
| Election detail/results | Access by election GUID only; no department scope | `AdminElectionsController`, `ObserverController`, `ResultsController` |
| Voter import | Free-text `department` compared to election string; existing student department can be overwritten | `StudentImportService` |
| Student creation | Department copied from the election string | `StudentImportService` |
| Roles | Global `ElectoralOfficer`, `Observer`, `SuperAdministrator`; no department assignment | `RoleNames`; authorization policies |
| Observers | See all elections and the global audit list | `ObserverController`, `AuditController`, `DashboardService` |
| Results | Any authenticated user can read a published result by election GUID | `ResultsController.Published` |
| Candidate photos | Any officer/observer/superadmin can read any candidate photo | `CandidatesController.Photo` |
| Announcements | Staff announcements are not scoped; global announcements are unrestricted | `AnnouncementService`, `ElectionReadService.GetAnnouncementsAsync` |
| Audit scope | Audit entries have no department/election scope metadata | `AuditLog`; `AuditController` |
| Seed | Hard-coded Computer Science title, students, dashboard branding and one election | `DatabaseSeeder` |
| Meta/settings | `DepartmentName` setting defaulted to `Computer Science`; branding says `IAUE CS Department` | `MetaController`, `SettingsView`, `AppShell`, `AuthLayout` |
| Election UI | Department is a free-text input; create form defaults to Computer Science | `CreateElectionView`, `ManageElectionView` |
| Tests/load | Fixtures and load selector assume Computer Science strings and one demo election | Integration tests, load test, Playwright |
| Documentation | README and academic documents describe a departmental/CS deployment rather than a university-wide platform | README, architecture, deployment, test report, defence outline |

## 3. What is legitimate and should remain

- The project is undertaken in Computer Science; the academic discipline and university names are
  legitimate.
- Computer Science must remain a supported department and the existing synthetic demo election remains
  valid as one sample.
- Ballot anonymity, one-vote enforcement, lifecycle rules, activation tokens, audit hash chain, result
  integrity and observer/publication separation are not department-specific and must be preserved.
- Student eligibility remains an explicit per-election record. The system must not assume every member
  of a department is eligible for every election.

## 4. Chosen design

Keep the existing stack and architecture. Add a proportionate organisational scope model:

1. **Department entity** with `Id`, `Name`, `Code` (normalized unique), optional `Faculty`, `IsActive`,
   `CreatedAtUtc` and `UpdatedAtUtc`.
2. **DepartmentId foreign keys** on `Election` and `Student`; retain legacy string columns for
   migration compatibility and display fallback, but all new behaviour and DTO display use the
   department relation. Historical elections are not deleted or rewritten.
3. **Department assignments** for existing `ElectoralOfficer` and `Observer` roles:
   `DepartmentAccess` rows identify a user, department and access scope. Super Administrators retain
   university-wide oversight by role. Students do not need assignments; their eligibility records
   determine election access.
4. **Server-side access service** used by controllers and critical services to enforce create, read,
   update, observe, result and audit scope. Frontend filtering is not an authorization boundary.
5. **Department-scoped reads** for admin/observer election lists, dashboards, result review, published
   results, audit list and announcements.
6. **Department-aware voter import**: the department column must match the election department's name
   or code; a student from another department cannot be silently moved. Eligibility remains explicit
   per election.
7. **Synthetic seed** with three clearly labelled departments and synthetic elections/students. No real
   names, records or outcomes.
8. **Audit access metadata**: add a non-hashed department scope field to audit rows for observer
   filtering; document that this metadata controls visibility, while the existing hash chain remains
   the tamper-evidence mechanism. Do not rewrite historical hashes.

## 5. Deliberate exclusions

- No separate faculty entity unless needed for the department association; `Faculty` is a nullable
  display field, which keeps the model proportionate.
- No new application role. Existing roles are retained; department scope is an assignment layer.
- No cryptographic claim that scoping makes a privileged database administrator unable to correlate
  ballots. Existing residual anonymity risks remain.
- No deletion of departments or historical elections. Deactivation is the lifecycle operation.
- No claim of university-wide operational approval. This is implemented and tested software, not an
  institutional production deployment.

## 6. Acceptance criteria for implementation

- At least three departments can coexist; duplicate codes are rejected; inactive departments cannot
  receive new elections/imports.
- An officer can create/manage only assigned departments; an observer can see/verify only assigned
  elections; SuperAdmin retains explicitly documented university-wide oversight.
- Cross-department ID tampering is rejected server-side and covered by negative integration tests.
- Department-specific imports accept matching name/code and reject mismatches per row without exposing
  tokens.
- Separate department elections do not share eligibility, candidates, ballots or results.
- Anonymous ballot and one-vote invariants remain intact.
- Migrations preserve existing records and are exercised against a disposable PostgreSQL database.
- All existing tests plus new department/isolation tests pass; frontend and Playwright tests are updated
  and executed.

## 7. Implementation status

Implemented and verified in Phase 10. Final evidence: 27 backend unit, 33 backend integration, 6
frontend unit and 3 Playwright checks (69 total); clean Release build; disposable-PostgreSQL legacy
migration drill; installed PostgreSQL 18 local startup; dependency audits; optional non-root Docker runtime. See
[phase-10-implementation.md](phase-10-implementation.md) and
[phase-10-test-results.md](phase-10-test-results.md).

Decision: the application now supports independent departmental elections within the implemented
software scope with synthetic data and scoped staff assignments. It is suitable for academic
demonstration/controlled laboratory validation, not a real university election.