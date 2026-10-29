# Deployment, Backup and Recovery

## Deployment options

| Option | Use |
|---|---|
| Installed PostgreSQL + run.cmd | Recommended student/local demonstration; PostgreSQL + API + Vite SPA; no Docker |
| Docker Compose | Optional reproducible deployment; PostgreSQL + API + built SPA |
| Framework-dependent publish | Managed Windows/Linux host with .NET 10 ASP.NET Core Runtime |
| Reverse proxy + TLS | Institutional deployment; terminate HTTPS and forward to the API |

The Compose stack serves the Vue production build from the API container. The API is the same origin as
the SPA, avoiding cross-site cookie/CORS complexity.

## Installed PostgreSQL (local/student system)

Use this path when Docker is not installed. PostgreSQL 17+ must be running locally.

```powershell
.\database\setup-local.ps1
.\run.cmd
```

The setup creates the non-superuser `securevote_app` role, `securevote` and `securevote_test`
databases, and ignored `.env.local` connection settings. The launcher loads `.env` and `.env.local` and
starts the API and Vite SPA; migrations and demo seeding run at API startup. See
[local-postgresql.md](local-postgresql.md).

## Docker Compose (optional)

Requirements: Docker Engine and Compose plugin.

```powershell
Copy-Item .env.example .env
# Set strong local-only values for POSTGRES_PASSWORD and SEED_DEMO_PASSWORD.
docker compose up -d --build
docker compose ps
Invoke-WebRequest http://localhost:8080/health
```

Open <http://localhost:8080>. The API health endpoint is `/health`; OpenAPI JSON is
`/openapi/v1.json`.

If port 8080 is occupied, set `API_PORT` in `.env` (for example `API_PORT=8090`) and use
`http://localhost:8090`. `FRONTEND_BASE_URL` is used in password-reset emails and must match the
external address.

Compose defaults are `ASPNETCORE_ENVIRONMENT=Production`, `SEED_ENABLED=false` and HTTP for local use.
The committed `.env.example` is an explicit Development/demo profile: copying it opts a local
demonstration into demo seeding. For production, omit demo seeding, supply a strong database password
through a secret store and terminate TLS at a trusted proxy. Demo seeding is rejected by the
application outside Development/Testing.

## Department configuration

Before creating elections, sign in as the Super Administrator and configure departments on the
**Departments** page. Assign each Electoral Officer and Observer only to the department(s) they are
authorised to work in. Super Administrators retain university-wide oversight.

After upgrading an existing database, review departments created by the migration. Legacy values are
preserved and receive generated codes such as `LEGACY-80C18801`; give them the institution-approved
codes and assignments before creating new elections. Deactivation blocks new elections/imports but does
not delete historical elections.

## Environment variables

| Variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | `Development`, `Staging` or `Production` |
| `Seed__Enabled` | Enable synthetic demo data; **must be false in production** |
| `Seed__DemoPassword` | Development-only demo account password, at least 12 chars |
| `App__FrontendBaseUrl` | External SPA URL used in reset links |
| `Email__Enabled` / SMTP variables | Optional password-recovery email |
| `DataProtection__KeysPath` | Persistent Data Protection key directory |
| `Storage__CandidatePhotosPath` | Candidate photo volume path |
| `Security__UseHttpsRedirection` | Enable only when the host handles HTTPS itself |
| `Security__UseForwardedHeaders` | Opt-in proxy trust; enable only behind a header-stripping trusted proxy |
| `RateLimit__LoginPermitLimit` / `RateLimit__BallotPermitLimit` | Per-minute limits; ballot limit is per authenticated voter |
| `ASPNETCORE_ENVIRONMENT` | Compose default is Production; use Development/Testing only for local demonstrations |
| `API_PORT` | Compose host port |

Never commit real values. Use the host secret manager, environment injection or a protected `.env`
outside source control.

## Production checklist

- [ ] Set `ASPNETCORE_ENVIRONMENT=Production` and `Seed__Enabled=false`.
- [ ] Run the container as the non-root `app` user and restrict `/data`/database volume permissions.
- [ ] Provision real staff accounts and student records through an approved operational process. The
      demo seeder is blocked outside Development/Testing.
- [ ] Terminate HTTPS at a trusted reverse proxy; set secure-cookie policy by running Production
      environment and configure forwarded headers/HSTS in the proxy.
- [ ] Use a dedicated least-privilege PostgreSQL role for the application and a separate backup role.
- [ ] Persist Data Protection keys and the candidate photo volume.
- [ ] Restrict database, backup, key and photo access with OS/volume permissions.
- [ ] Configure SMTP for password recovery, or document that recovery is unavailable.
- [ ] Set monitoring for `/health`, disk space and database availability.
- [ ] Do not expose PostgreSQL or the API container's internal port publicly.
- [ ] Apply institutional incident response, access review, backup retention and election procedures.
- [ ] Run an independent penetration test and legal/electoral review before real use.

