# Requirements Specification

## Project identity

**Design and Implementation of a Secure Electronic Voting System for Departmental Elections**  
Ignatius Ajuru University of Education, Port Harcourt, Rivers State, Nigeria  
Department of Computer Science - Final-Year Undergraduate Project

The product name used in the application is **IAUE SecureVote**.

## Purpose and scope

IAUE SecureVote is a configurable electronic voting platform for conducting independent departmental
student elections across authorised departments of Ignatius Ajuru University of Education. Computer
Science is the academic discipline in which the project is undertaken, not the exclusive department the
software serves. It provides controlled student account provisioning, university department
configuration, department-scoped election and candidate administration, a privacy-conscious ballot
submission workflow, aggregate result calculation, approval/publication, audit logging and role-specific
dashboards.

The system is an academic project. It is not certified by any electoral commission, does not replace
university electoral regulations, and must not be used for a real election without institutional
approval, independent security review, physical/operational testing and an agreed dispute procedure.

## Users and roles

| Role | Primary capabilities |
|---|---|
| Super Administrator | User/role administration, security settings, audit/system health, publication approval |
| Electoral Officer | Elections, positions, eligibility imports, candidate approval, lifecycle, result review |
| Student/Voter | Eligible elections, approved candidates, ballot submission, confirmation, published results |
| Candidate | Nomination/profile/manifesto/photo, approval status, official information |
| Independent Observer/Auditor | Read-only election configuration, aggregates, scoped audit events and result verification only for assigned departments |

Authorization is enforced by server-side policies plus department assignments. Frontend route hiding
and filtering are usability only. No user can gain broader access by changing a department/election ID
in a URL, query or request body.

## Functional requirements

### Authentication and student management

- Controlled provisioning, not open self-registration.
- Matriculation number validation and duplicate detection.
- CSV voter-register import per election.
- One-time activation token delivery to the electoral officer for distribution; token stored only as a
  SHA-256 hash and expires.
- ASP.NET Core Identity password hashing; no custom password hashing.
- Secure cookie login/logout, account activation, suspension and password reset.
- Login rate limiting plus Identity lockout.
- Role assignment restricted to Super Administrator.
- Server-side validation and authentication/authorization audit events.
- Optional institutional MFA is documented as a production extension and is not claimed as implemented.
- Possession of a matriculation number is not treated as proof of identity; activation tokens are
  distributed through a controlled administrative channel. Residual identity-proofing risk is documented.

### Department management and election scoping

- SuperAdministrator-managed departments with unique code, name, optional faculty, active state and timestamps.
- Deactivation preserves historical elections and records; no destructive department deletion.
- Every election and student has a department; eligibility remains explicit per election.
- Officers/observers receive explicit department scopes; cross-department access is rejected server-side.
- Department-specific CSV imports validate department name/code and report row-level errors without tokens.
- Separate departments cannot share voter eligibility, candidates, ballots or results by changing IDs.

### Election management

- Create/edit elections while Draft.
- Title, description, academic session, department, nomination window, voting window.
- Positions, seats, maximum selections, abstention and tie-handling rules.
- Eligibility import and candidate approval.
- Lifecycle:
  `Draft -> Scheduled -> Open -> Closed -> UnderReview -> Published`
  with explicit `Suspended` and terminal `Cancelled` states.
- Server-side UTC time checks; ballots accepted only while status is Open and the server time is inside
  the voting window.
- Early close, suspension, resumption and cancellation require reasons and audit entries.
- Published election configuration and candidate data are frozen.

### Candidate management

- Student nomination in Draft/Scheduled election before the nomination deadline.
- Position assignment, manifesto and optional photograph.
- Eligibility check against the election voter register.
- Electoral officer approval/rejection with reason; optional disqualification and withdrawal.
- Candidate role granted on approval.
- Approved details frozen when voting opens.
- Rejection/disqualification/withdrawal preserve history and audit records.

### Ballot privacy and one-vote enforcement

Separate models are used for:

1. identity and student records;
2. election eligibility;
3. one-vote entitlement and confirmation code;
4. anonymous accepted ballot and selections;
5. internal ballot receipt;
6. aggregate result snapshots.

An `VoterEntitlement` row is unique per election/student. Ballot submission:

1. validates authenticated active student and eligibility;
2. validates election status/window;
3. loads and validates every position selection against approved candidates;
4. opens a database transaction;
5. conditionally consumes the entitlement with
   `UPDATE ... WHERE ConsumedAtUtc IS NULL`;
