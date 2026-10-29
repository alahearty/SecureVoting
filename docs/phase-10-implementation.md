# Phase 10 - University-Wide Department Implementation

Date: 2026-10-29  
Project title: **Design and Implementation of a Secure Electronic Voting System for Departmental
Elections**  
System scope: a configurable electronic voting platform designed to support departmental elections
across authorised departments of Ignatius Ajuru University of Education.

Computer Science is the academic discipline in which the project is undertaken. It is not the
exclusive department the software serves.

## 1. Objective and constraints

The existing application used free-text department values and global staff roles. Phase 10 adds a
proportionate university organisational model and server-side departmental isolation while preserving:

- the existing Vue 3/ASP.NET Core/PostgreSQL architecture;
- the anonymous ballot and one-vote transaction;
- election lifecycle and result verification/publication controls;
- the audit hash chain and result integrity hash;
- the user's `.env` and Phase 9 evidence;
- existing tests, which were extended rather than weakened.

No real student data, election or credentials were used.

## 2. Implemented model

### Department

A new `Department` entity stores:

- GUID identifier;
- name;
- code;
- normalized code (case-insensitive unique);
- optional faculty/school name;
- active status;
- creation and update timestamps.

Department codes are unique through `CodeNormalized`. Departments are deactivated, never deleted.
Existing elections remain linked and readable; deactivation blocks new election creation and voter
imports.

### Election and student association

`Election` and `Student` now have a required `DepartmentId` foreign key. The original `Department`
string columns are retained as legacy display/migration data but application behaviour and DTO
department names/codes come from the `Departments` relation. Voter eligibility remains a separate
per-election record; no rule assumes every student in a department is eligible.

### Scoped staff access

The existing roles are retained:

- `SuperAdministrator` — university-wide oversight and department administration;
- `ElectoralOfficer` — election management only for assigned departments;
- `Observer` — election review/verification only for assigned departments;
- `Student` — eligibility-scoped elections and results;
- `Candidate` — candidate actions within an eligible election.

`DepartmentAssignment` records link an officer or observer user to a department with a scope. Only
users holding the corresponding role can receive that scope. A user may be assigned to more than one
department. Super Administrators do not need assignments and retain documented university-wide access.

### Access enforcement

`DepartmentAccessService` enforces scope in server-side controllers and critical services:

- election creation/update/positions/voter import/candidate decisions/lifecycle;
- admin and observer election lists;
- result review, observer verification and privileged result reads;
- published result access (eligible student or scoped staff);
- candidate photo access;
- announcement creation/read and notification recipients;
- observer audit-list filtering and dashboard metrics/charts/audit.

Frontend route guards and filters are usability controls only. Cross-department IDs are rejected by
the API even when a caller edits a URL or request payload.

## 3. Department lifecycle

- Only Super Administrators can create, update, activate or deactivate departments.
- Codes are normalized and duplicate codes are rejected case-insensitively.
- Update does not rewrite historical election results or delete student/eligibility rows.
- Deactivation preserves elections, voter eligibility, ballots, results and audit evidence.
- New elections and voter imports are blocked for inactive departments.
- Historical elections can still be closed, reviewed and published under the normal lifecycle.

## 4. Voter import and isolation

`StudentImportService` now:

1. verifies the actor may manage the election's department;
2. rejects imports when the department is inactive;
3. checks the election status (draft/scheduled only);
4. verifies the CSV `department` value against the election department name **or code**;
5. rejects a row when an existing matriculation number belongs to another department;
6. never moves an existing student to another department;
7. returns row-level errors without activation tokens for rejected rows;
8. keeps eligibility and entitlement creation inside the existing transaction.

## 5. Preserved security properties

- Ballot and selection tables still have no student/user identity columns.
- One entitlement is consumed atomically; duplicate/concurrent submissions still return conflicts.
- Election row locking still serializes trusted-time/window decisions.
- Result snapshots still use the full-field integrity hash and verify on read.
- Observer verification and Super Administrator publication remain separate.
- Audit entries remain hash-chained; department scope metadata is not added to the hash, so existing
  historical hashes are not rewritten. Observer audit visibility is filtered by department metadata.
- No new role was introduced.

## 6. Migration

