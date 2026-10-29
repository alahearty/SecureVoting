# Phase 9 Security Findings

Date: 2026-10-29  
Scope: SecureVoting source, API, database design, authentication, ballot and result flows, deployment
configuration and test evidence.

## Severity scale

| Level | Meaning |
|---|---|
| High | Could bypass a core election control, corrupt result evidence, or expose ballot/identity material under plausible use |
| Medium | Significant defense-in-depth, privacy or reliability weakness; requires an additional condition or privileged position |
| Low | Hardening gap with limited direct impact in the current academic deployment |
| Informational | Process/documentation observation or a limitation that is inherent to the declared scope |

A finding marked **Fixed** was corrected and covered by a regression or reproducible check. A finding
marked **Residual** is documented, not silently treated as solved.

## Summary

| ID | Severity | Finding | Status |
|---|---|---|---|
| F-01 | High | Generic lifecycle `review` action bypassed result snapshot creation | Fixed |
| F-02 | High | Rate limiter ran before authentication; ballot limit was effectively global | Fixed |
| F-03 | High | Ballot trusted time read before election lock | Fixed |
| F-04 | High | Activation token single-use was not atomic | Fixed |
| F-05 | High | Candidate photo ownership/overwrite flaw | Fixed |
| F-06 | Medium | Incomplete result integrity hash and no read verification | Fixed |
| F-07 | Medium | CSV formula injection after leading whitespace | Fixed |
| F-08 | Medium | Demo seed allowed outside Development/Testing | Fixed |
| F-09 | Medium | Container ran as root | Fixed |
| F-10 | Low | Unsafe Compose defaults (Development + seeding) | Fixed |
| F-11 | Medium | Intermittent activation transport failures at 16-way load | Residual |
| F-12 | Medium | Ballot timing/privileged correlation remains possible | Residual |
| F-13 | Medium | Privileged DB administrator can rewrite audit chain | Residual |
| F-14 | Low | Database constraints do not independently prove candidate/position validity | Residual |
| F-15 | Low | Forwarded-header trust and host filtering require deployment configuration | Partially fixed |
| F-16 | Low | Data Protection keys are not encrypted by the application | Residual |
| F-17 | Low | Photo validation is magic-byte based, with no image decode/AV scan | Residual |
| F-18 | Low | No MFA/OTP; SMTP recovery is optional and not E2E tested | Residual |
| F-19 | Low | New result hash is incompatible with pre-Phase-9 snapshots | Residual |
| F-20 | Low | Base image tags are not digest-pinned; CI has no mandatory vulnerability gate | Residual |
| F-21 | Informational | Audit bulk edits temporarily duplicated source classes | Fixed process incident |
| F-22 | Informational | No Git repository/history exists | Residual process limitation |
| F-23 | Informational | Test-report counts were stale before Phase 9 | Fixed in final docs |
| F-24 | Informational | Frontend worker timeout under concurrent build/test load | Environment observation |
| F-25 | Informational | No real election, hardware, biometrics or external penetration test | Scope limitation |

## Fixed findings

### F-01 - Generic lifecycle review bypass (High)

- **Path:** `backend/src/SecureVoting.Infrastructure/Elections/ElectionAdminService.cs`,
  `backend/tests/SecureVoting.IntegrationTests/ElectionAndBallotTests.cs`.
- **Issue:** `POST /api/admin/elections/{id}/actions/review` accepted the generic `review` action and
  transitioned a closed election to `UnderReview` without invoking the result-summary routine. This
  could leave an election in review with no result snapshot.
- **Fix:** The generic lifecycle action now rejects `review`; the only review path is
  `POST /api/elections/{id}/results/review`, which creates the snapshot under an election row lock.
- **Regression:** `GenericLifecycleReviewActionCannotBypassResultSnapshot` passed.

### F-02 - Rate limiter preceded authentication (High)

- **Path:** `backend/src/SecureVoting.Api/Program.cs`.
- **Issue:** `UseRateLimiter()` was registered before `UseAuthentication()`. The ballot policy reads
  the authenticated user identifier; at that point the user was empty, so every voter shared the
  `"anonymous"` partition. With the normal 10-per-minute limit, the eleventh ballot across the whole
  election would be rejected.
- **Fix:** Authentication now runs before rate limiting. Login limiting still works by IP; ballot
  limiting now partitions by authenticated user.
- **Verification:** A fresh stack was configured with `RateLimit__BallotPermitLimit=10` and
  `RateLimit__LoginPermitLimit=1000`. Twelve distinct voters submitted; **12/12 were accepted** with no
  429. Before the ordering fix the same scenario would have exhausted one global bucket.
