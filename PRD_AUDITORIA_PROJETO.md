# PRD de auditoria do projeto JobPilot

Data da auditoria: 2026-06-03  
Fonte principal: analise estatica do worktree em `C:\Projetos\JobPilot`, `goal.md`, `README.md`, `docs/ARCHITECTURE.md`, codigo em `apps/api`, `apps/web`, `apps/agent`, `apps/extension`, migrations EF Core e documentos internos de ATS.

## 1. Visao geral do projeto

O JobPilot e um sistema local/private-first para descobrir vagas, ranquear oportunidades, preparar candidaturas, apoiar respostas de triagem e executar aplicacoes assistidas/automatizadas em sites como LinkedIn e ATS externos. O projeto combina:

- API ASP.NET Core em `apps/api`.
- Frontend React + TypeScript + Vite em `apps/web`.
- Runtime/agente .NET em `apps/agent`.
- Extensao Chrome MV3 em `apps/extension`.
- PostgreSQL via `infra/docker-compose.yml`.
- Provedores de IA configuraveis, incluindo Ollama local, Kimi/OpenAI-compatible e OpenAI Responses.

O problema que o sistema tenta resolver e reduzir o trabalho manual de buscar vagas, avaliar aderencia, preencher formularios e enviar curriculos especificos por vaga, mantendo trilha de auditoria e checkpoints humanos.

Estado atual percebido: o produto tem uma base tecnica ampla e ja possui muitos blocos funcionais, mas esta concentrado em poucos arquivos grandes e com fluxos sobrepostos. A SPA usa abas internas em um unico `App.tsx` em vez de rotas por URL; o backend possui boa separacao por endpoints/services, mas ainda ha regras de negocio espalhadas entre API, agent e frontend. O modulo ATS/curriculo evoluiu rapidamente e tem backend robusto para piloto, mas ainda precisa validacao fim a fim, consolidacao de UX e revisao forte da qualidade visual/textual dos PDFs.

Ponto critico: a geracao de PDF atual e ATS-safe e parseavel por design, mas nao e um PDF profissional completo. O PDF e gerado manualmente com Helvetica/ASCII, uma coluna, pouco acabamento e conversao de caracteres fora de ASCII para `?`, o que pode degradar nomes, termos em portugues e apresentacao ao recrutador.

## 2. Objetivo do PRD

Este PRD serve para mapear o estado atual do JobPilot, identificar desorganizacoes, redundancias, riscos de arquitetura e fluxos confusos, e estruturar uma reorganizacao por fases antes da implementacao de novas funcionalidades.

O documento nao define novas features complexas como prioridade imediata. A prioridade e estabilizar o que ja existe: frontend, backend, banco, agente, extensao, ATS, curriculos, geracao de PDF, fluxo de candidatura, seguranca, UX e manutencao.

## 3. Mapa atual do sistema

### Modulos identificados

- Autenticacao: login JWT em `/api/auth/login`, seed local `admin@jobpilot.local`.
- Dashboard/Cockpit: visao operacional de saude, extensao, metricas e disparo de descoberta/aplicacao.
- Perfil do candidato: cadastro de dados pessoais, skills, resumo, curriculo base e importacao de curriculo.
- Curriculos/ATS: biblioteca de curriculos, versoes, secoes estruturadas, organizacao de vaga, mapa de evidencias, tailoring, score, diff, perguntas, regeneracao e exportacao.
- Buscas salvas: configuracao de palavras-chave/localidade para descoberta.
- Fila de vagas: listagem, status, selecao em lote, detalhe, auditoria e vinculo manual de curriculo.
- Execucoes: runs e application jobs com status e indicacao de curriculo vinculado/gerado.
- Respostas reutilizaveis: respostas de screening e rascunho por IA.
- Filtros e regras: senioridade, role keywords, workplace, apply types, score minimo, question patterns e validacoes de perfil.
- Provedores de IA: cadastro/teste de providers e politica do agente.
- Contas/plataformas: credenciais por plataforma, com senha protegida no backend.
- Checkpoints e automacao: eventos, checkpoints, pareamento do agente, discover/apply.
- Aprendizados do agente: candidatos/aprovados/rejeitados/desativados, dominio, risco e evidencias.
- Extensao Chrome: pareamento local, heartbeat e comandos atomicos de browser.
- Runtime/agente: observacao, normalizacao, deteccao ATS, planejamento, guardrails, execucao, trace, checkpoints e recuperacao.
- Banco de dados: PostgreSQL com EF Core migrations.
- Integracoes: LinkedIn, InHire, extensao Chrome, LLM local/remoto, OCR local e OpenAI Responses multimodal.
- Administracao: nao foi encontrada area administrativa separada; as configuracoes ficam distribuidas entre IA, filtros, contas, perfil e agente.
- Relatorios: nao foi encontrado modulo formal de relatorios; o que existe hoje e dashboard operacional, auditoria tecnica e historico de execucoes.
- Configuracoes: existem, mas estao espalhadas em varias abas em vez de centralizadas por dominio.

### Fluxos principais

1. Login -> refresh de dados -> cockpit.
2. Perfil -> importar curriculo por arquivo/caminho -> extrair texto -> criar biblioteca/versao/secoes.
3. Curriculo ATS -> selecionar versao base -> informar vaga por texto/URL/screenshot -> organizar requisitos -> gerar versao direcionada -> revisar diff/perguntas -> exportar TXT/DOCX/PDF.
4. Buscas/filtros -> descobrir vagas -> criar/atualizar `JobLead`.
5. Fila de vagas -> selecionar vagas -> criar run -> preparar curriculo por candidatura -> `ApplicationJob`.
6. Agente/extensao -> observar pagina -> preencher campos -> criar checkpoints -> persistir trace/auditoria.
7. Aprendizados -> revisar padroes gerados a partir de execucoes.

## 4. Inventario de telas, rotas e fluxos

Observacao importante: o frontend nao usa roteamento por URL. Todas as telas autenticadas sao abas internas controladas pelo estado `tab` em `apps/web/src/App.tsx`. A rota externa real e a raiz da SPA, normalmente `http://127.0.0.1:5173/`. Neste PRD, "rota" indica a aba interna.

### Autenticacao

| Tela | Rota | Modulo | Objetivo | Acoes do usuario | APIs/dados usados | Componentes principais | Status | Problemas encontrados | Recomendacoes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Login | `/` sem token | Autenticacao | Autenticar usuario local | Informar email/senha, alternar visibilidade de senha | `POST /api/auth/login` | Login shell/card no `App.tsx` | Ativa | Credenciais seed aparecem no frontend como default; UX depende de API local ativa | Remover defaults sensiveis da UI em ambiente nao-dev; exibir estado de API com diagnostico claro |

### Operacao