6. inserts a ballot with a random ID and no voter identity;
7. inserts selections/abstentions and an internal receipt;
8. appends the audit entry;
9. commits;
10. returns a random non-revealing confirmation reference whose hash is stored only on the entitlement.

A second or concurrent submission finds no unconsumed entitlement and is rejected with HTTP 409.
The ballot table contains no student ID, matriculation number, receipt token, IP address or user agent.
Residual timing/infrastructure correlation risks are documented in [../SECURITY.md](../SECURITY.md).

### Results

- Counts derived only from accepted ballot rows, never from frontend totals.
- Per-position/candidate counts, percentages, abstentions, turnout and reconciliation.
- Rank/winner calculation with configurable tie handling:
  `RunoffRequired`, `SharedOffice`, `ManualResolution`.
- Provisional snapshot, observer verification, Super Administrator publication.
- Published results immutable; corrections require a new version with reason and approval.
- CSV export and print-to-PDF from a print-friendly result view.
- Published results visible to students; unpublished aggregates restricted.

### Dashboards, announcements and notifications

- Role-specific dashboards using aggregate counts.
- No voter-to-choice mapping in charts, exports or audit.
- Announcements with audience and publication time.
- Extensible notification records; SMTP email support configured by environment.
- No ballot choices or credentials in email, logs or notifications.

### Audit

- Append-only application model with a SHA-256 hash chain and serialized chain state.
- Events cover authentication, roles, election changes, imports, candidate decisions, lifecycle,
  ballot acceptance (without choices), result verification/publication and maintenance.
- Verification endpoint recomputes the chain.
- Audit excludes ballot choices, passwords, tokens, OTPs and unnecessary personal data.
- A hash chain is tamper-evident, not tamper-proof against a privileged database administrator.
  External checkpoints are a documented advanced extension.

## Non-functional requirements

| Area | Target |
|---|---|
| Availability | Single-instance departmental deployment; health endpoint and database readiness |
| Correctness | Transactional ballot enforcement and aggregate recount |
| Performance | 300 eligible voters, 95th percentile ballot submission under 2 s on a reference 2 vCPU/4 GB host (to be load-tested) |
| Concurrency | No more than one accepted ballot per entitlement under simultaneous requests |
| Security | HTTPS deployment, secure cookies, CSRF protection, RBAC, rate limiting, security headers |
| Privacy | Separate identity/choice models; no choices in logs/exports outside aggregate results |
| Accessibility | Keyboard-operable forms, visible focus, labels, contrast-aware palette, status text |
| Responsiveness | Mobile, tablet and desktop layouts |
| Maintainability | Layered backend, typed frontend, migrations, tests, documented decisions |
| Recoverability | PostgreSQL backup/restore procedure and documented rollback |

Actual measured results, including any not performed, are recorded in `TESTING.md`.

## Explicit assumptions

- Election eligibility is defined by an officer-imported departmental register rather than an
  unverified student self-claim.
- Matriculation numbers are unique within the departmental register; the import validates format but
  does not prove real-world identity.
- One student may contest at most one position in one election.
- Candidates are students; candidate accounts may also vote when eligible.
- A student may be eligible for multiple elections, but each election has at most one ballot.
- Voting is single-choice/multi-choice per configured position; no ranked-choice voting.
- There is no remote voting receipt verification endpoint; the confirmation reference only proves
  submission to the voter who holds it.
- Email delivery is best-effort. The system does not claim delivery without an SMTP provider.
- The deployment has a trusted server clock and TLS-terminating reverse proxy.
- Local demonstration accounts are synthetic and must never be used for real elections.

## Out of scope for this implementation

- University-wide multi-department election administration.
- Biometric identity verification or integration with a university student information system.
- Cryptographic end-to-end verifiable voting (homomorphic tally, mixnets, paper audit trails).
- Ranked-choice/STV voting.
- Native mobile applications.
- SMS OTP/MFA and SMS notifications.
- Automated external timestamping/notarisation of audit checkpoints.
- Legal certification or official university election procedure automation.

## Acceptance criteria

The application is acceptable for **academic demonstration** when it builds, migrates a clean
PostgreSQL database, seeds/activates demonstration accounts, lets an officer create/schedule/open an
election and approve candidates, restricts the ballot to eligible students, accepts one ballot per
voter, rejects duplicates/forgeries/out-of-window submissions, keeps identity out of ballot selections,
calculates totals from accepted ballots, requires verification before publication, records audit
events, passes automated tests, and follows verified deployment/restore instructions. Physical
election use remains explicitly out of scope until the residual risks are resolved.