- **Residual:** Forwarded-header trust must be configured when a reverse proxy is used; otherwise the
  login limiter sees the proxy address, not the client.

### F-03 - Ballot trusted time captured before lock (High)

- **Path:** `backend/src/SecureVoting.Infrastructure/Ballots/BallotService.cs`.
- **Issue:** The trusted timestamp was read before the transaction acquired the election `FOR UPDATE`
  lock. A request could pass the time-window check, wait behind a close, and then insert a late ballot.
- **Fix:** The service begins the transaction, locks the election row, then reads `TimeProvider` and
  evaluates the window inside the locked transaction.
- **Verification:** Code review and the existing closed-election tests exercise the window. There is no
  deterministic fake-clock/lock regression test yet; this is an explicit test gap, not a claim of a
  failure under ordinary timing.
- **Residual:** PostgreSQL clock/NTP trust remains an environmental assumption.

### F-04 - Activation token race (High)

- **Path:** `backend/src/SecureVoting.Infrastructure/Students/ActivationService.cs`,
  `backend/tests/SecureVoting.IntegrationTests/VoterProvisioningTests.cs`.
- **Issue:** Token lookup and later marker assignment could race; two concurrent requests could both
  observe an unused token before either saved the used status.
- **Fix:** Inside the transaction, the token is consumed with a conditional update:
  `UPDATE ... WHERE Id = ... AND UsedAtUtc IS NULL`; exactly one row must be affected. Password setup
  and activation audit remain in the same transaction.
- **Regression:** `ActivationTokenCannotBeReused` and `ExpiredActivationTokenIsRejected` passed.
- **Residual:** The token is read for expiry before the transaction; expiry is evaluated again by the
  conditional flow only indirectly. The window is small and the token remains single-use.

### F-05 - Candidate photo ownership and overwrite (High)

- **Paths:** `CandidateController.cs`, `CandidateService.cs`, `CandidatePhotoStore.cs`,
  `CandidateWorkflowTests.cs`.
- **Issue:** The upload endpoint could read/save a file before proving the student owned the candidate.
  A failed update could overwrite or delete the previous photo because the filename was reused.
- **Fix:** Ownership is checked before file read/save; filenames are generated as
  `candidateId-guid.ext`; failed DB updates remove only the new file, leaving the previous photo.
- **Regression:** `StudentCannotUploadPhotoForAnotherCandidate` and the existing upload validation test
  passed.
- **Residual:** An authorized owner can still upload any valid-looking image; see F-17.

### F-06 - Result integrity hash incomplete (Medium)

- **Path:** `backend/src/SecureVoting.Application/Results/ResultIntegrity.cs`,
  `backend/src/SecureVoting.Infrastructure/Results/ResultService.cs`.
- **Issue:** The previous hash did not cover all published evidence (seats, abstentions, percentages,
  rank, winner/tie/turnout), and reads did not verify the hash.
- **Fix:** A full-field canonical SHA-256 hash is computed after result lines are built. Every snapshot
  mapping verifies the hash; a mismatch throws a 409 conflict. Publication audit details include the
  integrity hash.
- **Regression:** `ResultIntegrityTests` (determinism and every-field sensitivity) and the integration
  result-tamper test passed. The latter modifies a result line and expects HTTP 409.
- **Residual:** A database administrator can rewrite both data and hash; the hash is integrity
  evidence, not a signature. Pre-Phase-9 snapshots do not use the new hash semantics (F-19).

### F-07 - CSV formula injection (Medium)

- **Path:** `backend/src/SecureVoting.Application/Exporting/CsvEscaper.cs`,
  `ResultService.cs`, `CsvEscaperTests.cs`.
- **Issue:** A value beginning with whitespace followed by `=`, `+`, `-` or `@` could evade a simple
  first-character check when opened in a spreadsheet.
- **Fix:** Leading whitespace/control characters are inspected, dangerous values are prefixed with an
  apostrophe, and standard CSV quoting is applied. Unit tests cover leading spaces, tabs, controls,
  ordinary values and null.
- **Residual:** The export remains intended for trusted spreadsheet use; organizations should still
  treat exported files as untrusted input.

### F-08 - Demo seeding outside Development/Testing (Medium)

- **Path:** `backend/src/SecureVoting.Application/Configuration/SeedPolicy.cs`,
  `backend/src/SecureVoting.Api/Program.cs`, `SeedPolicyTests.cs`.
- **Issue:** The startup guard only rejected Production. A Staging or misnamed environment could seed
  synthetic accounts and a demo election.