| Tela | Rota | Modulo | Objetivo | Acoes do usuario | APIs/dados usados | Componentes principais | Status | Problemas encontrados | Recomendacoes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Cockpit | `tab=dashboard` | Dashboard/operacao | Mostrar prontidao, saude, extensao, metricas e disparos rapidos | Atualizar, iniciar descoberta, autopilot, recuperar saude, navegar para configuracoes | Refresh geral: `/profile`, `/jobs`, `/runs`, `/checkpoints`, `/extension/status`, `/health/operational`; acoes `/automation/discover`, `/automation/apply`, `/health/operational/recover` | `MetricCard`, cards de saude, launch panel | Ativa | Cockpit mistura dashboard, launch de automacao e diagnostico; hero visual ocupa area grande em ferramenta operacional | Separar "status operacional" de "acoes de execucao"; reduzir peso visual e priorizar alertas bloqueantes |
| Fila de vagas | `tab=jobs` | Vagas/candidaturas | Listar vagas descobertas, filtrar, selecionar, enfileirar e auditar | Filtrar por status, selecionar vagas, atualizar status, enfileirar run, vincular curriculo, abrir detalhe/auditoria | `GET /jobs`, `PATCH /jobs/{id}/status`, `POST /runs`, `PATCH /jobs/{id}/resume`, `GET /jobs/{id}/audit` | `JobsTab`, `JobDetailCard`, `TraceTimeline` | Ativa | Muita responsabilidade na mesma tela: tabela, detalhe, CV, auditoria tecnica; pode ficar densa e confusa | Separar detalhe/auditoria em subabas mais claras ou rota propria; manter vinculo de CV visivel e auditavel |

### Revisao

| Tela | Rota | Modulo | Objetivo | Acoes do usuario | APIs/dados usados | Componentes principais | Status | Problemas encontrados | Recomendacoes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Checkpoints e automacao | `tab=agent` | Agente/checkpoints | Resolver pendencias e controlar automacao local | Parear agente, iniciar discover/apply, resolver checkpoint, alternar eventos/historico | `/agent/pair`, `/automation/discover`, `/automation/apply`, `/checkpoints/{id}/resolve`, `/events`, `/agent/history` | `AgentTab` | Ativa, confusa | Nome da tela mistura checkpoint, automacao, eventos e historico; "agent token" e runtime legado coexistem com extensao-first | Redesenhar como "Central de revisao" e mover comandos tecnicos para area dev/diagnostico |
| Execucoes | `tab=runs` | Runs/candidaturas | Mostrar runs e application jobs | Ver status e badges de CV | `GET /runs` | `RunsTab` | Ativa | Visao resumida; nao parece permitir diagnostico profundo sem voltar a vagas/auditoria | Linkar run -> application jobs -> auditoria/curriculo usado |
| Aprendizados | `tab=learnings` | Agente/aprendizado | Revisar aprendizados do agente | Filtrar, abrir aprendizado, aprovar/rejeitar/desativar | `GET /agent/learnings`, `POST /agent/learnings/{id}/approve|reject|disable` | `LearningsTab` | Ativa | Conceito avancado para usuario comum; risco de aprovar regra ruim sem contexto visual suficiente | Exigir evidencia clara, impacto, exemplos e rollback antes de aprovar |

### Configuracao

| Tela | Rota | Modulo | Objetivo | Acoes do usuario | APIs/dados usados | Componentes principais | Status | Problemas encontrados | Recomendacoes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Perfil | `tab=profile` | Perfil/curriculo base | Editar dados do candidato e importar curriculo | Editar campos, salvar, importar por caminho, upload de arquivo, revisar texto extraido | `GET/PUT /profile`, `GET /profile/validate`, `POST /profile/import-resume`, `POST /profile/import-resume-file` | `ProfileTab` | Ativa | Perfil e importacao de CV ficam juntos; `resumePath` local e upload real coexistem e podem confundir | Separar "dados do perfil" de "biblioteca de curriculos"; tratar caminho local como modo dev |
| Curriculo ATS | `tab=resume` | ATS/curriculos/PDF | Criar versao ATS generica ou por vaga, revisar e exportar | Escolher versao/layout, colar vaga/URL/screenshot, gerar, editar secoes/diff, responder unknowns, exportar TXT/DOCX/PDF | `/resume/documents`, `/resume/layouts`, `/resume/ats-build`, `/job-targets/organize`, `/job-targets/organize-screenshot`, `/resume/tailor`, `/resume/review-answers`, `/resume/review-answers/regenerate`, `/resume/versions/{id}/exports`, `/resume/exports/{id}/download`, `/resume/versions/{id}/sections` | `ResumeAtsTab`, `SectionEditorPanel`, `SectionDiffList`, preview visual | Ativa, parcialmente organizada | Tela concentra biblioteca, editor, job target, score, diff, perguntas, preview e exportacao; risco alto de sobrecarga cognitiva | Redesenhar fluxo em etapas: base -> vaga -> evidencias -> revisar -> exportar; manter estado persistido e recuperavel |
| Buscas | `tab=searches` | Descoberta | CRUD de buscas salvas | Criar/editar/excluir busca | `GET/POST/PUT/DELETE /searches` | `SearchesTab` | Ativa | Campos default rigidos no frontend: workplace remote, seniority junior, limit 25 | Trazer defaults do backend/politica; mostrar campos completos ou explicar default |
| Respostas | `tab=answers` | Screening/IA | CRUD de respostas reutilizaveis e rascunho por IA | Criar/editar/excluir resposta, gerar rascunho | `/answers`, `/ai/answer-draft` | `AnswersTab` | Ativa | Sobreposicao conceitual com `QuestionPattern`, `ProfileValidationRule` e resposta `unknown` do ATS | Consolidar taxonomia de "respostas reutilizaveis", "padroes" e "respostas de evidencias ATS" |
| Filtros e regras | `tab=filters` | Politicas/validacao | Configurar filtros de vagas, validacoes e question patterns | Ajustar listas allow/block, salvar, testar pattern | `/job-filters`, `/question-patterns`, `/question-patterns/resolve`, `/profile/validate` | `FiltersTab`, `ChipToggleGroup` | Ativa, confusa | Tela mistura filtros de busca/aplicacao, validacao de perfil e resolver de perguntas | Separar regras de vaga, regras do perfil e regras de respostas |
| IA | `tab=ai` | Provedores/politica | Configurar provedores de IA e politica do agente | CRUD provider, testar provider, definir default, salvar politica | `/ai/providers`, `/ai/providers/{id}/test`, `/agent/policy` | `AiTab` | Ativa | Politica de execucao do agente fica dentro de tela de IA; provider e auto-submit sao dominios diferentes | Separar "IA" de "politica de automacao" |
| Contas | `tab=credentials` | Integracoes | Gerenciar credenciais de plataformas | Criar/editar/excluir conta | `/platform-credentials` | `CredentialsTab` | Ativa | Senhas de plataformas existem no produto mesmo com direcao extension-first sem armazenar senha LinkedIn | Revisar necessidade; preferir sessao do browser/extensao e limitar credenciais a conectores que realmente precisam |

