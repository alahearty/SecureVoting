# Phase 9 Performance and Load Test Report

Date: 2026-10-29  
Target context: a departmental election with approximately 300 eligible voters.  
Target: ballot submission p95 under 2 seconds for the demonstration environment, with no lost or
duplicated ballots.

These results describe a single Windows/Docker workload. They are not a capacity certification and do
not represent a campus network, multiple API replicas or a production PostgreSQL server.

## Environment

| Item | Value |
|---|---|
| Host | Windows 11 Pro, Docker Desktop 29.2.1, Linux containers |
| API | ASP.NET Core 10, Release container |
| Database | PostgreSQL 17.11 (`postgres:17-alpine`) |
| Docker VM allocation | 7.603 GiB reported by `docker stats` |
| Harness | `backend/tests/SecureVoting.LoadTest` |
| Data | Synthetic 300-voter register, 3 positions, 6 candidate choices |
| Rate limits during load | Login 1000/min; ballot 1000/min (to isolate application capacity) |
| Normal ballot limit | 10/min per voter; separately tested at reduced limit |
| Metrics | Wall-clock HTTP latency measured by the harness; Docker CPU/memory snapshots |

The harness imports 300 voters, activates them concurrently, opens the election, submits one ballot per
voter, attempts a duplicate submission, closes the election, requests result review, and checks the
aggregate count and candidate-vote total.

## Results

### Run A - 32-way concurrency, early image

| Metric | Value |
|---|---:|
| Voters requested | 300 |
| Activated | 299 |
| Accepted ballots | 299 |
| Activation failures | 1 transport error |
| Submission failures | 0 |
| Duplicate rejected | Yes |
| Result ballots | 299 |
| Candidate-vote sum | 897 (299 x 3) |
| Turnout | 96.14% |
| Submission p50 | 1,848.9 ms |
| Submission p95 | 2,479.6 ms |
| Submission p99 | 2,620.6 ms |
| Activation p50 | 6,285 ms |
| Activation p95 | 9,872.9 ms |
| API CPU peak observed | approximately 406% |
| API memory observed | approximately 207.5 MiB |
| Database CPU observed | approximately 20-52% |

The p95 target was not met at 32-way concurrency. The one activation failure was a client transport
error; no application 5xx was recorded.

### Run B - 16-way concurrency, early image

| Metric | Value |
|---|---:|
| Voters requested | 300 |
| Activated | 300 |
| Accepted ballots | 300 |
| Activation failures | 0 |
| Submission failures | 0 |
| Duplicate rejected | Yes |
| Result ballots | 300 |
| Candidate-vote sum | 900 (300 x 3) |
| Turnout | 96.46% |
| Submission p50 | 763.9 ms |
| Submission p95 | 1,215.6 ms |
| Submission p99 | 1,308.8 ms |
| Activation p50 | 3,686.9 ms |
| Activation p95 | 6,046.3 ms |
| API CPU peak observed | approximately 372% |
| API memory observed | approximately 194.2 MiB |
| Database CPU observed | approximately up to 52% |

This run met the 2-second submission p95 target and completed all 300 ballots with no errors.

### Final-image runs

The final image included the Phase 9 security changes and non-root runtime. The same high rate limits
were used.

| Run | Concurrency | Activated | Accepted | Activation failures | Submission p50 | Submission p95 | Submission p99 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Final 1 | 16 | 296 | 296 | 4 transport | 654.3 ms | 1,284.3 ms | 1,710.3 ms |
| Final 2 | 16 | 288 | 288 | 12 transport | 560.7 ms | 1,031.1 ms | 1,131.6 ms |
| Final 3 | 16 | 288 | 288 | 12 transport | 1,093.4 ms | 2,129.5 ms | 3,008.2 ms |
| Final 4 | 12 | 300 | 300 | 0 | 292.9 ms | 1,107.2 ms | 1,315.7 ms |

For every completed run, the result count matched the number of accepted ballots, the candidate sum was
exactly three times the ballot count, and the duplicate submission returned 409. No application 5xx was
observed.

The activation transport failures are not accompanied by application exceptions or 5xx entries. They
occurred while the harness held hundreds of per-voter HTTP clients/connections. The 12-way final run was
clean. This is recorded as an unresolved reliability question rather than a hidden pass.

## Rate-limit partition check

The corrected middleware order was verified separately with:

- 12 synthetic voters;
- ballot limit **10 per minute**;
- login limit 1000 per minute;
- 12-way concurrency.

Observed result: **12/12 activated and accepted**, duplicate rejected, result total 12, candidate sum
36, no HTTP 429. This demonstrates per-authenticated-user ballot partitioning. With the original
middleware order, all requests would have shared the `"anonymous"` ballot partition and the run would
have been limited to the global limit.

## Interpretation

- **300 ballots are functionally processed correctly** when all activations succeed.
- **12-way concurrency is the clean demonstrated operating point** on this host: 300/300, p95
  1,107 ms.
- **16-way concurrency is borderline/intermittent** on this host: one early run was clean, but three
  final runs had activation transport failures. The slower final run also exceeded the 2-second p95
  target.
- **32-way concurrency is beyond the target** in this environment: p95 2,480 ms and one activation
  failure.
- The API CPU figures exceed 100% because `docker stats` reports aggregate multi-core utilisation; the
  host and VM were shared with Docker/DB processes.
- The harness intentionally opens one client per voter. Activation latency (about 2-6 seconds p95)
  dominates the run; ballot submission itself was substantially faster.
- No claim is made about WAN latency, mobile devices, email delivery, reverse-proxy overhead, multiple
  API instances or high availability.

## Recommended operating conditions

1. Demonstrate at 12 concurrent voters unless the 16-way activation transport issue is investigated.
2. Keep load-test rate limits separate from production limits; do not use 1000/min as a production
   setting.
3. Add reverse-proxy and client-network latency testing before an institutional pilot.
4. Profile activation (Identity password hashing, token update, audit append) before increasing
   concurrency.
5. Add server-side request timing, database wait/lock metrics and client connection-pool diagnostics.
6. Repeat the test on the target institutional host rather than extrapolating from this Windows Docker
   environment.
7. Treat the 300-voter result as a synthetic demonstration result, not an election capacity guarantee.

## Evidence

| Artifact | Path |
|---|---|
| Early 32-way output | `artifacts/phase9/load-run.out`, `load-run.err` |
| Early 16-way output | `artifacts/phase9/load2-run.out`, `load2-run.err` |
| Early resource snapshots | `artifacts/phase9/load-stats.txt`, `load2-stats.txt` |
| Final-image outputs | `artifacts/phase9/final-load-run*.out` |
| Final resource snapshots | `artifacts/phase9/final-load-stats.txt` |
| Rate-partition run | `artifacts/phase9/rate.env` and the reduced-limit load invocation |
| Load harness | `backend/tests/SecureVoting.LoadTest/Program.cs` |

Commands (representative):

```powershell
$env:SECUREVOTE_LOAD_URL='http://localhost:8096'
$env:SECUREVOTE_LOAD_VOTERS='300'
$env:SECUREVOTE_LOAD_CONCURRENCY='12'
dotnet run --project backend/tests/SecureVoting.LoadTest/SecureVoting.LoadTest.csproj -c Release --no-build
```

The `.env` files under `artifacts/phase9` are synthetic local test inputs and are not part of the source
archive.
