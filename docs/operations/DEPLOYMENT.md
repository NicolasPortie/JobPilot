# Deployment

The production Compose profile provides a managed single-tenant baseline. It is
not a public multi-tenant SaaS profile.

## Configure

Create a private environment file from `infra/.env.production.example` and
replace every credential.

For a new database, set `JOBPILOT_SEED_ON_STARTUP=true` in that private file
before the first startup. After the first successful login, set it back to
`false` and recreate the API container. Seeding is disabled by default.

## Start

```powershell
docker compose --env-file infra/.env.production -f infra/docker-compose.production.yml up -d --build
```

The profile applies migrations; the explicit first-boot setting creates the
bootstrap user. Existing users are not overwritten. Place a TLS reverse proxy in front of the
web service before exposing it outside a trusted network.

## Verify

```powershell
Invoke-RestMethod http://localhost:8080/health
Invoke-RestMethod http://localhost:8080/health/ready
```

## Backup

```powershell
$env:JOBPILOT_POSTGRES_PASSWORD = "senha-do-compose"
.\scripts\backup-postgres.ps1
.\scripts\backup-postgres.ps1 -Restore .\.build\backup\jobpilot-<data>.dump
```

The profile persists PostgreSQL, resume originals/exports (`/app/documents`),
and job source storage (`/app/storage`) in three named volumes. Back up all
three together; recreating the containers retains these volumes.
`docker compose down --volumes` deletes them and must not be used for an update.

`backup-postgres.ps1` connects to a host-accessible PostgreSQL server. The
production profile keeps PostgreSQL internal: use `pg_dump` inside the postgres
container for that profile, or an explicitly configured private database port.
The script alone does not back up document volumes.
Run a restore test before relying on a backup, and always back up before
applying a destructive migration (run it outside boot, with a backup and a
rollback plan).

## Runbook

- First boot: set `JOBPILOT_SEED_ON_STARTUP=true`, then disable it after the first login.
- Rollback: `dotnet ef database update <PreviousMigration> --project apps/api`
  with a backup taken first; verify with `/health/ready`.
- Key rotation: change `Auth__JwtKey` and `Security__SecretKey` (independent,
  32+ chars) and restart; sessions re-issue on next login (see `SECURITY.md`).
- TLS: place a reverse proxy in front of `web` before exposing it outside a
  trusted network; never expose the API directly.

## PDF export and OCR requirements

- `TXT`/`DOCX` export works everywhere. `PDF` export needs a Chromium provider
  on the API host: install Playwright browsers (`playwright install chromium`)
  or keep Google Chrome available. Without either, `PDF` export returns `400`
  with guidance and `TXT`/`DOCX` keep working.
- The production `apps/api/Dockerfile` does not bundle browsers. If you need
  `PDF` in the container, extend the image with the Playwright browser install
  or point the API at a host with Chrome.
- OCR is best-effort: without `tesseract`/`ocrmypdf` on the host, scanned PDFs
  are flagged `requiresOcr` instead of failing.