### Telas/fluxos sem tela dedicada clara

| Fluxo | Rota | Modulo | Objetivo | APIs/dados usados | Status | Problemas encontrados | Recomendacoes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Extensao Chrome popup | `apps/extension/popup.html` | Extensao | Parear extensao e testar pagina | `/extension/local-pair`, `/extension/heartbeat`, comandos | Ativa tecnica | Nao e tela web principal; dependencia operacional importante | Documentar onboarding e status no cockpit |
| Auditoria tecnica de vaga/application job | Dentro de Jobs detail | Auditoria | Ver fields, answers, steps, trace, checkpoints, events, learnings | `/jobs/{id}/audit`, `/application-jobs/{id}/audit` | Ativa | Muito tecnica; risco de baixa compreensao por usuario comum | Manter modo tecnico, mas adicionar resumo executivo de bloqueios |
| OCR status | Sem tela dedicada clara | OCR/curriculo | Ver disponibilidade Tesseract/OCRmyPDF | `/resume/ocr/status` | Possivelmente incompleta na UI | Endpoint existe, mas inventario nao confirmou painel claro para status OCR | Expor no fluxo de importacao/ATS como prerequisito diagnostico |

## 5. Diagnostico geral do projeto

### Redundancias e duplicidades

- Frontend centralizado em `apps/web/src/App.tsx` com aproximadamente milhares de linhas, acumulando estado, acoes de API, renderizacao, editor de curriculo, auditoria e utilitarios.
- Conceitos sobrepostos: respostas reutilizaveis, question patterns, review answers de ATS, profile validation rules e agent answer resolution.
- Automacao extension-first e runtime legado convivem: `AgentTab` ainda expoe pareamento de agent token enquanto a arquitetura atual prioriza extensao.
- Endpoints de auditoria aparecem por `jobs/{id}` e `application-jobs/{id}`, com possibilidade de confusao de identidade entre lead e candidatura concreta.
- `PATCH /jobs/{id}/resume` e `PATCH /application-jobs/{id}/resume` coexistem; o primeiro cria/usa candidatura tecnica via servico. Isso facilita UX, mas mistura "vaga" com "candidatura".
- Nao foram encontradas telas duplicadas no sentido literal de duas rotas iguais para a mesma coisa, mas existem telas parecidas/conceitualmente sobrepostas: `Respostas` vs `Filtros e regras`/question patterns, `Checkpoints e automacao` vs `Execucoes`, e `Perfil` vs `Curriculo ATS` para dados de curriculo.

### Fluxos confusos

- A aba `Curriculo ATS` faz muitas coisas: biblioteca, geracao generica, tailoring por vaga, editor de secoes, diff, perguntas, preview e export. O usuario pode nao entender se esta editando o curriculo base, uma versao gerada ou uma versao para uma vaga.
- A tela `Filtros e regras` mistura filtros de descoberta com validacao de perfil e padroes de perguntas.
- A tela `IA` tambem configura politica de execucao do agente, o que pertence ao dominio de automacao/risco.
- A tela `Agent` mistura comandos operacionais, checkpoints, eventos e historico tecnico.

### Inconsistencias e riscos de manutencao

- Textos no codigo/documentos apresentam sinais de mojibake em varios pontos (`Ã`, `â`, etc.). Para um produto que gera curriculo em portugues, isso e risco direto de qualidade.
- O frontend usa estado local amplo e `refresh()` global que busca quase tudo em paralelo; qualquer endpoint instavel impacta varias telas.
- O projeto possui muitos artefatos gerados versionados/no worktree (`dist`, logs, documentos exportados, screenshots, node_modules), dificultando auditoria e revisao.
- Algumas documentacoes internas de ATS estao desatualizadas entre si: um documento marca itens como lacuna, outro marca como implementado.
- A validacao runtime fim a fim com Postgres/API/Web/LinkedIn nao esta comprovada nesta auditoria; documentos registram bloqueio por Postgres/Docker inativo.

## 6. Diagnostico tecnico do frontend

### Estrutura

Existe uma SPA React/Vite em `apps/web`, com:

- `src/App.tsx`: componente principal, estados, chamadas de API, renderizacao de todas as abas e varios subcomponentes.
- `src/api/client.ts`: instancia Axios com base URL `VITE_API_URL` ou `http://localhost:5000/api`.
- `src/types.ts`: contratos TypeScript principais.
- `src/components/layout.tsx`: Sidebar, Topbar e MetricCard.
- `src/constants/labels.ts`: labels/status.
- `src/hooks/useJobAudit.ts`: hook para auditoria.
- `src/index.css`: design system CSS manual.

### Pontos positivos

- Tipos TS cobrem grande parte dos DTOs da API.
- Layout operacional consistente, com sidebar, topbar, cards, badges, tabelas e estados.
- Uso de lucide-react para icones.
- A aba ATS renderiza score, requisitos, diff, perguntas, preview e exportacao.
- Existe responsividade basica via media queries.

### Problemas

- `App.tsx` virou um "god component". Isso dificulta teste, manutencao, revisao e onboarding.
- Nao ha rotas reais. Abas internas nao geram URLs compartilhaveis, historico de navegacao, deep links ou reload preservando contexto.
- Muitas chamadas de API e regras de fallback ficam no componente principal, por exemplo selecao automatica da ultima versao de curriculo.
- Tratamento de erro e inconsistente: alguns endpoints usam `.catch`, outros deixam `finally`, outros gravam mensagens genericas.
- Estado local muito amplo aumenta risco de stale state, principalmente no fluxo ATS com `atsTailoredResume`, `atsJobTarget`, `reviewAnswers`, versao selecionada e layout.
- UI operacional tem mistura de foco: cockpit com hero grande, ATS com preview/editor/gerador, agent com historico tecnico.
- Responsividade existe, mas algumas areas densas como tabelas, trace JSON, editor de secoes e preview sticky podem ficar ruins em telas pequenas.
- O padrao visual e coerente, mas usa muitos cards e grids dentro de uma ferramenta operacional; em telas densas, isso aumenta scroll e fragmentacao.

### Recomendacoes

- Quebrar `App.tsx` por modulo: `Dashboard`, `Profile`, `ResumeAts`, `Jobs`, `Agent`, `Settings`.
- Introduzir roteamento client-side com URLs como `/dashboard`, `/jobs`, `/resumes`, `/resumes/:id`, `/jobs/:id/audit`.
- Criar camada de API por dominio: `profileApi`, `resumeApi`, `jobsApi`, `agentApi`.
- Padronizar loading/error/toast/empty states.
- Transformar `ResumeAtsTab` em fluxo por etapas com estado persistente no backend.
- Separar componentes de formulario do editor de secoes e do preview visual.

