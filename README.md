# JobPilot

JobPilot é um workspace local-first para preparar um currículo ATS verdadeiro e adaptado a cada vaga. A candidatura continua sendo uma decisão manual: o produto prepara o trabalho, não controla o computador, não entra em contas externas e não envia formulários.

## O fluxo do produto

1. Importe seu currículo base e complete seu perfil.
2. Traga uma vaga por link, texto colado, PDF ou screenshot.
3. Revise os requisitos encontrados, a fonte e a confiança da leitura.
4. Compare a vaga com o currículo base e veja pontos fortes, lacunas e perguntas de revisão.
5. Edite a versão adaptada, exporte PDF/DOCX/TXT e abra a vaga para se candidatar manualmente.

O produto está em `apps/web` (interface) e `apps/api` (API e persistência). Não há extensão de navegador nem automação: nada para instalar ou parear.

## Uso rápido (várias candidaturas)

1. Em **Perfil**, envie o PDF/DOCX do seu currículo uma vez. O JobPilot separa experiências, projetos, formação, certificações e idiomas e guarda uma versão base.
2. Em **Currículo**, cole a vaga (texto), um link ou vários links (um por linha, até 15) e pressione `Ctrl + Enter`.
3. Baixe o PDF (já nomeado `Curriculo-Nome-Cargo-Empresa.pdf`, em A4), abra a vaga e envie a candidatura **você mesmo**.
4. Marque como enviada e use **Próxima vaga**. A aba **Candidaturas** guarda o histórico para baixar de novo.

O currículo adaptado só reorganiza e prioriza o que já está no seu currículo: nenhuma tecnologia ou experiência é acrescentada. Requisitos da vaga que o currículo não comprova aparecem como lacunas para você decidir.

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
cd JobPilot
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

A IA (opcional) se configura pela aba **Configurações** do app: escolha o provedor, informe endereço, modelo e chave (guardada criptografada, nunca devolvida à tela) e use **Testar conexão**. O provedor escolhido ali vence a configuração do servidor, que vence o Ollama local. A configuração do servidor serve de padrão para quem não usa a tela:

```powershell
dotnet user-secrets set "Ai:Provider" "anthropic" --project apps\api   # ou openai, kimi
dotnet user-secrets set "Ai:ApiKey" "<sua-chave>" --project apps\api
```

Para um gateway compatível com OpenAI (ex.: OmniRoute em HTTPS), use `Ai:Provider=openai` com `Ai:BaseUrl` (terminando em `/v1`), `Ai:Model`, `Ai:Name` (rótulo na tela) e `Ai:AllowedHosts` (o host do gateway); a chave vai em `Ai:ApiKey`. Endereços `http://` ou de rede privada (`192.168.x.x`) são recusados de propósito. `Ai:Provider` + `Ai:ApiKey` vencem a configuração antiga `Kimi:ApiKey`/`KIMI_API_KEY`, que só vale quando `Ai:*` não está definido. E-mail e telefone não vão no prompt da IA: são trocados por marcadores e restaurados na resposta. Se o texto da IA for descartado (tecnologia sem evidência, contato sumido ou IA fora do ar), a tela diz o motivo e o currículo sai sem IA. O controle "Usar IA" da tela mostra se o provedor está respondendo; sem IA, o JobPilot gera o currículo de forma determinística, sem inventar conteúdo. Texto da IA é descartado se citar tecnologia que não está no seu currículo.

`Security:SecretKey` é independente da chave JWT.

## Contratos principais da API

- `GET/PUT /api/profile` — base profissional sem CPF ou caminhos físicos.
- `POST /api/profile/import-resume-file` — upload privado do currículo.
- `POST /api/profile/reimport-resume` — reprocessa o último arquivo enviado com o leitor atual.
- `POST /api/applications/quick` — vaga (link, vários links ou texto) → currículo adaptado em uma chamada.
- `GET /api/applications/history` e `PUT /api/applications/{versionId}/applied` — histórico e status manual de envio.
- `GET /api/ai/status` — provedor de IA configurado e resultado da última chamada.
- `POST /api/job-targets/organize` — análise de link ou texto da vaga.
- `POST /api/job-targets/organize-screenshot` — análise de screenshot/PDF enviado.
- `POST /api/resume/tailor` e `/api/resume/ats-build` — adaptação com evidências.
- `POST /api/resume/versions/{id}/exports` — exportação autorizada.

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

## Licença

MIT. Veja [LICENSE](LICENSE).