- **Fix:** `SeedPolicy.EnsureAllowed` permits enabled seeding only in Development or Testing. Production,
  Staging and other names throw at startup. Six unit cases cover the policy.
- **Residual:** The operator can still intentionally set the environment to Development; deployment
  review must prevent that.

### F-09 - Root container runtime (Medium)

- **Path:** `Dockerfile`.
- **Issue:** The runtime container ran as root, increasing the impact of an application/file-write
  compromise.
- **Fix:** A dedicated `app` user/group owns `/app` and `/data`; the runtime stage uses `USER app`.
- **Verification:** `docker exec ... id` returned `uid=1654(app) gid=1654(app)`; keys and photo paths
  were writable. Final E2E passed on the hardened image.
- **Residual:** Docker daemon/host access remains privileged; volume permissions still require review.

### F-10 - Unsafe Compose defaults (Low)

- **Path:** `docker-compose.yml`, `.env.example`.
- **Issue:** The default Compose environment was Development with demo seeding, which is unsafe if
  reused outside a demonstration.
- **Fix:** Compose defaults are now `ASPNETCORE_ENVIRONMENT=Production` and
  `SEED_ENABLED=false`. The committed `.env.example` remains an explicit local demo profile and is
  documented as such. `POSTGRES_PASSWORD` remains mandatory.
- **Residual:** A copied demo `.env` is still a demo configuration; operators must not use it for a real
  election.

## Residual findings

### F-11 - Activation transport reliability at higher load (Medium)

- **Evidence:** Three 300-voter, 16-way runs against the final image produced 4, 12 and 12 activation
  transport failures respectively; accepted ballots were 296 and 288 on the runs with failures. A
  12-way final-image run accepted 300/300 with p95 submission 1,107 ms. One earlier 16-way run accepted
  300/300 with p95 1,216 ms.
- **Impact:** Demonstration capacity at 16 concurrent voters is not reliable in this Windows/Docker
  environment. No server 5xx or application exception was logged; the failures were client-observed
  transport errors during activation.
- **Status:** Open. Recommended demonstration limit: 12 concurrent voters. Investigate client connection
  reuse, Docker port-proxy behaviour and activation latency before claiming 16-way capacity.
- **See:** [performance-test-report.md](performance-test-report.md).

### F-12 - Ballot timing/privileged correlation (Medium)

The ballot has no student/user key, but `VoterEntitlement.ConsumedAtUtc` and `Ballot.SubmittedAtUtc`
are close in time. A privileged database administrator with transaction/WAL/backup access can correlate
them. The application does not pad timing or use a mixnet. This is an accepted academic limitation;
absolute anonymity is not claimed.

### F-13 - Audit chain rewrite by DB administrator (Medium)

The audit chain detects alteration but a privileged administrator can rewrite entries and
`AuditChainState`. There is no external signed checkpoint, write-once store or HSM. Export periodic
hashes to an independent system if stronger non-repudiation is required.

### F-14 - Application-level ballot validity constraints (Low)

The API validates candidate approval, position membership, seat/selection limits and election window.
The database enforces uniqueness and relationships, but not every business rule independently. A
privileged DB writer could insert an invalid selection without going through the API. Database roles,
change control and audit review remain required.

### F-15 - Proxy/header configuration (Low; partially fixed)

An opt-in `Security__UseForwardedHeaders` switch and forwarded-header processing were added for
reverse-proxy deployments. It deliberately clears default trusted proxies/networks when enabled, so it
must only be turned on when the API is unreachable except through a proxy that strips client-supplied
forwarded headers. `AllowedHosts` is still unrestricted; an institution should set host filtering at
the proxy/API boundary.

### F-16 - Data Protection key encryption (Low)

The container logs a warning that XML keys are not encrypted. Keys are persisted on a volume and must be
protected by OS permissions/encryption. Losing the keys invalidates authentication and antiforgery
cookies.

### F-17 - Image parsing/AV (Low)

Candidate photos are limited to JPEG/PNG/WebP magic bytes and 2 MB, stored outside the static root and
served with validated content types. The application does not decode/re-encode images or run antivirus.
A malformed-image or browser-decoder risk remains.

### F-18 - MFA, OTP and recovery (Low)

There is no app-level MFA/OTP. Password recovery is optional SMTP and was not end-to-end tested. A real
deployment needs an approved identity/recovery process.

### F-19 - Result hash compatibility (Low)

Snapshots created before Phase 9 use the old partial hash format and will fail the new full-field
verification. A Phase 9 database should start from a fresh/empty result history or run a documented
migration. No silent migration is performed.

### F-20 - Supply chain (Low)