## 7. Diagnostico tecnico do backend

### Estrutura

O backend esta em ASP.NET Core minimal APIs:

- `Program.cs`: DI, CORS, auth JWT, migrations automaticas, seed, mapeamento de endpoints.
- `Endpoints/*.cs`: grupos de endpoints por dominio.
- `Services/*.cs`: regras de negocio.
- `Domain/Entities.cs`: entidades e DTOs/records.
- `Data/JobPilotDbContext.cs`: DbSets, indices, relacionamentos e seed.
- `Data/Migrations/*`: migrations EF Core.

### Pontos positivos

- Endpoints sao relativamente finos e delegam para services.
- DbContext possui indices importantes por usuario, data, FK e unicidade.
- JWT e `RequireAuthorization` protegem a maioria dos endpoints.
- Existe redacao de trace (`TraceRedactionService`) e protecao de secrets (`SecretProtector`).
- ATS/curriculo tem services dedicados: `ResumeTextService`, `ResumeOcrService`, `ProfileImportService`, `JobTargetService`, `ResumeEvidenceMapService`, `ResumeTailoringService`, `ResumeExportService`, `ResumeSectionService`, `ResumeReviewAnswerService`.
- Testes cobrem varios services de API e agent.

### Problemas

- `Domain/Entities.cs` centraliza entidades e DTOs de muitos dominios em um unico arquivo grande. Isso cria acoplamento e dificulta evolucao.
- `Program.cs` executa migrations no startup. Isso e aceitavel para MVP local, mas arriscado para SaaS/ambiente multiusuario.
- JWT key default `dev-only-change-this-key-please-32` e seed de admin sao adequados apenas para dev; precisam guardrails de ambiente.
- CORS permite `chrome-extension://` e origens locais; precisa revisao quando virar SaaS.
- Algumas regras de negocio ainda existem no agente e no frontend, especialmente automacao, escolha de curriculo e upload.
- Erros sao frequentemente `InvalidOperationException` com mensagem; falta envelope padronizado de erro/API problem details.
- Logs/observabilidade existem via eventos/traces, mas falta diagnostico centralizado para saude de OCR, IA, extensao, banco e agente.
- `JobTargetService` faz scraping simples de URL com regex HTML-to-text. Funciona como fallback, mas e fragil para sites reais.

### APIs identificadas

- Auth: `POST /api/auth/login`.
- Profile: `GET/PUT /api/profile`, `GET /api/profile/validate`, `POST /api/profile/import-resume`, `POST /api/profile/import-resume-file`, `GET /api/resume/ocr/status`, `profile-validation-rules`.
- Resume/ATS: `GET /resume/imports`, `GET /resume/documents`, `GET /resume/layouts`, `GET /resume/versions/{id}`, `GET/POST /resume/versions/{id}/sections`, `PUT/DELETE /resume/sections/{id}`, `POST /resume/versions/{id}/sections/reorder`, `POST /resume/tailor`, `POST /resume/ats-build`, `POST /resume/versions/{id}/exports`, `GET /resume/exports/{id}/download`, `POST/GET /resume/review-answers`, `POST /resume/review-answers/regenerate`, `POST/GET /resume/evidence-maps`.
- Job targets: `GET /job-targets`, `GET /job-targets/{id}`, `POST /job-targets/organize`, `POST /job-targets/organize-screenshot`.
- Jobs: `GET /jobs`, `GET /jobs/{id}`, `PATCH /jobs/{id}/status`, `PATCH /jobs/{id}/resume`, audit endpoints.
- Runs/application jobs: `POST /runs`, `GET /runs`, `PATCH /application-jobs/{id}/resume`.
- Answers/patterns: CRUD `/answers`, CRUD `/question-patterns`, `POST /question-patterns/resolve`.
- Filters: `GET/PUT /job-filters`.
- AI: CRUD/test `/ai/providers`, `POST /ai/answer-draft`.
- Agent management: `/agent/policy`, `/agent/history`, `/agent/pair`.
- Agent runtime: `/agent/tasks`, `/agent/runs`, `/agent/steps`, `/agent/trace-steps`, `/agent/checkpoints`, `/agent/application-fields`, `/agent/application-answers`, `/agent/learnings`.
- Automation: `/automation/discover`, `/automation/apply`.
- Extension: `/extension/status`, `/extension/pair`, `/extension/local-pair`, `/extension/heartbeat`, `/extension/commands`.
- Health/events/checkpoints/platform credentials.

## 8. Diagnostico do banco de dados

### Entidades centrais

- Usuario/perfil: `Users`, `CandidateProfiles`.
- Curriculos/ATS: `ResumeImports`, `ResumeDocuments`, `ResumeVersions`, `ResumeSections`, `ResumeExports`, `ResumeTailoringRuns`, `ResumeReviewAnswers`, `JobTargets`, `JobRequirements`, `ResumeEvidenceMapRuns`, `ResumeRequirementEvidences`.
- Vagas/candidaturas: `SavedSearches`, `JobLeads`, `ApplicationRuns`, `ApplicationJobs`.
- Agente/auditoria: `AgentRuns`, `AgentSteps`, `AgentTraceSteps`, `ApplicationFields`, `ApplicationAnswers`, `SiteWorkflowMemories`, `AgentLearnings`, `ExecutionEvents`, `Checkpoints`, `AgentDevices`.
- Configuracoes: `ReusableAnswers`, `QuestionPatterns`, `JobFilters`, `ProfileValidationRules`, `AgentPolicies`, `AiProviders`, `AiUsageLogs`, `PlatformCredentials`.
- Extensao: `BrowserExtensionSessions`, `BrowserExtensionCommandJobs`.

### Relacionamentos e indices

- `CandidateProfile.UserId` unico.
- `JobLead` unico por `UserId + Url`.
- `ReusableAnswer` unico por `UserId + QuestionKey`.
- `AiProvider` unico por `UserId + Name`.
- `AgentPolicy.UserId` unico.
- `QuestionPattern` unico por `UserId + PatternKey`.
- `JobFilter.UserId` unico.
- `ResumeDocument` -> `ResumeVersion` -> `ResumeSection` com cascade.
- `JobTarget` -> `JobRequirement` com cascade.
- `ResumeEvidenceMapRun` -> `ResumeRequirementEvidence` com cascade.
- Indices em exports, tailoring, evidence maps e application jobs por usuario/versao.

### Problemas e riscos

