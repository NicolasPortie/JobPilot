# JobPilot Browser Extension

Extensao Chrome MV3 que sera o canal principal do JobPilot para observar paginas de candidatura e executar comandos atomicos no navegador real do usuario.

## Objetivo

- Usar a sessao real do usuario, sem abrir um navegador separado.
- Observar LinkedIn, Gupy, Indeed e InHire a partir de content scripts.
- Enviar somente dados estruturados para o runtime do JobPilot.
- Executar comandos pequenos: observar, preencher, selecionar, marcar, clicar e rolar.
- Manter decisao, risco, politicas, redaction e auditoria no backend/harness.

## Como carregar localmente

1. Abra `chrome://extensions`.
2. Ative o modo de desenvolvedor.
3. Clique em `Carregar sem compactacao`.
4. Selecione `C:\Projetos\JobPilot\apps\extension`.

## Protocolo inicial

No popup da extensao, clique em `Conectar ao JobPilot`. O pareamento local e automatico: a extensao fala com a API local e guarda um segredo interno no `chrome.storage.local`. O usuario nao copia codigo, nao gera token e nao precisa manter o popup aberto. Se clicar em conectar de novo, a mesma instalacao local e reaproveitada em vez de criar varias conexoes duplicadas.

A extensao envia heartbeat para:

```txt
POST /api/extension/local-pair
POST /api/extension/heartbeat
```

O painel consulta:

```txt
GET /api/extension/status
```

Fila de comandos:

```txt
POST /api/extension/commands
POST /api/extension/commands/next
POST /api/extension/commands/{id}/result
```

O service worker da extensao mantem heartbeat e busca comandos em segundo plano. O popup serve apenas para conectar, exibir status e testar a pagina atual.

O content script responde a mensagens:

```json
{ "type": "JOBPILOT_COMMAND", "command": { "type": "observe" } }
```

Comandos suportados nesta primeira versao:

- `observe`
- `click`
- `fill`
- `select`
- `check`
- `scroll`
- `navigate`
- `extract_jobs`

Upload de arquivo nao e automatizado pela extensao: quando a pagina pedir curriculo e o arquivo ainda nao estiver anexado, o runtime deve criar checkpoint para o usuario escolher o arquivo no navegador.

## Plataformas cobertas

| Plataforma | Content script | Observacao | Acoes atomicas | Upload |
|---|---|---|---|---|
| LinkedIn | Sim | Sim, com extracao de vagas dedicada | Sim | Checkpoint/manual |
| Gupy | Sim | Sim, generica | Sim | Checkpoint/manual |
| Indeed | Sim | Sim, generica | Sim | Checkpoint/manual |
| InHire | Sim | Sim, generica com classificacao `inhire` | Sim | Checkpoint/manual |

Qualquer plataforma fora dessa matriz deve ser tratada como nao suportada ou manual ate manifest, background, content script, agente e documentacao estarem alinhados.

Esta pasta ainda nao substitui o agente local inteiro. Ela materializa o novo transporte principal; o runtime ja consegue usar esse caminho para observacao/acoes com `--browser-driver extension` ou `JOBPILOT_BROWSER_DRIVER=extension`.

No runtime .NET, a primeira ponte desse transporte vive em:

```txt
apps\agent\Browser\BrowserExtensionHttpTransport.cs
```