NuGet and npm audits reported no known vulnerabilities at audit time. Docker base tags
(`dotnet/aspnet`, `postgres:17-alpine`, Node build image) are not pinned by digest, and the CI workflow
does not enforce a vulnerability gate. Pin digests and add dependency/scan gates for production.

### F-21 - Audit-process source corruption (Informational)

During bulk text replacement, duplicate class definitions were introduced in `ResultService.cs` and
`AuditHash.cs`. A clean build exposed the problem. The baseline checkpoint contained a single class in
each file. Corrupt copies were preserved as evidence and the source was reconstructed. Final clean build
and all suites pass. No source corruption remains; this was an audit-tooling incident, not a baseline
product defect.

### F-22 - No Git repository (Informational)

There is no Git history, branch or signed commit trail. `artifacts/phase9/baseline-source.zip` is the
immutable comparison point. Establish version control and signed releases before institutional use.

### F-23 - Documentation drift (Informational)

Baseline docs reported 42 checks and, later, the old 12/21/6/3 counts. Final docs now report the
Phase 9 counts and link to the new evidence. Test counts should be generated from CI, not maintained by
hand.

### F-24 - Frontend worker timeout (Informational)

Vitest failed to start a worker while the backend build/test workload was running. The same suite passed
in isolation. CI should run frontend tests separately from integration tests or increase the worker
startup allowance.

### F-25 - Real-election limitation (Informational)

No physical election, biometric identity, modem/SIM/SMS, independent penetration test, legal review or
real student data was tested. The software must not be represented as certified or ready for a real
departmental election.

## Privacy and data protection notes

| Data | Purpose | Protection/limitation |
|---|---|---|
| Student name, matriculation number, email, department/level | Eligibility and activation | Access-controlled admin APIs; logs avoid matriculation numbers; test data synthetic |
| Activation and reset tokens | One-time enrollment/recovery | Stored hashed (activation) or Identity-protected; never logged; expiry and single-use implemented |
| Eligibility/entitlement | One-vote enforcement | Linked to student; separate from ballot |
| Ballot/selections | Vote capture | No student/user/IP/user-agent columns; confirmation reference hashed on entitlement |
| Audit log | Accountability | Hash-chained; no ballot choices; actor display controlled; privileged admin can rewrite |
| Candidate photos | Candidate presentation | Size/type validation; private volume; authenticated retrieval |
| Results and exports | Publication/reconciliation | Aggregate-only public views; result integrity hash; operator exports still contain sensitive aggregate data |
| Backups and Data Protection keys | Recovery | Institutional encryption/access controls required; app has no backup download endpoint |

The application does not claim anonymity against a privileged database/server administrator or against
client-side coercion/device compromise. Retention periods and lawful bases are institutional
responsibilities.

## Verification commands

```powershell
dotnet build backend\SecureVoting.slnx -c Release --no-restore
dotnet test backend\tests\SecureVoting.UnitTests\SecureVoting.UnitTests.csproj -c Release --no-build
dotnet test backend\tests\SecureVoting.IntegrationTests\SecureVoting.IntegrationTests.csproj -c Release --no-build
dotnet list backend\SecureVoting.slnx package --vulnerable --include-transitive

cd frontend
npm run typecheck
npm run test:unit
npm run build
npm audit --omit=dev
```

## Reporting

This is an academic project. Report suspected vulnerabilities privately to the project owner. Do not test
against real student data or a live election.

## Phase 10 addendum - departmental scope

The university-wide department work adds these residual findings:

- **Scope assignment is administrative (Medium):** an incorrectly assigned officer/observer can access
  the wrong department. Assignments are SuperAdministrator-only, audited and invalidate role claims,
  but are not reconciled automatically with institutional HR records.
- **Legacy department codes (Low):** migration creates explicit departments from legacy strings and
  generated `LEGACY-...` codes. Historical rows are preserved, but institutions must review codes and
  assignments before production use.
- **Audit visibility metadata (Low/Medium):** `AuditLogs.DepartmentId` scopes observer list visibility
  but is not part of the existing hash. A privileged database administrator can alter visibility
  metadata without invalidating old event hashes. The event content covered by the hash remains
  tamper-evident.
- **No external identity provider (Medium):** department membership and eligibility still derive from
  imported records and activation tokens, not an institutional identity service.
- **Faculty is a string (Informational):** no faculty entity is modelled; this is deliberate scope
  control, not a full university hierarchy.

No high-severity cross-department bypass was found after remediation. Seven positive/negative
integration tests cover the enforced paths; legacy data and assignment operations remain institutional
responsibilities.