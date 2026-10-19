# Local Development

## Prerequisites

- .NET 10 SDK
- Node.js 24
- Docker with Compose
- Ollama local (opcional, para análise com IA — veja seção Ollama abaixo)

## Start

O atalho recomendado gera segredos dev, configura `JOBPILOT_POSTGRES_PASSWORD` e
aguarda os health-checks:

```powershell
.\scripts\start-mvp.ps1
```

Para executar manualmente (exporte a senha do Postgres antes):

```powershell
$env:JOBPILOT_POSTGRES_PASSWORD = "uma-senha-local-forte"
docker compose -f infra\docker-compose.yml up -d
dotnet run --project apps\api
```

In another terminal:

```powershell
cd apps\web
npm ci
npm run dev
```

Web em `http://localhost:5173`, API em `http://localhost:5000` (`/health`).

Credenciais de bootstrap em desenvolvimento são geradas pelo `start-mvp.ps1` em
`.jobpilot-dev-secrets.json` (ignorado pelo Git). O arquivo
`apps/api/appsettings.Development.json` mantém valores vazios por padrão e apenas
habilita `ApplyMigrations/Seed` em Development — a credencial real vem do arquivo
de segredos, nunca de valores fixos no repositório.

## Ollama (IA local, opcional)

Sem Ollama o produto funciona com análise determinística (sem IA).

Para ativar a IA local:

```powershell
# Instale o Ollama em https://ollama.com/download (Windows)
ollama pull qwen3:4b-instruct
ollama serve
```

Configuração padrão em `appsettings.json` (`LocalLlm:Enabled=true`,
`BaseUrl=http://localhost:11434`, `Model=qwen3:4b-instruct`).
Para rodar sem IA: `"LocalLlm:Enabled": false`.

Provedor externo alternativo (opcional): defina `KIMI_API_KEY` no ambiente para
usar o provedor Kimi OpenAI-compatible. Sem a chave, o Kimi é ignorado.

## Quality Gate

```powershell
.\scripts\quality.ps1
```

## Production-Like Configuration

Set at least:

```text
Auth__JwtKey
Security__SecretKey
ConnectionStrings__DefaultConnection
Frontend__Origins
```

Database migration and seed on startup are disabled outside Development by
default (`appsettings.json`: `false/false`). A exceção é o compose de produção
(`infra/docker-compose.production.yml`), que define ambos como `"true"`.
Fora desses dois casos, rode migrations como etapa controlada de deploy
(`dotnet ef database update`).
