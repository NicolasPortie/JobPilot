# JobPilot: plano completo para virar produto real

Este plano partiu da decisao extension-first para o MVP. A decisao de produto atual e hibrida: extensao para consentimento, revisao e fallback; worker local dedicado como proximo passo para nao prender o navegador principal do usuario; worker remoto isolado como alvo SaaS.

Detalhe da decisao: [`SERVER_SIDE_AUTOMATION_STRATEGY.md`](SERVER_SIDE_AUTOMATION_STRATEGY.md).

## Norte do produto

O JobPilot nao deve prometer "aplicar em tudo" antes de provar. O produto deve fazer bem um fluxo estreito, mostrar tudo que aconteceu, pedir ajuda quando precisa e crescer por conectores.

Promessa inicial:

- LinkedIn/Easy Apply como conector estavel.
- Extensao Chrome como canal principal.
- Painel local para perfil, filtros, fila, auditoria, checkpoints e aprendizado.
- Gupy/Indeed como conectores beta quando houver cobertura real.

## Fase 1: Cockpit operacional

Objetivo: a tela inicial precisa responder "o que faco agora?".

Entregas:

- Colocar busca de vagas e piloto automatico no primeiro bloco do dashboard.
- Mostrar status da automacao, fila, checkpoints e candidaturas no mesmo contexto.
- Separar a aba `Agente` para diagnostico, eventos e historico tecnico.
- Deixar a navegacao com hierarquia: Operar, Revisar, Configurar.
- Reduzir telas duplicadas e textos tecnicos no fluxo principal.

Critério de pronto:

- Usuario logado consegue iniciar busca e aplicacao sem procurar em abas secundarias.
- Dashboard mostra se o sistema esta pronto, pausado, com pendencias ou sem vagas.

## Fase 2: Extensao conectada ao runtime

Objetivo: substituir o browser controlado como caminho principal.

Entregas:

- Criar canal autenticado entre `apps/extension` e runtime local.
- Implementar `IBrowserExtensionTransport` real.
- Pareamento local invisivel: o usuario clica em conectar e a extensao guarda o segredo internamente.
- Observacao da aba ativa com IDs estaveis e snapshot redigivel.
- Execucao atomica: fill, select, check, click, scroll, screenshot.
- Status da extensao no dashboard: conectada, aba suportada, dominio, ultima observacao.

Opcoes de transporte:

- WebSocket local autenticado: melhor para comando bidirecional em tempo real.
- Native Messaging: mais integrado ao Chrome, mas mais chato de instalar.
- Polling local autenticado: simples para MVP, menos elegante.

Recomendacao: WebSocket local primeiro, Native Messaging depois se empacotar para usuarios finais.

## Fase 3: LinkedIn confiavel

Objetivo: ter um conector estavel antes de ampliar promessa.

Entregas:

- Content script especifico para LinkedIn.
- Detector de feed/lista de vagas, pagina de vaga, Easy Apply, modal, steps e confirmacao.
- Normalizacao de selects, radios, uploads e perguntas frequentes.
- Modo review-only e auto-submit com politica explicita.
- Golden traces para casos LinkedIn simulados.
- Relatorio por candidatura com sucesso, bloqueio, checkpoint ou manual-only.

Critério de pronto:

- Fluxos LinkedIn Easy Apply comuns passam em dry-run e auto-submit controlado.
- Falhas viram checkpoint explicavel, nao loop silencioso.

## Fase 4: Scoring e fila melhores que automacao burra

Objetivo: aplicar menos, mas melhor.

Entregas:

- Scoring por cargo, senioridade, remoto, stack, salario, localidade e palavras bloqueadas.
- Explicacao curta do motivo de entrar ou sair da fila.
- Fila com prioridades e limites por execucao/dia.
- Auto-ignore de vagas ruins com auditoria.
- Revisao em lote de vagas borderline.

Critério de pronto:

- Usuario entende por que cada vaga foi aplicada, ignorada ou enviada para revisao.

## Fase 5: Respostas e checkpoints como vantagem competitiva

