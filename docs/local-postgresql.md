# Local PostgreSQL Setup (No Docker)

SecureVote can run entirely against an installed PostgreSQL service. Docker is optional and is used
only by the Compose deployment and the Docker/Testcontainers fallback.

Verified on this machine: **PostgreSQL 18** running as the Windows service `postgresql-x64-18` on
`localhost:5432`.

## One-time database setup

From the repository root:

```powershell
.\database\setup-local.ps1
```

The script:

- locates `psql.exe` even when PostgreSQL is not on `PATH`;
- reads `POSTGRES_PASSWORD` from `.env` as the local `postgres` superuser password, or prompts for it;
- creates the non-superuser role `securevote_app`;
- creates the `securevote` application database and the `securevote_test` integration-test database;
- makes `securevote_app` the owner and grants the required public-schema access;
- writes `.env.local`, which is ignored by source control, with:
  - `ConnectionStrings__Default` for `securevote`;
  - `SECUREVOTING_TEST_CONNECTION` for `securevote_test`;
  - local demo seeding values.

The setup does not modify `.env` and does not print the superuser password. The local application
password is the documented demonstration value in `.env.example`; it is not a production credential.

## Run the application

```powershell
.\run.cmd
```

or:

```powershell
.\run-local.ps1
```

The launcher:

1. loads `.env` and then `.env.local`;
2. checks .NET, Node.js and frontend dependencies;
3. starts the API at <http://localhost:5000>;
4. starts Vite at <http://localhost:5173>;
5. opens the SPA.

The API applies EF Core migrations and demo seeding at startup. It uses the installed PostgreSQL
service; no container is started.

## Tests without Docker

```powershell
.\test-local.ps1
```

This loads `.env.local`, sets `SECUREVOTING_TEST_CONNECTION` to `securevote_test`, and runs the backend
unit and integration suites. The fixture refuses any external database whose name does not end in
`_test`, so it cannot accidentally reset the development database.

Verified local result: **27 unit + 33 integration tests passed** with no Docker containers.

## Playwright without Docker

Start the local stack with `.\run.cmd`, then:

```powershell
cd frontend
$env:SECUREVOTE_E2E_URL='http://localhost:5173'
npx playwright install chromium
npx playwright test --project=chromium
```

Verified local result: **3 Playwright tests passed** against the installed-PostgreSQL API and Vite SPA.

## Docker remains optional

The existing `docker-compose.yml`, Dockerfile and Testcontainers fallback are retained for deployment
and CI. If `SECUREVOTING_TEST_CONNECTION` is not set, integration tests use Testcontainers when Docker
is available. The student/local path does not require Docker.

## Troubleshooting

- **psql not on PATH:** run `database/setup-local.ps1`; it searches
  `C:\Program Files\PostgreSQL\<version>\bin\psql.exe`.
- **Superuser password:** set `POSTGRES_PASSWORD` in `.env` or let the setup script prompt.
- **Port 5432 occupied:** ensure the Windows PostgreSQL service is the intended database; update
  the connection strings in `.env.local` if a different local port is used.
- **Demo seeding disabled:** ensure `.env.local` contains `Seed__Enabled=true` and the local demo
  password. Never use this configuration for a real election.
- **Frontend proxy:** Vite proxies `/api` to `http://localhost:5000`; do not change the API port
  without updating `frontend/vite.config.ts`.