## TLS reverse proxy example

A reverse proxy should forward to `http://api:8080`, set `Host`, `X-Forwarded-Proto: https` and
`X-Forwarded-For`, and enforce HSTS. Only trust forwarded headers from the proxy. If the proxy
terminates TLS, keep `Security__UseHttpsRedirection` false unless the API can itself issue HTTPS
redirects; otherwise configure the proxy redirect.

Set `Security__UseForwardedHeaders=true` only when the API is unreachable except through a proxy that
strips client-supplied `Forwarded`/`X-Forwarded-*` headers. The opt-in implementation trusts the
forwarded values; enabling it on a directly reachable API lets clients spoof rate-limit IP addresses.
Also set explicit host filtering at the proxy; the application currently leaves `AllowedHosts` open.

## Database backup

The API has no database download endpoint. Use PostgreSQL backup tooling under institutional controls:

```powershell
# Stream a custom-format backup from the Compose database
docker compose exec -T db pg_dump -U securevote -d securevote -Fc -f /tmp/securevote.dump
docker cp securevoting-db-1:/tmp/securevote.dump .\securevote.dump
Get-FileHash .\securevote.dump -Algorithm SHA256
```

The exact container name can be checked with `docker compose ps`. For a non-Compose deployment:

```bash
pg_dump "$DATABASE_URL" -Fc -f securevote-$(date -u +%Y%m%dT%H%M%SZ).dump
```

Encrypt and copy the dump to a protected location separate from the host. Test restoration regularly.
Backups contain student identity, eligibility, audit and aggregate election evidence.

## Restoration

**Restoration is destructive to the current database.** Stop the API, preserve the current database,
restore into a fresh database and validate before allowing election traffic.

```powershell
docker compose stop api
docker compose exec -T db createdb -U securevote securevote_restore
docker cp .\securevote.dump securevoting-db-1:/tmp/securevote.dump
docker compose exec -T db pg_restore -U securevote -d securevote_restore --clean --if-exists /tmp/securevote.dump

# Validate the restored database before switching it into service.
@'
select count(*) from "AspNetUsers";
select count(*) from "Elections";
select count(*) from "Ballots";
'@ | docker compose exec -T db psql -U securevote -d securevote_restore
```

To make the restored database the live one, either restore it under the configured database name
during a maintenance window or change the application connection string after backup and health
validation. Do not delete the current database until the restored copy has been verified and the
institution has approved the recovery. Run the application's database health check and verify that the
audit chain is internally consistent before reopening the election.

The audit chain detects partial alteration, but a privileged database administrator can rewrite the
whole chain and chain state. Export periodic hash checkpoints to a separate protected system if a
stronger evidence boundary is required.

## Upgrades

1. Stop the API or enter a maintenance window.
2. Take a verified `pg_dump` backup and export it outside the host volume.
3. Build the new image; review EF Core migrations in the diff.
4. Start the stack; migrations run at startup. If startup fails, preserve logs and do not repeatedly
   restart a failing migration.
5. Validate `/health`, roles, data counts, result visibility and audit chain.
6. Keep the previous image and dump until acceptance.

## Logs and diagnostics

- Docker: `docker compose logs -f api`.
- File/console logs use structured ASP.NET Core logging; request bodies and ballot choices are not
  logged.
- PostgreSQL logs are separate; do not enable statement logging with sensitive parameter values
  without a retention/access review.
- Correlation IDs appear in ProblemDetails responses and audit entries where supplied.
- Audit chain verification is available to authorized Super Administrators and Observers in the UI or
  `POST /api/audit/verify`.
- Candidate photos are stored in the volume configured by `Storage__CandidatePhotosPath`; only
  validated JPEG/PNG/WebP are accepted, with generated names.

## Recovery scenarios

| Scenario | Action |
|---|---|
| API cannot connect to DB | Check DB health, credentials, network, PostgreSQL logs; do not delete DB |
| Migration failure | Preserve logs/database; restore pre-upgrade dump; test migration on a copy |
| Corrupt database | Stop API; restore verified dump into a fresh DB; validate before traffic |
| Audit chain mismatch | Treat as a security incident; preserve database/WAL/backups; do not overwrite evidence |
| Lost Data Protection keys | Existing auth/antiforgery cookies become invalid; users reauthenticate; restore keys or plan rotation |
| Photo volume lost | Results/audits remain; candidate photos unavailable; restore from protected volume backup |
| Host compromise | Isolate host; preserve evidence; rotate credentials/keys; rebuild from trusted image |

See [../TESTING.md](../TESTING.md) and [test-report.md](test-report.md) for verification evidence.
