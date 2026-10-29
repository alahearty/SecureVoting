# Phase 9 - Independent Audit and Quality Report

Date: 2026-10-29  
Project: IAUE SecureVote (SecureVoting)  
Workspace: `C:\SyncGrass Limited\Topics\SecureVoting`  
Audit type: independent technical review against a fixed source checkpoint, followed by remediation and re-verification.

## 1. Purpose and scope

This audit assessed whether the existing SecureVoting implementation is credible for an academic demonstration and controlled laboratory evaluation. It covered:

- authentication, authorization, CSRF, rate limiting and session handling;
- voter import, activation, eligibility and one-vote guarantees;
- ballot validation, trusted time, concurrency and privacy boundaries;
- result calculation, snapshot integrity, observer verification and publication;
- audit integrity, evidence handling and PII exposure;
- candidate photo upload and object-level authorization;
- CSV export safety and dependency/security posture;
- Docker runtime, demo-seeding safety and operational documentation;
- automated tests, load behaviour, backup/restore and evidence reproducibility.

Out of scope: physical election hardware, biometric or institutional identity systems, real student
data, legal certification, an external penetration test and a real departmental election.

### Constraints honoured

- The local `.env` was preserved and was never written, printed, committed or packaged.
- No valuable database was reset. All tests used disposable PostgreSQL containers, or isolated
  Compose projects with synthetic data.
- The source was not rebuilt from scratch. Existing architecture, UI and deployment model were kept.
- No credentials, tokens or seed passwords were added to source or documentation.
- The starting source was checkpointed before remediation.

## 2. Baseline

| Item | Result |
|---|---|
| Source checkpoint | `artifacts/phase9/baseline-source.zip` |
| Checkpoint entries | 178 |
| Checkpoint SHA-256 | `B7593519EC510E159E4D1EEB4FFDDD06E4A06F7070B1AF64C160548013767862` |
| Git repository | Not present |
| Local `.env` | Present before audit; preserved |
| Backend Release build | Passed, 0 warnings / 0 errors |
| Backend unit tests | 12 passed |
| Backend integration tests | 21 passed |
| Frontend typecheck/build | Passed |
| Frontend unit tests | 6 passed when run in isolation |
| Playwright E2E | 3 passed against isolated Docker Compose project on port 8091 |
| Documented automated checks | 42 |

The first frontend unit run was started while backend builds/tests were consuming resources and
terminated before the tests ran with a Vitest worker timeout. The same suite passed 6/6 when run in
isolation. This is recorded as test-environment contention, not as an application test failure.

## 3. Final verified state

| Item | Result |
|---|---|
| Final Release build | Passed, 0 warnings / 0 errors from a clean build |
| Backend unit tests | 27 passed, 0 failed, 0 skipped |
| Backend integration tests | 26 passed, 0 failed, 0 skipped |
| Frontend unit tests | 6 passed |
| Playwright E2E | 3 passed against a fresh final Docker stack |
| Total automated checks | 62 |
| Docker runtime | Non-root `app` user, UID 1654 |
| Data Protection and photo paths | Writable by the runtime user |
| NuGet vulnerable packages | 0 reported |
| npm production vulnerabilities | 0 reported |
| Backup/restore | Custom-format dump restored into a second database; ballots, audit entries, result and result lines matched |

Final evidence is in [phase-9-test-results.md](phase-9-test-results.md) and
[performance-test-report.md](performance-test-report.md).

## 4. What was fixed

| ID | Severity | Finding | Status |
|---|---|---|---|
| F-01 | High | Generic lifecycle `review` action could move a closed election to review without creating the result snapshot | Fixed; generic action now rejects; dedicated result endpoint is the only review path |
| F-02 | High | Rate limiting ran before authentication, so the ballot limiter's per-user partition saw an empty user and applied one shared `"anonymous"` bucket | Fixed; authentication now runs before rate limiting; verified with a reduced ballot limit and 12 distinct voters |
| F-03 | High | Ballot trusted time was read before the election row was locked, allowing a ballot to pass the window check while waiting for a concurrent state change | Fixed; time is captured after `FOR UPDATE` inside the transaction |
| F-04 | High | Activation token consumption was not a single conditional database operation, so concurrent activation could race | Fixed; token is consumed with `UPDATE ... WHERE UsedAtUtc IS NULL` and a one-row check; reuse test added |
| F-05 | High | Candidate photo upload wrote a file before proving the student owned the candidate, and a failed photo update could overwrite/delete the previous photo | Fixed; ownership check precedes save; unique generated filenames; IDOR and upload tests added |
| F-06 | Medium | Result integrity hash covered only part of the snapshot and was not verified when a snapshot was read | Fixed; full-field SHA-256 over lines, seats, abstentions, percentages, ranks, winners, ties and turnout; verification returns 409 on mismatch; unit and tamper tests added |
| F-07 | Medium | CSV formula injection could survive leading whitespace before a formula marker | Fixed with `CsvEscaper`; unit tests cover leading spaces, tabs, controls and ordinary values |
| F-08 | Medium | Demo seeding was blocked only in Production, leaving Staging and other non-development environments exposed to accidental demo data | Fixed; seeding is permitted only in Development or Testing, with a unit-tested policy |
| F-09 | Medium | Docker runtime image ran as root | Fixed; runtime uses `app:app`; verified in the running final image |
| F-10 | Low | Compose defaults selected Development and demo seeding | Fixed; defaults are Production and seed-disabled; `.env.example` is an explicit local demo profile |
| F-11 | Medium | Final-image load runs at 16-way concurrency showed intermittent activation transport failures and variable latency; a 12-way run was clean | Open residual performance/reliability issue; see the performance report |
| F-21 | Informational | Bulk substring edits during remediation duplicated classes in `ResultService.cs` and `AuditHash.cs` | Audit-process incident, not present in the baseline checkpoint. Evidence preserved; files reconstructed; clean build passes |

