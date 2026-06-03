# JobPilot

MVP local/private-first para descobrir, ranquear e aplicar em vagas com uma extensao de navegador auditavel. A tese e parecida com a do Vaga Automatica, mas com foco em confiabilidade: LinkedIn primeiro, limites claros, checkpoints, trilha de auditoria e expansao honesta para Gupy/Indeed quando os conectores estiverem maduros.

## Stack

- ASP.NET Core API em `apps/api`
- React + TypeScript + Vite em `apps/web`
- Extensao Chrome MV3 em `apps/extension`
- Runtime local .NET em `apps/agent` para decisao, guardrails, auditoria e compatibilidade de desenvolvimento
- PostgreSQL em `infra/docker-compose.yml`
- Ollama local (`qwen3:4b-instruct`) como LLM padrao do agente
- Kimi Code configuravel como provedor opcional para rascunhos de respostas

## Rodando localmente

```powershell
cd C:\Projetos\JobPilot
docker compose -f infra\docker-compose.yml up -d
dotnet run --project apps\api
```

Em outro terminal:

```powershell
cd C:\Projetos\JobPilot\apps\web
npm install
npm run dev
```

Abra:

```txt
http://127.0.0.1:5173
```

Atalho para subir banco, API e web:

```powershell
.\scripts\start-mvp.ps1
```

Login inicial:

```txt
admin@jobpilot.local
admin123
```

## Configuracao local segura

Nao coloque chaves ou senhas no repositorio. Use variaveis de ambiente ou user-secrets:

```powershell
dotnet user-secrets set "Kimi:ApiKey" "SUA_CHAVE" --project apps\api
dotnet user-secrets set "Kimi:BaseUrl" "https://api.kimi.com/coding" --project apps\api
dotnet user-secrets set "Kimi:Model" "kimi-for-coding" --project apps\api
dotnet user-secrets set "Kimi:Api" "anthropic-messages" --project apps\api
dotnet user-secrets set "Candidate:DefaultResumePath" "C:\Users\Nicolas\Downloads\Nova pasta\Curriculo Nicolas Portie.pdf" --project apps\api
```

Tambem funciona:

```powershell
$env:KIMI_API_KEY="SUA_CHAVE"
```

## LLM local

O JobPilot usa Ollama em `http://localhost:11434` por padrao, com o modelo `qwen3:4b-instruct`.
Esse modelo foi escolhido para o loop de decisao do agente em maquinas com 6 GB de VRAM.

```powershell
ollama pull qwen3:4b-instruct
```

Para trocar o modelo local:

```powershell
[Environment]::SetEnvironmentVariable("LocalLlm__Model", "qwen3:8b", "User")
```

## Harness de busca

Depois dos filtros baratos de senioridade, remoto e score, o backend pode usar o LLM local para revisar o fit de cada vaga antes de enfileirar no piloto automatico. A decisao possivel e `queue`, `ignore` ou `review`.

```powershell
[Environment]::SetEnvironmentVariable("SearchHarness__Enabled", "true", "User")
[Environment]::SetEnvironmentVariable("SearchHarness__MaxReviewsPerBatch", "12", "User")
```

Se o LLM local nao responder, a vaga fica em revisao em vez de entrar automaticamente na fila.

## Extensao de navegador

A nova direcao do JobPilot e extensao-first. A extensao usa a sessao real do usuario no Chrome, observa campos/botoes da pagina ativa e executa comandos atomicos aprovados pelo runtime. Ela nao deve conter decisao, politica, risco ou prompt de LLM.

Carregue a extensao local:

```txt
chrome://extensions
```

Ative o modo de desenvolvedor, escolha `Carregar sem compactacao` e selecione:

```txt
C:\Projetos\JobPilot\apps\extension
```

O MVP da extensao mira LinkedIn/Easy Apply primeiro. Gupy e Indeed entram como conectores progressivos, com status claro no produto em vez de promessa ampla sem comprovacao.

Plano completo de implementacao:

```txt
docs\IMPLEMENTATION_PLAN.md
```

## Runtime local

O runtime local continua existindo para orquestrar decisao, guardrails, trace, checkpoints, aprendizado e o fluxo legado de desenvolvimento enquanto o transporte real da extensao e conectado.

Para o usuario comum, o caminho esperado e abrir a extensao, clicar em `Conectar ao JobPilot` e operar pelo Cockpit. O pareamento da extensao e automatico, sem copiar codigo ou gerar token.

O modo abaixo fica para diagnostico tecnico e desenvolvimento do runtime legado:

```powershell
cd C:\Projetos\JobPilot
dotnet run --project apps\agent -- discover --token SEU_TOKEN --keywords "ASP.NET junior remote" --location Brazil
dotnet run --project apps\agent -- run --token SEU_TOKEN
```

Para testar o caminho extension-first no loop de candidatura:

```powershell
dotnet run --project apps\agent -- run --token SEU_TOKEN --browser-driver extension --dry-run
```

O runtime nao armazena senha do LinkedIn e nao tenta passar por captcha/2FA. No caminho extension-first, login e cookies ficam no navegador real do usuario.

Para um ensaio seguro, use dry-run. O runtime pode navegar/preencher no modo legado, mas para antes do envio final e registra checkpoint/evidencias:

```powershell
dotnet run --project apps\agent -- run --token SEU_TOKEN --dry-run
```

Se `JOBPILOT_AGENT_TOKEN` estiver configurado, use:

```powershell
.\scripts\run-agent-discover.ps1
.\scripts\run-agent-apply.ps1
```

Os atalhos de automacao usam a extensao por padrao. Para diagnostico tecnico com Playwright, configure `JOBPILOT_BROWSER_DRIVER=playwright`. O `run-agent-apply.ps1` roda com auto-submit habilitado por padrao quando `JOBPILOT_AUTO_SUBMIT` nao estiver definido. Para limitar o volume:

```powershell
[Environment]::SetEnvironmentVariable("JOBPILOT_MAX_APPLICATIONS", "10", "User")
[Environment]::SetEnvironmentVariable("JOBPILOT_DRY_RUN", "true", "User")
```

## Verificacao

```powershell
dotnet test JobPilot.slnx
cd apps\web
npm run lint
npm run build
```
