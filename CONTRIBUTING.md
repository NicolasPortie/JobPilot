# Contributing

## Quality Gate

Run the complete local quality gate before opening a pull request:

```powershell
.\scripts\quality.ps1
```

The quality gate stops on the first failing command and restores the environment
variables it temporarily sets. When editing the gate, also run
`.\scripts\tests\quality.Tests.ps1` to verify command failures and cleanup.

## Local Development

JobPilot local needs **three services**, not only Docker:

| Servico | Porta | Como subir |
|---------|-------|------------|
| PostgreSQL | `5434` | `docker compose -f infra\docker-compose.yml up -d` (exige `JOBPILOT_POSTGRES_PASSWORD` — use `.\scripts\start-mvp.ps1` para configurar automaticamente) |
| API | `5000` | `dotnet run --project apps\api` |
| Web | `5173` | `cd apps\web && npm ci && npm run dev` |

Atalho para subir tudo:

```powershell
.\scripts\start-mvp.ps1
```

Para diagnosticar o que falta:

```powershell
.\scripts\status.ps1
```

O atalho local gera segredos e credenciais de desenvolvimento em
`.jobpilot-dev-secrets.json` (arquivo ignorado pelo Git). Consulte a saída do
script para os dados da sessão atual; nunca documente credenciais fixas aqui.

Keep commits focused and describe the behavior changed. Do not commit generated
build output, local documents, credentials, tokens, screenshots, or logs.

## Project Boundaries

- `apps/api`: HTTP boundary, persistence, and backend business services.
- `apps/web`: user interface organized by feature.
- `docs`: current product and engineering documentation.

New behavior should include focused tests at the closest useful boundary.