- `ResumeVersion.PlainText`, `ResumeSection.ContentJson`, `AtsReportJson`, `ResultJson`, `WarningsJson`, trace JSONs e outros campos JSON/texto indicam muita informacao semi-estruturada sem governanca de schema versionado.
- `ApplicationJob` referencia `ResumeVersionId`, `ResumeExportId` e `ResumeFilePath`, mas os relacionamentos FK nao ficaram evidentes no DbContext para todos esses campos. Ponto pendente de validacao.
- `ResumeFilePath` persiste caminho local absoluto/relativo. Para SaaS, isso nao escala e pode vazar estrutura local.
- `PlatformCredentials` armazena senhas protegidas; precisa revisao de necessidade, retencao e threat model.
- `AgentTraceStep` pode acumular muito JSON e crescer rapidamente; falta politica de retencao/arquivamento.
- `AiUsageLogs` existe, mas custo estimado e confiabilidade dependem do provider.
- Migrations recentes e documentos indicam evolucao rapida do ATS; risco de schema crescer sem consolidacao.

### Pontos pendentes de validacao

- Validar o schema real aplicado no Postgres apos migrations, pois a auditoria foi estatica.
- Validar volume de dados, cardinalidade e performance de consultas em `jobs`, `audit`, `traceSteps` e `resume` com dados reais.
- Validar integridade entre `ApplicationJob.ResumeExportId`, `ResumeExport.FilePath` e existencia fisica do arquivo.
- Validar retencao/limpeza de arquivos em `documents/resumes`, `documents/resume-exports` e `documents/job-screenshots`.

## 9. Diagnostico especifico do modulo ATS

### Objetivo atual do ATS

O modulo ATS tem dois sentidos no projeto:

1. Detectar/plugar ATS externos durante candidaturas (`AtsDetector`, LinkedIn, Greenhouse, Lever, Ashby, Workable, Workday, Gupy, InHire etc.).
2. Gerar curriculos ATS-safe e curriculos direcionados por vaga, com mapa de evidencias e exportacao.

Esses dois sentidos sao relacionados, mas hoje aparecem espalhados entre `apps/agent`, `apps/api` e `apps/web`.

### Fluxo esperado

1. Usuario importa curriculo real.
2. Sistema extrai texto, estrutura secoes e calcula qualidade.
3. Usuario escolhe uma versao base.
4. Usuario informa vaga por texto, URL, screenshot/PDF ou seleciona um `JobLead`.
5. Backend organiza requisitos da vaga.
6. Backend mapeia requisito -> evidencia.
7. Backend gera curriculo direcionado sem inventar experiencia.
8. Usuario revisa score, diff, warnings, unknowns e preview.
9. Usuario exporta DOCX/PDF/TXT.
10. Candidatura usa o arquivo correto.

### Fluxo real percebido

- A maior parte do fluxo esta implementada em API e UI.
- A aba `Curriculo ATS` chama backend-first para organizacao, tailoring e export.
- A fila de vagas permite vincular CV a vaga.
- O agente prefere `ApplicationJob.ResumeFilePath` quando existe.
- Em extension mode, upload de arquivo vira checkpoint humano, nao selecao silenciosa de arquivo.
- A validacao runtime completa com Postgres + API + frontend + LinkedIn/dry-run nao esta comprovada.

### Telas envolvidas

- `Perfil`: importacao e base de dados do candidato.
- `Curriculo ATS`: construcao/edicao/exportacao.
- `Fila de vagas`: vinculo de curriculo e auditoria da candidatura.
- `Execucoes`: badges de CV.
- `Checkpoints`: bloqueios de upload/manual.

### APIs envolvidas

- `/profile/import-resume*`
- `/resume/documents`, `/resume/versions`, `/resume/sections`
- `/job-targets/organize`, `/job-targets/organize-screenshot`
- `/resume/evidence-maps`
- `/resume/tailor`, `/resume/ats-build`
- `/resume/review-answers`, `/resume/review-answers/regenerate`
- `/resume/versions/{id}/exports`, `/resume/exports/{id}/download`
- `/jobs/{id}/resume`, `/application-jobs/{id}/resume`
- agent task/checkpoint/audit endpoints

### Dados/tabelas envolvidas

`CandidateProfiles`, `ResumeImports`, `ResumeDocuments`, `ResumeVersions`, `ResumeSections`, `JobTargets`, `JobRequirements`, `ResumeEvidenceMapRuns`, `ResumeRequirementEvidences`, `ResumeTailoringRuns`, `ResumeReviewAnswers`, `ResumeExports`, `JobLeads`, `ApplicationJobs`, `Checkpoints`, `AgentTraceSteps`.

### Componentes envolvidos

Frontend: `ResumeAtsTab`, `SectionEditorPanel`, `SectionDiffList`, `JobsTab`, `JobDetailCard`, `RunsTab`, `AgentTab`.  
Backend: `ProfileImportService`, `ResumeTextService`, `ResumeOcrService`, `JobTargetService`, `ResumeEvidenceMapService`, `ResumeTailoringService`, `ResumeExportService`, `ApplicationResumePreparationService`, `ApplicationResumeService`.  
Agent: `AtsDetector`, `FormStateMachine`, `FormAuditor`, `ActionPlanner`, `ApplicationAgent`.

### Diagnostico direto

O ATS esta parcialmente organizado no backend, mas desorganizado na experiencia de produto.

O backend tem uma cadeia razoavelmente clara e backend-first. O problema esta na apresentacao, na sobreposicao de conceitos e na falta de validacao fim a fim. A tela ATS concentra muitas responsabilidades e ainda nao deixa obvio quando o usuario esta trabalhando em curriculo base, versao generica ATS, versao por vaga, arquivo exportado ou curriculo vinculado a uma candidatura.

### Problemas principais

- Nome "ATS" e usado para plataforma externa e para curriculo ATS-safe.
- Fluxo de curriculo por vaga e funcional, mas pouco guiado.
- Diff e editor existem, mas precisam de modelo mental mais claro: aceitar/rejeitar/editar deve persistir de forma explicita e auditavel.
- Unknowns geram perguntas, mas a relacao entre resposta, evidencia e nova versao precisa ficar mais transparente.
- Organizacao de vaga por URL usa extracao HTML simples; pode falhar em sites reais.
- Screenshot/PDF de vaga depende de OCR/IA e precisa diagnostico de qualidade visivel.
- Dry-run real com LinkedIn/upload de CV continua pendente.

### O que precisa ser reestruturado

- Separar "Curriculos" de "ATS por vaga" e "Aplicacao em ATS externo".
- Criar fluxo guiado e persistente de versao por vaga.
- Consolidar respostas unknown com evidence map.
- Criar auditoria de curriculo usado por candidatura com link para versao/export.
- Validar runtime com dados reais antes de evoluir templates ou novas automacoes.

## 10. Diagnostico da geracao de PDF

### Como o PDF atual funciona

`ResumeExportService` gera PDF manualmente:

- PDF 1.4 simples.
- Uma coluna.
- Fonte Type1 Helvetica.
- MediaBox A4/Letter-like `[0 0 612 792]`.
- Texto dividido em linhas por numero de caracteres.
- Layouts alteram fonte, margens, line height e quantidade de linhas.
- Reextrai texto com PdfPig para validar parseabilidade.
- `human_review` aumenta margem/fonte e tenta destacar headings.