Migration: `20261029120236_AddDepartmentsAndScopedAccess`.
Migration files:

- `20261029120236_AddDepartmentsAndScopedAccess.cs`
- `20261029120236_AddDepartmentsAndScopedAccess.Designer.cs`
- updated `AppDbContextModelSnapshot.cs`

The migration:

1. adds nullable `DepartmentId` columns to `Students`, `Elections` and `AuditLogs`;
2. creates `Departments` and `DepartmentAssignments`;
3. creates one department per distinct legacy string, using `LEGACY-<hash>` codes;
4. provides an `UNASSIGNED` department for empty legacy values;
5. backfills students and elections by case-insensitive department name;
6. backfills election audit entries where the entity is an election;
7. alters the student/election foreign keys to required;
8. creates indexes and restrictive foreign keys.

The legacy string columns are retained. No historical rows are deleted. Generated legacy codes are
administrative metadata; a seeded demonstration reuses a matching legacy department name and replaces
its code with the canonical sample code. Production upgrades should review legacy departments and
assign officers/observers explicitly.

## 7. Synthetic data

The demo seed now creates three clearly labelled synthetic departments:

| Department | Code | Sample election |
|---|---|---|
| Computer Science | CSC | 2026/2027 Computer Science Departmental Election |
| Mathematics | MTH | 2026/2027 Mathematics Departmental Election |
| Physics | PHY | 2026/2027 Physics Departmental Election |

The Computer Science election retains the existing full demo fixtures used by tests. Mathematics and
Physics have six synthetic students each, positions, approved synthetic candidates and draft
elections. Additional scoped staff accounts are seeded for demonstration. No real students,
candidates, outcomes or institutional approvals are represented.

## 8. User interface

- Department dropdown in election creation and draft editing.
- Department filter and code display in election administration.
- Department context on management, voter register, candidate, observer, result and student pages.
- Super Administrator department management page.
- Department scope assignment dialog in user administration.
- University-wide branding replaces the former CS-only labels.
- Empty states cover no departments, no scoped departments, no elections and no results.

## 9. Verification summary

Final verified results:

| Check | Result |
|---|---|
| Clean Release build | 0 warnings / 0 errors |
| Backend unit tests | 27 passed |
| Backend integration tests | 33 passed, including 7 new department-isolation tests |
| Frontend unit tests | 6 passed |
| Playwright E2E | 3 passed against the installed-PostgreSQL local stack |
| Total automated checks | 69 |
| Local PostgreSQL | Installed PostgreSQL 18; non-superuser `securevote_app`; `securevote` + `securevote_test` |
| Migration compatibility drill | Passed on disposable PostgreSQL with legacy rows |
| Optional Docker runtime | Previously verified non-root `app`, UID 1654 |
| NuGet/npm vulnerability audit | 0 known vulnerabilities reported |

The complete commands and migration evidence are in
[phase-10-test-results.md](phase-10-test-results.md).

## 10. Unresolved concerns

- The application still uses matriculation-number/account activation, not an institutional identity
  provider or biometric proof.
- Department access depends on correct assignment by a Super Administrator. Misassignment is an
  institutional operational risk; it is audited but not automatically detected.
- Audit department metadata is visibility metadata and is not part of the hash chain. A privileged
  database administrator can alter it; the hash chain still protects the event content that it covers.
- Existing legacy departments receive generated codes during migration; an institution must review
  names/codes and assignments before production use.
- There is no university-wide faculty entity; faculty remains an optional display string.
- The Phase 9 300-voter performance evidence predates this schema change. It is retained as historical
  evidence but was not rerun against the new department schema.
- No external penetration test, real hardware, institutional approval or real election was performed.
- No automated E2E test for the Super Administrator department assignment dialog was added; backend
  isolation and UI build/typecheck cover the implemented control.

## 11. Final assessment

**The application now genuinely supports independent departmental elections within the implemented
software scope.**

It is suitable for a controlled academic demonstration with three synthetic departments and for
further laboratory validation. It is **not** certified for a real departmental election. Real use
requires institutional identity integration, reviewed department assignments, migration acceptance,
external security assessment, TLS/proxy hardening, backup/key protection and formal electoral
approval.
