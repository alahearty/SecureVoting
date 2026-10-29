# Electoral Administrator Guide

## Roles and boundaries

| Role | Can do | Cannot do |
|---|---|---|
| Super Administrator | Manage departments, scoped staff assignments, users/roles, settings, audit, publish verified results, request recounts | Cast/manipulate ballots; alter submitted ballots |
| Electoral Officer | Create/configure elections, import register, approve candidates, open/close, request review only for assigned departments | Publish results; manage system roles/settings; work in unassigned departments |
| Student | Vote once when eligible, view published results | Access administration or other students' private records |
| Candidate | Manage own nomination/profile; view approval status and notices | Access voter data, vote on behalf of another student, change results |
| Observer | Read aggregate configuration/results, verify snapshots and read audit trail only for assigned departments | Administer elections, alter votes/results; inspect unassigned departments |

All checks are server-side. Do not share a privileged account.

## Prepare a demonstration

1. Start the stack using [deployment.md](deployment.md). Use demo credentials only in a development or
   lab environment.
2. Sign in as the Super Administrator and configure **Departments** plus scoped officer/observer assignments. Assign each officer only to the department(s) they may manage.
3. Sign in as the assigned Electoral Officer.
4. Open **Admin overview -> New election**.
5. Select the authorised department and enter title, session, nomination and voting windows. Save the draft.
6. Add at least one position. Set seats and maximum selections per voter.
7. On **Voter register**, import a CSV with `matric,fullname,email,level,department`; the department must match the election name or code.
8. Distribute each one-time activation token through an approved secure channel. The plaintext token is
   shown only in the import result; only a hash is stored.
9. Students nominate themselves from the Candidate portal. Approve/reject on **Candidates**.
10. Schedule the election. At the authorised time, use **Open voting**.
11. Close voting after the scheduled period. Use **Send for result review**.
12. The Observer signs in and verifies the aggregate snapshot with notes.
13. The Super Administrator signs in, opens **Result publication**, adds publication notes and publishes.
14. Students can now see the result under **Published results**.

## CSV import rules

```csv
matric,fullname,email,level,department
IAUE/CS/26/001,Ada Example,ada@example.edu.ng,300,Computer Science
```

- Up to 5,000 rows and 2 MB.
- Matriculation numbers and emails must be unique across the register.
- Department must match the election department name or code. A student from another department is rejected; the import never moves a student between departments.
- Level must be 100–900.
- The import is all-or-nothing; row errors are returned and no changes are committed if any row fails.
- New accounts receive a one-time activation token; existing active accounts do not. Re-import can
  update register details and issue a replacement token only for accounts with no password.
- Do not share a token through email or chat unless that channel is approved for credentials.
- The register is an eligibility claim. The software does not prove a person's real-world identity.

## Candidate decisions

- **Approve** a pending nomination only after checking eligibility and manifesto policy.
- **Reject** with a meaningful reason; the candidate can edit/resubmit where the workflow permits.
- **Disqualify** an approved candidate only before voting opens. Once voting opens, approved candidate
  details are frozen.
- Candidate role grants are recorded in the audit trail. A role change invalidates active role claims at
  the next security-stamp check.
- Approved candidates may also be eligible voters; they cannot vote on behalf of anyone else.

## Lifecycle controls

- Draft: editable; positions and voter imports are available.
- Scheduled: voting window configured; candidates can still be decided before opening.
- Open: ballots accepted only inside the trusted server window.
- Closed: no ballots accepted; request result review.
- Under review: provisional snapshot; observer verification and Super Administrator publication.
- Published: result visible; corrections use an authorised recount/new version.
- Suspended: explicitly reason-recorded; resume targets Scheduled, Open or Closed according to time.
- Cancelled: terminal; preserve evidence and audit records.

Early close, suspension, cancellation and recount reasons are audited. Do not use them to influence a
result after observing partial counts; define and follow institutional procedures in advance.

## Result review and publication

- The provisional result is calculated from accepted anonymous ballots.
- Verify totals, turnout, abstentions and ties; do not treat frontend totals as authoritative.
- An independent observer must verify before publication. The Super Administrator publication creates a
  version and marks any earlier published version as superseded.
- A recount requires a documented reason and creates a new provisional version; the previous published
  version remains preserved.
- CSV exports and PDF/print reports are aggregate-only. Handle exports as sensitive operational data.

## Operational security

- Use unique named accounts; never share Super Administrator credentials.
- Review audit events for unexpected role changes, imports, lifecycle actions and result activity.
- Export and protect database backups according to [deployment.md](deployment.md).
- Keep the host, Docker, PostgreSQL, .NET runtime and Node dependencies patched.
- Monitor `/health`, disk space, PostgreSQL availability and failed login patterns.
- Disable demo seeding (`Seed__Enabled=false`) outside development/lab use.
- Treat database and Data Protection key access as privileged insider access.
- An audit chain is tamper-evident, not tamper-proof against a privileged DB administrator.

## Procedures before a real election

Complete the institutional identity-verification process, candidate eligibility checks, legal review,
independent security test, hardware/network validation, disaster-recovery rehearsal, staff training and
a published disputes procedure. This software does not replace those controls.