### Pontos positivos

- Texto nativo e selecionavel.
- Uma coluna, sem imagens, tabelas, headers/footers ou caixas.
- Parseabilidade e comparada com o preview.
- Exporta TXT, DOCX e PDF.
- DOCX tende a ser mais seguro para ATS e e reextraido para validacao.

### Problemas de qualidade visual/profissional

- O PDF nao e tipograficamente profissional; e uma renderizacao textual basica.
- O layout `human_review` nao tem hierarquia refinada suficiente para envio humano competitivo.
- Nao ha controle real de viuvas/orfas, quebras de secao ou agrupamento de experiencia.
- Quebras de pagina sao baseadas em contagem de linhas, nao em blocos semanticamente indivisiveis.
- Nao ha cabecalho visual profissional, resumo com hierarquia clara, spacing refinado ou bullets consistentes por secao.
- O preview visual CSS nao garante fidelidade com o PDF final, pois o PDF e construido por outra logica.

### Problemas de legibilidade e compatibilidade textual

- `ToPdfSafeText` remove acentos e troca caracteres fora de ASCII por `?`. Isso pode destruir nomes, cidades, empresas, tecnologias, portugues e bullets.
- O codigo tem sinais de mojibake em strings. Se esses textos entrarem no curriculo, o PDF pode exportar conteudo degradado.
- Fonte Helvetica WinAnsi limita caracteres.
- O criterio de headings depende de uppercase e comprimento; pode falhar em secoes com acentos ou titulo misto.
- PDF pode ser ATS-safe, mas nao necessariamente "bonito" ou confiavel para recrutador humano.

### Avaliacao final do PDF

O PDF atual e util como exportacao minima ATS-safe, mas fraco como curriculo profissional. Antes de adicionar templates visuais ou features secundarias, o projeto precisa definir um novo padrao de layout baseado em conteudo estruturado, Unicode correto, hierarquia visual clara, validacao de texto reextraido e fidelidade entre preview e arquivo exportado.

Ponto pendente de validacao: abrir os PDFs gerados reais (`apps/api/documents/resume-exports/...`) e comparar visualmente com o preview e com o texto extraido. Esta auditoria nao executou renderizacao visual do PDF.

## 11. Requisitos funcionais

- RF01 — Autenticacao local
  - Descricao: permitir login com usuario/senha e uso de JWT.
  - Estado atual: existe `POST /api/auth/login`.
  - Problema identificado: defaults de dev aparecem no frontend; hardening de producao pendente.
  - Resultado esperado: autenticacao segura por ambiente, sem credenciais default expostas fora de dev.

- RF02 — Perfil do candidato
  - Descricao: cadastrar dados pessoais, links, skills, objetivo, senioridade e curriculo.
  - Estado atual: existe `GET/PUT /profile` e tela `Perfil`.
  - Problema identificado: perfil e importacao de curriculo estao acoplados; dados ainda rasos para CV profissional completo.
  - Resultado esperado: perfil estruturado, validado e separado da biblioteca de curriculos.

- RF03 — Importacao de curriculo
  - Descricao: importar PDF/DOCX/TXT/MD por upload e caminho local dev.
  - Estado atual: implementado com `ProfileImportService`, `ResumeFileService`, `ResumeTextService` e OCR condicional.
  - Problema identificado: validacao com PDFs ruins/escaneados reais pendente; caminho local confunde usuario.
  - Resultado esperado: importacao guiada com relatorio de qualidade, texto extraido e pendencias.

- RF04 — Biblioteca e versoes de curriculo
  - Descricao: manter documentos, versoes e secoes estruturadas.
  - Estado atual: entidades e endpoints existem; editor de secoes existe.
  - Problema identificado: UI esta dentro de `Curriculo ATS`, sem area clara de biblioteca.
  - Resultado esperado: modulo de curriculos separado, com versoes, secoes, status e historico.

- RF05 — Organizacao de vaga alvo
  - Descricao: transformar texto/URL/screenshot/vaga descoberta em requisitos estruturados.
  - Estado atual: `JobTargetService` e endpoints existem.
  - Problema identificado: URL scraping e heuristico; qualidade de OCR/vision precisa ser exposta.
  - Resultado esperado: requisitos com evidencia, confianca, warnings e revisao manual clara.

- RF06 — Mapa de evidencias ATS
  - Descricao: comparar requisitos da vaga com curriculo/perfil.
  - Estado atual: `ResumeEvidenceMapService` classifica proven/adjacent/missing/unknown.
  - Problema identificado: relacao visual entre evidencia, resposta unknown e curriculo final ainda pode ser confusa.
  - Resultado esperado: mapa auditavel requisito por requisito, com fonte e decisao.

- RF07 — Geracao de curriculo por vaga
  - Descricao: criar versao direcionada sem inventar fatos.
  - Estado atual: `ResumeTailoringService` implementado com guardrails.
  - Problema identificado: fluxo de revisao precisa ser mais guiado e persistente.
  - Resultado esperado: versao por vaga com diff, score, warnings, revisao e historico.

- RF08 — Edicao de secoes e diff
  - Descricao: editar secoes e aceitar/rejeitar mudancas.
  - Estado atual: `SectionEditorPanel` e `SectionDiffList` existem.
  - Problema identificado: aceitacao/rejeicao precisa ser mais explicita e auditavel.
  - Resultado esperado: alteracoes com status salvo, responsavel e efeito visivel no export.

- RF09 — Exportacao TXT/DOCX/PDF
  - Descricao: exportar curriculo em formatos parseaveis.
  - Estado atual: implementado.
  - Problema identificado: PDF visual fraco e com risco de perda de acentos/Unicode.
  - Resultado esperado: DOCX/PDF/TXT com texto nativo, Unicode, layout profissional e validacao de parseabilidade.

- RF10 — Vinculo de curriculo com candidatura
  - Descricao: garantir que cada application job use o CV correto.
  - Estado atual: `ApplicationJob` tem `ResumeVersionId`, `ResumeExportId`, `ResumeFilePath`; endpoints existem.
  - Problema identificado: fluxo real com agente/LinkedIn pendente.
  - Resultado esperado: candidatura mostra arquivo usado, export usado e checkpoint se arquivo faltar.

- RF11 — Descoberta e fila de vagas
  - Descricao: descobrir, filtrar, ranquear e enfileirar vagas.
  - Estado atual: existem searches, filters, jobs, runs e automation endpoints.
  - Problema identificado: filtros e politicas estao espalhados entre UI, services e agente.
  - Resultado esperado: politica unica, auditavel e editavel.

- RF12 — Checkpoints e auditoria
  - Descricao: registrar bloqueios, campos, respostas, steps, traces e eventos.
  - Estado atual: entidades e telas existem.
  - Problema identificado: UI muito tecnica e dificil para usuario comum.
  - Resultado esperado: resumo executivo + drilldown tecnico.