The final source diff also removed an unused, older partial result-hash method from `AuditHash.cs` so
there is only one result-integrity implementation.

## 5. Reproducible verification

### Backend

```powershell
dotnet clean backend\SecureVoting.slnx -c Release
dotnet build backend\SecureVoting.slnx -c Release --no-restore
dotnet test backend\tests\SecureVoting.UnitTests\SecureVoting.UnitTests.csproj -c Release --no-build
dotnet test backend\tests\SecureVoting.IntegrationTests\SecureVoting.IntegrationTests.csproj -c Release --no-build
```

### Frontend

```powershell
cd frontend
npm run typecheck
npm run test:unit
npm run build
```

### Final Docker/E2E check

```powershell
docker compose -p securevoting-final --env-file artifacts\phase9\final.env up -d --build
$env:SECUREVOTE_E2E_URL='http://localhost:8095'
cd frontend
npx playwright test --project=chromium
```

### Load, rate and backup evidence

The 300-voter harness is `backend/tests/SecureVoting.LoadTest`. The rate-partition check used a ballot
limit of 10 and a login limit of 1000; 12 distinct voters were all accepted, demonstrating that the
ballot limit is per authenticated voter rather than one global bucket.

Backup restoration was exercised on the isolated load stack: custom-format dump, restore into a second
database, verify ballot/audit/result counts, then drop the temporary database. No production data was
touched.

## 6. Readiness assessment

### Final classification

**Ready for academic demonstration.**

The application is suitable for a controlled laboratory demonstration with synthetic data after the
documented residual controls are applied. It is **not suitable for a real departmental election** yet.

Reasons for the qualification:

- There is no institutional identity proof, biometric check or independent electoral audit.
- The database administrator can correlate timing and can rewrite the database and audit chain; there
  is no external signed checkpoint.
- No cryptographic end-to-end verifiable voting or paper audit trail exists.
- TLS/reverse-proxy, key encryption, MFA/credential recovery, retention and incident response remain
  institutional deployment responsibilities.
- The final 16-way load runs were not consistently clean; demonstrate at 12-way concurrency or
  investigate the transport failures before claiming that capacity.
- No independent penetration test, real hardware test or real-election trial was performed.

### Conditions for controlled laboratory testing

1. Use synthetic data and an isolated database; keep demo seeding disabled outside Development/Testing.
2. Run behind an institution-controlled TLS proxy; enable `Security__UseForwardedHeaders` only when the
   proxy strips client-supplied forwarded headers, and restrict direct access to the API.
3. Cap demonstration load at 12 concurrent voters unless the activation transport failures at 16 are
   investigated and resolved.
4. Preserve and back up Data Protection keys and candidate photos; encrypt backups.
5. Treat the audit chain as tamper-evident, not tamper-proof, and export periodic checkpoints if the
   demonstration depends on stronger evidence.
6. Obtain supervisor/institutional approval and document a manual dispute procedure before involving
   real participants.

## 7. Limitations and explicitly unperformed checks

- Physical modem/SIM/Nigerian mobile network: not tested.
- Institutional identity provider, biometrics or voter registration proof: not integrated.
- Real student data or real election: not used.
- Independent external penetration test: not performed.
- Signed installer or clean institutional Windows profile: not performed.
- Multi-node/high-availability deployment, failover and disaster recovery: not tested.
- SMTP delivery and password recovery: not end-to-end tested.
- SMS OTP/MFA: not implemented.
- Cryptographic end-to-end verification, mixnets, zero-knowledge proofs, paper trail: not implemented.
- Legacy result snapshots created before the new full integrity hash: not migrated; they would fail
  verification and should not be carried into a Phase 9 database without a documented migration.
- GitHub Actions CI was reviewed as configuration but not executed from this local workspace.
- No real credentials, production database or local `.env` were used in any test.

## 8. Evidence index

| Evidence | Path |
|---|---|
| Baseline checkpoint | `artifacts/phase9/baseline-source.zip` |
| Corrupt-edit evidence | `artifacts/phase9/evidence/ResultService.corrupt.cs` |
| Corrupt-edit evidence | `artifacts/phase9/evidence/AuditHash.corrupt.cs` |
| Load output | `artifacts/phase9/load-run.out`, `load-run.err` |
| Load output | `artifacts/phase9/load2-run.out`, `load2-run.err` |
| Final load output | `artifacts/phase9/final-load-run*.out` |
| Resource snapshots | `artifacts/phase9/load-stats.txt`, `load2-stats.txt`, `final-load-stats.txt` |
| Backup dump | `artifacts/phase9/backup-check.dump` |
| Test results | `docs/phase-9-test-results.md` |
| Findings | `docs/security-findings.md` |
| Performance | `docs/performance-test-report.md` |
| Modified files | `docs/phase-9-modified-files.md` |

The final source archive is regenerated after documentation and excludes `.env`, build output,
dependencies, local databases and test volumes.
