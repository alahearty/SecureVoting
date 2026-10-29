# Phase 9 Modified Files and Rationale

Date: 2026-10-29  
Comparison point: `artifacts/phase9/baseline-source.zip`  
No Git repository exists in the workspace; this file is the audit trail for source changes.

## Source changes

| File | Change | Reason |
|---|---|---|
| `backend/src/SecureVoting.Api/Program.cs` | Moved rate limiting after authentication; added opt-in forwarded-header processing; centralized seed-environment guard; retained security middleware order | Prevent global ballot throttling, support controlled reverse-proxy deployment, prevent demo seeding outside Development/Testing |
| `backend/src/SecureVoting.Api/Controllers/CandidateController.cs` | Verify candidate ownership before photo read/save | Prevent student photo IDOR and writes before authorization |
| `backend/src/SecureVoting.Application/Configuration/SeedPolicy.cs` | New seed policy | Unit-testable Development/Testing-only demo-seeding rule |
| `backend/src/SecureVoting.Application/Exporting/CsvEscaper.cs` | New CSV escaping utility | Prevent formula injection after leading whitespace/control characters |
| `backend/src/SecureVoting.Application/Results/ResultIntegrity.cs` | New full-result snapshot hash and verifier | Make result evidence cover all published fields and detect tampering on read |
| `backend/src/SecureVoting.Application/Security/AuditHash.cs` | Reconstructed after audit bulk-edit incident; removed unused old partial result-hash method | Keep one integrity implementation and restore clean source |
| `backend/src/SecureVoting.Infrastructure/Ballots/BallotService.cs` | Capture trusted time after election row lock | Close the close/time race |
| `backend/src/SecureVoting.Infrastructure/Candidates/CandidateService.cs` | Unique photo handling and stricter ownership/status flow | Prevent failed updates from destroying an existing photo |
| `backend/src/SecureVoting.Infrastructure/Elections/ElectionAdminService.cs` | Reject generic `review` lifecycle action | Force result review through the snapshot-creating endpoint |
| `backend/src/SecureVoting.Infrastructure/Results/ResultService.cs` | Reconstructed clean source; compute full integrity hash; verify on map; audit hash; CSV escaper | Restore source and protect result snapshots |
| `backend/src/SecureVoting.Infrastructure/Storage/CandidatePhotoStore.cs` | Generate `candidateId-guid.ext` filenames; safe replace/delete semantics | Prevent overwrite/IDOR side effects and path collision |
| `backend/src/SecureVoting.Infrastructure/Students/ActivationService.cs` | Conditional atomic token consumption; remove matriculation number from logs | Prevent activation race and reduce PII logging |
| `Dockerfile` | Run runtime stage as non-root `app:app`; own `/app` and `/data` | Container hardening |
| `docker-compose.yml` | Default Production and seed-disabled; add rate-limit environment variables | Safe defaults; make limits configurable |

## Test changes and additions

| File | Change |
|---|---|
| `backend/tests/SecureVoting.UnitTests/CsvEscaperTests.cs` | New CSV escaping regressions |
| `backend/tests/SecureVoting.UnitTests/ResultIntegrityTests.cs` | New full-field integrity tests |
| `backend/tests/SecureVoting.UnitTests/SeedPolicyTests.cs` | New seed environment policy tests |
| `backend/tests/SecureVoting.IntegrationTests/ElectionAndBallotTests.cs` | Generic review bypass and result tamper assertions |
| `backend/tests/SecureVoting.IntegrationTests/VoterProvisioningTests.cs` | Activation reuse and expired-token tests |
| `backend/tests/SecureVoting.IntegrationTests/CandidateWorkflowTests.cs` | Candidate photo ownership test |
| `backend/tests/SecureVoting.IntegrationTests/AuditTests.cs` | Deleted-record audit-chain test |
| `backend/tests/SecureVoting.LoadTest/SecureVoting.LoadTest.csproj` | New 300-voter load harness project |
| `backend/tests/SecureVoting.LoadTest/Program.cs` | Import/activate/submit/result verification/rate-limit load harness |

The load-test project is intentionally outside the main solution file; it is run explicitly and is not
part of the normal unit/integration suite.

## Documentation changes

| File | Change |
|---|---|
| `docs/phase-9-audit-report.md` | New audit report, baseline, remediation, readiness decision |
| `docs/security-findings.md` | New detailed findings and residual-risk record |
| `docs/phase-9-test-results.md` | New final test evidence |
| `docs/performance-test-report.md` | New load/performance evidence |
| `docs/phase-9-modified-files.md` | This file |
| `README.md` | Current counts, audit links, safe Compose explanation |
| `SECURITY.md` | Phase 9 controls, load/rate residual, deployment assumptions |
| `TESTING.md` | Final suites, clean build, rate/load commands, unperformed checks |
| `CHANGELOG.md` | Phase 9 fixes and residual issues |
| `IMPLEMENTATION_PLAN.md` | Phase 9 verification record |
| `docs/test-report.md` | Final counts and pointer to Phase 9 evidence |
| `docs/deployment.md` | Safe Compose defaults and forwarded-header guidance |
| `docs/threat-model.md` | Controls/residuals updated for Phase 9 |
| `docs/architecture.md` | Ballot lock/time sequence and result-integrity notes |

## Preserved / not changed

- Root `.env`: local configuration preserved; never read into reports, committed or archived.
- `artifacts/phase9/baseline-source.zip`: immutable baseline checkpoint.
- `artifacts/phase9/evidence/*.corrupt.cs`: corrupt-edit evidence retained for audit transparency.
- Load/E2E evidence outputs: retained under `artifacts/phase9/`.
- No production database, real student data or real credentials were used.
- No files were deleted from the application source.

## Final source archive

The final archive is regenerated after documentation is complete. It excludes `.env`, `.git` (none),
`bin`, `obj`, `node_modules`, `dist`, local databases, Playwright output and `artifacts/phase9` test
volumes. The SHA-256 will be recorded in the final response and can be verified with `Get-FileHash`.