- RF13 — Extensao Chrome
  - Descricao: observar paginas e executar comandos atomicos no navegador real.
  - Estado atual: MV3 scaffold funcional com pareamento/heartbeat/comandos.
  - Problema identificado: upload de arquivo exige checkpoint; fluxo completo ainda depende de validacao real.
  - Resultado esperado: extensao com onboarding claro, status confiavel e fallback humano.

- RF14 — Aprendizados do agente
  - Descricao: registrar e revisar padroes aprendidos.
  - Estado atual: existe modulo de learnings.
  - Problema identificado: risco de aprovar automacao sem evidencia suficiente.
  - Resultado esperado: workflow de revisao com evidencia, risco e rollback.

## 12. Requisitos nao funcionais

- RNF01 Performance: refresh global nao deve bloquear telas independentes; queries de audit/trace devem paginar.
- RNF02 Seguranca: JWT key, seed admin, CORS e credenciais devem ser tratados por ambiente.
- RNF03 Privacidade: curriculos, CPF, telefone, arquivos e traces devem ser redigidos e ter retencao definida.
- RNF04 Escalabilidade: arquivos locais em `documents/*` devem migrar para storage abstraido antes de SaaS.
- RNF05 Manutenibilidade: quebrar arquivos monoliticos (`App.tsx`, `Entities.cs`) por dominio.
- RNF06 UX: fluxos criticos devem ser guiados, com estados persistentes e linguagem clara.
- RNF07 Responsividade: tabelas, auditoria, JSON trace e editor ATS devem funcionar em mobile/tablet.
- RNF08 Qualidade dos PDFs: PDF deve preservar Unicode, acentos, ordem de leitura, margens, hierarquia e quebras de pagina.
- RNF09 Compatibilidade ATS: export deve ser uma coluna, texto nativo, secoes padrao, sem tabelas/caixas/imagens para conteudo essencial.
- RNF10 Observabilidade: health deve cobrir banco, OCR, IA, extensao, fila, comandos, agente e armazenamento.
- RNF11 Tratamento de erros: API deve retornar erros padronizados; frontend deve exibir acao recomendada.
- RNF12 Testabilidade: fluxos ATS/PDF devem ter testes de parseabilidade, snapshots de texto e validacao visual/manual definida.
- RNF13 Integridade de dados: versao/export/candidatura devem ter relacoes consistentes e limpeza de arquivos orfaos.
- RNF14 Acessibilidade: botoes, formularios e abas devem ter labels, foco e navegacao por teclado.

## 13. Problemas prioritarios

| Prioridade | Problema | Impacto | Modulo afetado | Recomendacao |
| ---------- | -------- | ------- | -------------- | ------------ |
| Alta | PDF converte caracteres nao ASCII para `?` | Curriculo pode sair com texto degradado e pouco profissional | PDF/ATS | Corrigir pipeline PDF para Unicode e validar acentos antes de evoluir templates |
| Alta | `App.tsx` concentra quase todo o frontend | Manutencao lenta, alto risco de regressao | Frontend | Modularizar por dominio e criar rotas reais |
| Alta | Fluxo ATS sobrecarregado em uma tela | Usuario pode gerar/exportar versao errada ou nao entender evidencias | ATS/UX | Redesenhar fluxo por etapas e persistir estado |
| Alta | Validacao fim a fim com Postgres/API/Web/LinkedIn pendente | Codigo pode estar correto em teste unitario, mas falhar no uso real | Projeto inteiro | Executar piloto local completo antes de novas features |
| Alta | Conceitos de respostas/patterns/unknowns se sobrepoem | Regras duplicadas e respostas inconsistentes | ATS/agente/backend | Consolidar modelo de respostas e fontes de evidencia |
| Alta | Caminhos locais de curriculo/export persistidos | Risco de arquivo ausente, vazamento de paths e baixa portabilidade | Banco/agente | Abstrair storage e validar existencia no momento de uso |
| Media | `Entities.cs` centraliza muitos dominios | Dificulta evolucao do backend | Backend | Separar entidades/DTOs por modulo |
| Media | Agent legacy e extension-first convivem na UI | Confusao operacional | Agente/extensao | Separar modo usuario e modo diagnostico |
| Media | Scraping de URL por regex HTML | Requisitos de vaga podem vir incompletos | ATS/job targets | Usar parser mais robusto e mostrar qualidade da extracao |
| Media | Logs/traces podem crescer sem politica | Performance e armazenamento | Auditoria/banco | Implementar paginacao, retencao e arquivamento |
| Media | Documentos internos de ATS divergem | Perda de clareza sobre status real | Produto/processo | Consolidar documentacao unica de status |
| Baixa | Dashboard usa composicao mais "hero" que operacional | Perda de densidade em uso diario | UX | Simplificar cockpit para operacao recorrente |

## 14. Proposta de reorganizacao por fases

### Fase 1 — Auditoria e limpeza estrutural

Objetivo: estabilizar a base de entendimento antes de novas mudancas.

Entregas:

- Inventario oficial de telas, endpoints, entidades e fluxos.
- Limpeza de artefatos gerados fora do escopo do repo.
- Consolidacao dos documentos de ATS em uma unica fonte atual.
- Lista de pontos pendentes de validacao runtime.
- Decisao sobre nomenclatura: ATS externo vs curriculo ATS.

Criterios de conclusao:

- Documento unico aceito pelo time.
- Nenhuma feature nova em andamento sem referencia ao inventario.
- Pendencias de validacao registradas com responsavel e evidencia esperada.

### Fase 2 — Reorganizacao do frontend

Objetivo: reduzir risco de manutencao e melhorar clareza dos fluxos.

Entregas:

- Quebra de `App.tsx` por modulos.
- Rotas reais ou pelo menos deep links por aba.
- API clients por dominio.
- Estados de loading/error padronizados.
- Separacao de telas: Perfil, Curriculos, ATS por vaga, Jobs, Auditoria, Automacao, Configuracoes.

Criterios de conclusao:

- Cada modulo tem componente/arquivo proprio.
- Refresh global deixa de ser dependencia unica de todas as telas.
- Fluxo ATS fica navegavel e recuperavel.

### Fase 3 — Reorganizacao do backend e banco de dados

Objetivo: consolidar dominios, contratos e integridade.

Entregas:

- Separacao de entidades/DTOs por dominio.
- Erros padronizados.
- Revisao de FKs/indices para resume/export/application job.
- Politica de storage e retencao para arquivos e traces.
- Hardening de auth/CORS/seed por ambiente.

Criterios de conclusao:

- Contratos por modulo documentados.
- Migrations revisadas e aplicadas em ambiente local limpo.
- Consultas principais testadas com dados representativos.

### Fase 4 — Reorganizacao do ATS

Objetivo: tornar o ATS um fluxo confiavel, compreensivel e auditavel.

