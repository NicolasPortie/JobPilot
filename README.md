# JobPilot

JobPilot é um workspace local-first para preparar um currículo ATS verdadeiro e adaptado a cada vaga. A candidatura continua sendo uma decisão manual: o produto prepara o trabalho, não controla o computador, não entra em contas externas e não envia formulários.

## O fluxo do produto

1. Importe seu currículo base e complete seu perfil.
2. Traga uma vaga por link, texto colado, PDF ou screenshot.
3. Revise os requisitos encontrados, a fonte e a confiança da leitura.
4. Compare a vaga com o currículo base e veja pontos fortes, lacunas e perguntas de revisão.
5. Edite a versão adaptada, exporte PDF/DOCX/TXT e abra a vaga para se candidatar manualmente.

O núcleo do produto está em `apps/web` e `apps/api`. O runtime `apps/agent` é legado em quarentena: não participa do fluxo web e permanece somente para migração, testes históricos e eventual decisão futura. A extensão do navegador foi removida do produto (código, protocolo, tabelas e docs): não há nada para instalar ou parear.

## Stack

- ASP.NET Core + Entity Framework Core em `apps/api`
- React + TypeScript + Vite em `apps/web`
- PostgreSQL em `infra/docker-compose.yml`
- Ollama local como provedor opcional de análise

## Pré-requisitos

- .NET 10 SDK
- Node.js 24
- Docker com Compose
- Ollama local (opcional, para análise com IA — veja `docs/operations/LOCAL_DEVELOPMENT.md`)

## Rodando localmente

O Docker sobe apenas o PostgreSQL. A API e a web são iniciadas separadamente.

O atalho recomendado cuida de segredos, senha do Postgres e health-checks:

```powershell
cd C:\Projetos\JobPilot
.\scripts\start-mvp.ps1
```

> Não rode `docker compose -f infra\docker-compose.yml up -d` manualmente sem antes
> exportar `JOBPILOT_POSTGRES_PASSWORD` — o compose exige essa variável e falha sem ela.
> O `start-mvp.ps1` já faz isso para você.

O script gera segredos locais e uma credencial de desenvolvimento aleatória em `.jobpilot-dev-secrets.json`, arquivo ignorado pelo Git. Ele exibe o e-mail de bootstrap no terminal; a senha nunca fica no repositório.

Para executar manualmente:

```powershell
docker compose -f infra\docker-compose.yml up -d
dotnet run --project apps\api
```

Em outro terminal:

```powershell
cd apps\web
npm ci
npm run dev
```

Preview local: `http://localhost:5173`.

## Configuração segura

Não coloque chaves, senhas ou caminhos pessoais no repositório. Para uma instância nova, configure pelo ambiente ou pelo User Secrets:

```powershell
dotnet user-secrets set "Auth:JwtKey" "uma-chave-com-pelo-menos-32-caracteres" --project apps\api
dotnet user-secrets set "Security:SecretKey" "outra-chave-independente-com-32-caracteres" --project apps\api
dotnet user-secrets set "Auth:SeedEmail" "seu-email-local@example.com" --project apps\api
dotnet user-secrets set "Auth:SeedPassword" "uma-senha-local-forte" --project apps\api
```

`Security:SecretKey` é independente da chave JWT. O runtime legado fica desligado por padrão (`Features:EnableLegacyRuntime=false`) e não deve ser ativado em uma instalação de produção.

## Contratos principais da API

- `GET/PUT /api/profile` — base profissional sem CPF ou caminhos físicos.
- `POST /api/profile/import-resume-file` — upload privado do currículo.
- `POST /api/job-targets/organize` — análise de link ou texto da vaga.
- `POST /api/job-targets/organize-screenshot` — análise de screenshot/PDF enviado.
- `POST /api/resume/tailor` e `/api/resume/ats-build` — adaptação com evidências.
- `POST /api/resume/versions/{id}/exports` — exportação autorizada.

Os endpoints de automação retornam `410 Gone`. Eles não devem ser reativados para completar o fluxo principal.

## Regra de engenharia (permanente)

Frontend nunca tem lógica de negócio. Vereditos, classificações, scores, limiares e regras de completude vivem no backend e chegam prontos nos contratos. O frontend só formata, agrupa para exibir e projeta estados que o backend já calculou. Nada de recriar no TypeScript o que o C# já decidiu.

## Verificação

```powershell
.\scripts\quality.ps1
```

Também é possível rodar as partes separadamente:

```powershell
dotnet test JobPilot.slnx --configuration Release
cd apps\web
npm run check
```

Consulte `docs/README.md`, `docs/ARCHITECTURE.md`, `docs/REESTRUTURACAO_PRODUTO.md` e `SECURITY.md` para a decisão de produto, limites e plano de migração.