Objetivo: transformar interrupcoes em aprendizado.

Entregas:

- Inbox de checkpoints na tela inicial.
- Resolver checkpoint salvando resposta reutilizavel quando fizer sentido.
- Biblioteca de respostas com origem: perfil, manual, IA revisada, politica.
- Sensibilidade por categoria: salario, diversidade, autorizacao, documentos, CPF.
- Upload de curriculo pela extensao deve virar checkpoint simples quando o arquivo ainda nao estiver anexado.
- Nunca inventar dado sensivel.

Critério de pronto:

- A cada checkpoint resolvido, o sistema fica melhor sem perder seguranca.

## Fase 6: Auditoria e suporte

Objetivo: evitar a caixa-preta que derruba confianca.

Entregas:

- Timeline por candidatura com observacao, proposta, guardrail, execucao, resultado e evidencia.
- Resumo operacional legivel antes dos JSONs tecnicos.
- Export de diagnostico redigido para suporte.
- Indicador de saude: extensao conectada, API, banco, LLM, ultima automacao.

Critério de pronto:

- Quando algo falha, o usuario e o suporte conseguem ver onde e por que.

## Fase 7: Conectores Gupy/Indeed

Objetivo: expandir sem repetir o erro de promessa ampla.

Entregas:

- Status por conector: experimental, beta, estavel.
- Gupy: leitura de formulario, campos recorrentes, upload e checkpoint de submit.
- Indeed: descoberta e aplicacao assistida conforme superficie real.
- Testes fixture para cada ATS antes de ativar auto-submit.

Critério de pronto:

- Conector so aparece como estavel quando tiver cobertura testada e auditoria.

## Fase 8: Empacotamento

Objetivo: sair de projeto local para produto instalavel.

Entregas:

- Instalador do runtime local.
- Extensao empacotada.
- Onboarding guiado: perfil, curriculo, extensao, login LinkedIn, primeira busca.
- Politicas padrao conservadoras.
- Logs locais redigidos.

Critério de pronto:

- Um usuario novo consegue configurar e rodar a primeira candidatura assistida sem terminal.

## Ordem imediata de implementacao

1. Reorganizar dashboard como cockpit. Status: iniciado.
2. Criar tela/estado de extensao conectada. Status: iniciado com pareamento local invisivel e heartbeat.
3. Criar fila inicial de comandos da extensao. Status: iniciado com polling em background pelo service worker.
4. Introduzir `ExecutionTarget` / roteador de execucao: `extension`, `local_dedicated_browser`, `remote_browser`, `server_connector`. Status: iniciado; API, politica, health operacional e agent ja distinguem extensao e navegador local dedicado.
5. Implementar worker local dedicado com perfil separado do JobPilot. Status: iniciado com `local_dedicated_browser`, perfil persistente configuravel por `JOBPILOT_BROWSER_PROFILE` / `--browser-profile` e pasta padrao por usuario.
6. Avaliar WebSocket local ou Native Messaging para comandos mais imediatos antes do empacotamento.
7. Adaptar `BrowserExtensionDriver` para o transporte real. Status: iniciado com `BrowserExtensionHttpTransport`.
8. Ligar o runtime de candidatura ao driver da extensao por feature flag. Status: iniciado com `--browser-driver extension` / `JOBPILOT_BROWSER_DRIVER=extension`.
9. Migrar descoberta LinkedIn para extensao. Status: iniciado com comandos `navigate` e `extract_jobs` no content/background e `BrowserExtensionJobDiscovery`.
10. Migrar abertura inicial da vaga para extensao. Status: iniciado com `pre_apply.navigate`, observacao da aba real e checkpoint de login/seguranca.
11. Fazer Cockpit iniciar automacao extension-first. Status: iniciado; `/automation/discover` e `/automation/apply` usam extensao por padrao e pedem conexao quando ela nao esta ativa.
12. Promover LinkedIn Easy Apply para conector estavel em modo review-first.
13. Comecar Gupy/Indeed/InHire como beta, priorizando conectores server-side quando nao dependerem de sessao LinkedIn.