Entregas:

- Separar "curriculo ATS" de "ATS externo".
- Fluxo por etapas: importar base, organizar vaga, mapear evidencias, gerar, revisar, exportar, vincular.
- Evidence map com explicacao requisito por requisito.
- Unknowns integrados ao modelo de respostas e regeneracao.
- Validacao com vaga real e job lead real.

Criterios de conclusao:

- Usuario consegue explicar qual versao esta usando e por que.
- Toda palavra-chave incluida tem evidencia ou pergunta pendente.
- Fluxo real gera CV e vincula a candidatura sem ambiguidade.

### Fase 5 — Reestruturacao da geracao de PDF

Objetivo: criar exportacao profissional e ATS-safe sem degradar texto.

Entregas:

- Suporte Unicode/acento no PDF.
- Layout semantico baseado em secoes estruturadas.
- Quebra de pagina por blocos.
- Preview fiel ao PDF final ou disclaimer claro.
- Validacao automatica de texto reextraido.
- Um template ATS-safe e um template human-review profissional.

Criterios de conclusao:

- PDF preserva nome, acentos, links, secoes e keywords.
- Texto extraido segue ordem correta.
- Visual e profissional para recrutador humano.
- DOCX/PDF/TXT passam em checklist ATS.

### Fase 6 — Padronizacao visual e experiencia do usuario

Objetivo: tornar a ferramenta mais clara para operacao diaria.

Entregas:

- Design system operacional menos disperso.
- Tabelas e detalhes mais densos e legiveis.
- Estados vazios e erros acionaveis.
- Onboarding: perfil, curriculo, extensao, primeira busca, primeiro CV por vaga.
- Modo tecnico separado do modo usuario.

Criterios de conclusao:

- Usuario novo consegue completar primeiro fluxo sem ler docs tecnicas.
- Alertas bloqueantes sao priorizados.
- UI e responsiva nas telas criticas.

### Fase 7 — Melhorias avancadas e novas funcionalidades

Objetivo: evoluir apos estabilizar a base.

Entregas:

- Conectores ATS adicionais com testes por plataforma.
- Templates visuais adicionais.
- Analise ATS mais sofisticada.
- Observabilidade avancada.
- Automacao server-side/remote quando aplicavel.

Criterios de conclusao:

- Fases 1 a 6 concluidas.
- Fluxo real validado.
- Novas features tem criterio de aceite e teste.

## 15. Proposta de novo padrao para curriculos em PDF

### Estrutura ideal

1. Cabecalho:
   - Nome completo em destaque.
   - Cargo alvo ou area.
   - Cidade/UF, email, telefone, LinkedIn, GitHub/portfolio.
   - Sem icones como conteudo essencial.

2. Resumo profissional:
   - 3 a 5 linhas.
   - Foco em stack, tipo de entrega, senioridade real e contexto.
   - Sem promessas genericas ou palavras-chave soltas.

3. Competencias:
   - Skills tecnicas agrupadas por categoria.
   - Apenas competencias reais.
   - Keywords da vaga somente quando houver evidencia.

4. Experiencia profissional:
   - Empresa, cargo, periodo, local/opcional.
   - Bullets com impacto, contexto e tecnologias.
   - Ordem cronologica reversa.

5. Projetos:
   - Nome, contexto, stack e resultado.
   - Bom para candidatos junior ou com pouca experiencia formal.

6. Formacao:
   - Curso, instituicao, status/ano.

7. Certificacoes:
   - Nome, emissor, ano/validade quando houver.

8. Idiomas:
   - Idioma e nivel honesto.

9. Secoes opcionais:
   - Publicacoes, voluntariado, premios, quando relevantes.

### Regras de layout

- Uma coluna.
- Texto nativo e selecionavel.
- Unicode preservado.
- Margens entre 0,6 e 0,8 polegada.
- Fonte comum: Arial, Calibri, Helvetica equivalente com suporte a acentos.
- Tamanho: 10,5 a 11,5 pt no corpo; 14 a 18 pt no nome.
- Headings claros e padronizados.
- Bullets simples.
- Sem tabelas, caixas de texto, colunas, sidebars, fotos, barras de skill ou icones como informacao.
- Quebra de pagina evitando separar empresa/cargo dos bullets.
- Nome de arquivo: `Nome-Sobrenome-Cargo-Empresa.pdf`.
- Exportar tambem TXT para validar "o que o ATS ve".

## 16. Riscos do projeto se nada for reorganizado

- Bugs aumentam porque frontend e backend concentram responsabilidades demais.
- Manutencao fica mais lenta por arquivos grandes e conceitos sobrepostos.
- A duplicidade entre agent legado, extensao-first e automacao server-side pode gerar decisoes conflitantes.
- Usuario pode se perder entre perfil, curriculo base, versao ATS, versao por vaga, export e candidatura.
- PDFs podem continuar fracos visualmente e degradar texto em portugues.
- ATS pode parecer confiavel na UI, mas falhar no fluxo real de upload/aplicacao.
- Traces e arquivos podem crescer sem controle.
- Novas features podem se apoiar em fundacoes ainda instaveis.
- O custo de reorganizacao aumenta quanto mais telas e regras forem adicionadas sobre a estrutura atual.

## 17. Recomendacoes finais

O primeiro passo deve ser validar e limpar o que ja existe: consolidar documentacao, rodar stack completa com Postgres, importar um curriculo real, gerar uma versao por vaga real, exportar DOCX/PDF/TXT, vincular a uma candidatura e executar dry-run com agente/extensao.

Evite agora adicionar novas features complexas, novos templates visuais, novos conectores ATS ou automacao mais agressiva. Antes disso, corrija a estrutura do frontend, consolide o modelo de respostas/evidencias, melhore a qualidade do PDF e prove o fluxo fim a fim.

Prioridade imediata:

1. Corrigir PDF/Unicode e validar visualmente arquivos reais.
2. Redesenhar o fluxo de Curriculo ATS por etapas.
3. Modularizar o frontend.
4. Revisar integridade de resume/export/application job.
5. Executar piloto local completo com dados reais.

O momento certo de voltar a implementar novas melhorias e depois que o JobPilot conseguir demonstrar, com evidencia, que uma vaga real gera um curriculo correto, exporta um arquivo profissional/parseavel e usa esse arquivo certo em uma candidatura auditada.

## Pontos pendentes de validacao

- Validar stack completa com Docker/Postgres/API/Web ativa.
- Validar runtime real com LinkedIn e upload de curriculo em dry-run.
- Validar visualmente PDFs exportados reais em leitor de PDF.
- Validar texto extraido de PDFs exportados com acentos e caracteres especiais.
- Validar OCR com PDF escaneado/layout ruim real.
- Validar comportamento de URL scraping em sites reais de vagas.
- Validar performance de audit/trace com volume de dados.
- Validar politica de retencao/limpeza de documentos e traces.
