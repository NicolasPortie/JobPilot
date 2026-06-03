# PRD - Auditoria do Estado Atual do Projeto JobPilot V2

Documento gerado a partir de auditoria estatica do repositorio em `C:\Projetos\JobPilot`, com releitura do `goal.md`, do `PRD_AUDITORIA_PROJETO.md` e validacao complementar contra codigo-fonte, migrations, endpoints, frontend, agente, extensao, servicos ATS e geracao/exportacao de curriculos.

Escopo desta revisao:
- Nao houve implementacao de codigo.
- O PRD original foi preservado.
- Esta V2 corrige, aprofunda e reclassifica pontos do PRD anterior quando a leitura do codigo mostrou contrato quebrado, risco subestimado ou afirmacao dependente de runtime.

Legenda de evidencia:
- **Confirmado no codigo**: identificado diretamente em arquivos do repositorio.
- **Parcialmente confirmado**: existe implementacao, mas ha lacuna de contrato, teste, runtime ou integracao.
- **Pendente de validacao runtime**: precisa executar API, web, agente, extensao, banco ou fluxo real.
- **Risco tecnico**: comportamento provavelmente problematico, mesmo sem execucao.

## 1. Visão geral do projeto

O JobPilot e um sistema SaaS/local-first para apoiar candidatos na busca, organizacao e aplicacao a vagas. O produto combina:

- frontend React/Vite para operacao do candidato;
- API ASP.NET Core com Entity Framework Core e PostgreSQL;
- agente .NET com automacao via Playwright ou extensao Chrome;
- extensao Chrome MV3 para interagir com paginas de vagas;
- modulo ATS para importar curriculos, interpretar vagas, mapear evidencias, gerar versoes direcionadas e exportar curriculos;
- exportacao de curriculos em TXT, DOCX e PDF.

Problema que o sistema tenta resolver:
- centralizar perfil, curriculo, buscas, vagas, respostas reutilizaveis, automacao de candidatura e melhoria de curriculo;
- reduzir trabalho manual em plataformas de vaga;
- produzir curriculos mais aderentes a requisitos de vagas e mais compativeis com leitura ATS.

Estado atual percebido:
- O projeto tem base funcional extensa e varios modulos ja codificados.
- A arquitetura esta dividida em `apps/api`, `apps/web`, `apps/agent`, `apps/extension` e `infra`.
- O backend concentra boa parte das regras de negocio.
- O frontend concentra muitas telas e fluxos em um unico `App.tsx`, com alto acoplamento.
- O ATS esta em fase avancada de implementacao estatica, mas ainda apresenta lacunas de contrato, inconsistencias de score, riscos de qualidade de PDF e dependencia de validacao runtime.
- A geracao de PDF e o ponto mais fragil do modulo de curriculo: o PDF e produzido manualmente, com perda de acentos e Unicode, layout basico e validacao que pode deixar passar degradacao visual/textual.

Diagnostico executivo:
- Antes de novas features complexas, o projeto precisa estabilizar contratos, separar responsabilidades, corrigir lacunas do ATS/PDF e criar uma matriz de testes de fluxo completo.
- O risco principal nao e ausencia de funcionalidades; e crescimento sobre uma base com telas concentradas, contratos parcialmente desalinhados e PDF profissional insuficiente.

## 2. Objetivo do PRD

Este PRD serve para:

- mapear o estado atual real do projeto;
- registrar modulos, telas, rotas, APIs, fluxos, entidades e integracoes;
- identificar redundancias, duplicidades, contratos quebrados, regras espalhadas e riscos de arquitetura;
- diagnosticar profundamente o ATS e a geracao de PDF;
- separar o que ja existe, o que precisa ser corrigido e o que pode ser melhorado depois;
- orientar uma reorganizacao em fases antes da evolucao funcional.

Este documento nao deve ser usado como backlog de novas features sofisticadas antes das correcoes estruturais. A prioridade e dar previsibilidade tecnica ao produto.

## 3. Mapa atual do sistema

### 3.1 Estrutura principal

| Area | Caminho | Papel | Status |
|---|---|---|---|
| API | `apps/api` | Backend ASP.NET Core, EF Core, endpoints, regras de negocio | Ativa |
| Web | `apps/web` | SPA React/Vite, telas operacionais e configuracoes | Ativa, mas concentrada |
| Agente | `apps/agent` | Automacao de candidaturas via Playwright/extensao | Ativa, dependente de runtime |
| Extensao | `apps/extension` | Chrome MV3 para observar e operar paginas de vagas | Ativa, com lacunas de cobertura |
| Infra | `infra/docker-compose.yml` | PostgreSQL 16 local | Ativa |
| Docs | `docs` | Planos, auditorias e status do ATS | Parcialmente atualizadas |

### 3.2 Modulos funcionais identificados

| Modulo | Funcionalidades identificadas | Evidencia |
|---|---|---|
| Autenticacao | Login JWT, seed de usuario admin/local | Confirmado no codigo |
| Perfil do candidato | Dados pessoais, cargo alvo, senioridade, skills, validacao, importacao de curriculo | Confirmado no codigo |
| Biblioteca de curriculos | ResumeDocument, ResumeVersion, ResumeSection, ResumeExport | Confirmado no codigo |
| ATS de curriculo | Build generico ATS, tailor por vaga, layouts, review answers, evidence map | Confirmado no codigo, runtime pendente |
| Analise de vaga | JobTarget, requisitos, organizacao por texto, URL, job lead, imagem/OCR/PDF | Confirmado no codigo |
| Busca e vagas | SavedSearch, JobLead, filtros, status, auditoria de vaga | Confirmado no codigo |
| Execucoes/candidaturas | ApplicationRun, ApplicationJob, checkpoints, eventos | Confirmado no codigo |
| Agente inteligente | Politica, historico, traces, learnings, plataforma, aplicacao | Confirmado no codigo |
| Extensao Chrome | Pareamento, heartbeat, comandos, observacao, acao em pagina | Confirmado no codigo |
| Respostas reutilizaveis | ReusableAnswer, draft de resposta com IA | Confirmado no codigo |
| IA | Providers, chat service, usage logs | Confirmado no codigo |
| Credenciais | PlatformCredential, endpoints de credenciais | Confirmado no codigo |
| Saude operacional | Health, recovery, manutencao | Confirmado no codigo |

### 3.3 Fluxos principais

1. Login e carregamento inicial:
   - Web chama `/auth/login`.
   - Depois carrega perfil, buscas, vagas, respostas, execucoes, checkpoints, eventos, providers, politica, historico, filtros, credenciais, health, documentos e layouts.

2. Perfil e importacao de curriculo:
   - Usuario edita perfil ou importa curriculo.
   - API extrai texto de PDF/DOCX/TXT.
   - Quando necessario, tenta OCR.
   - Persiste ResumeImport, ResumeDocument, ResumeVersion e ResumeSections.

3. ATS generico:
   - Usuario aciona build ATS.
   - API cria ResumeVersion sem vaga alvo.
   - Gera secoes normalizadas e score de qualidade.

4. ATS direcionado por vaga:
   - Usuario organiza vaga como JobTarget.
   - API extrai requisitos.
   - EvidenceMap compara requisitos contra perfil/curriculo.
   - Tailoring gera nova ResumeVersion.
   - Usuario pode responder lacunas e regenerar.

5. Exportacao de curriculo:
   - Usuario solicita TXT, DOCX ou PDF.
   - API cria ResumeExport e arquivo em diretorio local.
   - Web baixa via `/resume/exports/{id}/download`.

6. Vaga para candidatura:
   - Usuario vincula curriculo/exportacao a vaga ou ApplicationJob.
   - Agente pode usar ResumeFilePath preferencialmente no ApplicationJob.

7. Automacao:
   - Playwright ou extensao observa pagina, abre superficie de candidatura, preenche campos e gera checkpoints quando exige intervencao.

## 4. Inventário de telas, rotas e fluxos

Observacao: o frontend nao usa roteador client-side formal. As "rotas" abaixo sao tabs internas da SPA renderizada em `/`. A navegacao e controlada por estado em `App.tsx` e pelo menu em `components/layout.tsx`.

### 4.1 Inventario resumido das telas

| Tela | Rota/tela interna | Modulo | Objetivo | APIs/dados usados | Componentes principais | Status | Problemas | Recomendacoes |
|---|---|---|---|---|---|---|---|---|
| Cockpit | `/` tab `dashboard` | Operacao | Visao geral de metricas, execucoes e saude | `/jobs`, `/runs`, `/events`, `/health/operational`, `/extension/status` | `App.tsx`, `MetricCard`, cards de layout | Ativa | Depende de muitos estados globais carregados em lote | Separar dashboard em modulo proprio e reduzir acoplamento com App |
| Fila de vagas | `/` tab `jobs` | Vagas | Listar, auditar e alterar status de vagas | `/jobs`, `/jobs/{id}`, `/jobs/{id}/status`, `/jobs/{id}/resume`, auditoria | `JobsTab`, componentes locais em App | Ativa | Fluxo de curriculo por vaga mistura JobLead e ApplicationJob | Padronizar contrato de candidatura e curriculo anexado |
| Checkpoints/agente | `/` tab `agent` | Agente | Operar pendencias da automacao | `/checkpoints`, `/automation/discover`, `/automation/apply`, `/extension/commands`, `/agent/policy` | `AgentTab`, paineis de checkpoint | Ativa | Multipla responsabilidade: politica, extensao, automacao e pendencias | Separar operacao do agente, politica e status da extensao |
| Execucoes | `/` tab `runs` | Candidaturas | Visualizar execucoes e ApplicationJobs | `/runs`, `/application-jobs/{id}/resume`, eventos | `RunsTab` | Ativa | Ligacao com curriculo exportado precisa validacao runtime | Criar jornada clara: vaga -> versao -> exportacao -> candidatura |
| Aprendizados | `/` tab `learnings` | Agente | Gerenciar aprendizados do agente | `/agent/learnings` | `LearningsTab` | Ativa | Pode estar desconectado da explicabilidade do agente | Relacionar aprendizados com dominio/plataforma e runs |
| Perfil | `/` tab `profile` | Perfil | Editar perfil e importar curriculo | `/profile`, `/profile/import-resume`, `/profile/import-resume-file`, `/profile/validate`, `/resume/ocr/status` | `ProfileTab` | Ativa | Validacao ainda assume campos como `ResumePath`; biblioteca de curriculos cria novo paradigma | Unificar perfil, biblioteca e validacao de curriculo |
| Curriculo ATS | `/` tab `resume` | ATS/PDF | Gerar, revisar, ordenar secoes e exportar curriculo | `/resume/documents`, `/resume/layouts`, `/resume/ats-build`, `/resume/tailor`, `/resume/versions`, `/resume/sections`, `/resume/exports` | `ResumeAtsTab`, preview visual | Ativa com contrato quebrado | Frontend chama endpoint de reorder inexistente no backend; PDF fragil | Corrigir contrato antes de evoluir UX |
| Buscas | `/` tab `searches` | Busca | Gerenciar buscas salvas | `/searches` | `SearchesTab` | Ativa | Dados e filtro podem duplicar responsabilidades com JobFilters | Clarificar diferenca entre busca salva e filtro operacional |
| Respostas | `/` tab `answers` | Respostas/IA | Reutilizar respostas para formularios | `/answers`, `/ai/answer-draft` | `AnswersTab` | Ativa | Regras de matching precisam rastreabilidade com ApplicationAnswers | Conectar resposta reutilizavel, pergunta real e aprendizado |
| Filtros | `/` tab `filters` | Triagem | Configurar filtros de vaga | `/job-filters`, `/jobs/filtered` | `FiltersTab` | Ativa | Filtros, buscas e scoring podem se sobrepor | Definir fronteiras: busca, filtro, score, decisao |
| IA | `/` tab `ai` | Configuracao | Configurar providers de IA | `/ai/providers` | `AiTab` | Ativa | Risco de chaves/segredos e logs sensiveis | Revisar criptografia, mascaramento e logs |
| Credenciais | `/` tab `credentials` | Integracoes | Gerenciar credenciais de plataformas | `/platform-credentials` | `CredentialsTab` | Ativa | Alto risco se armazenamento/mascara nao estiverem robustos | Auditar criptografia, permissao e exposicao no frontend |

### 4.2 Fluxos com maior risco

| Fluxo | Estado atual | Risco | Acao recomendada |
|---|---|---|---|
| Reordenar secoes do curriculo | UI chama `/resume/versions/{id}/sections/reorder`; endpoint nao foi encontrado na API | Funcionalidade quebra em runtime | Implementar ou remover/ocultar acao ate contrato existir |
| Gerar PDF profissional | API gera PDF manual, ASCII, Helvetica, sem Unicode real | Perda de acentos, layout fraco, baixa percepcao profissional | Trocar motor de PDF ou criar pipeline robusto com teste visual/textual |
| Regenerar curriculo apos review answers | Servico recalcula score com formula diferente do EvidenceMapService | Scores inconsistentes e perda de confianca | Centralizar calculo de score |
| InHire via extensao | Agente/background reconhecem InHire, manifest nao inclui `inhire.app` em host_permissions/content_scripts | Extensao pode nao operar nessa plataforma | Alinhar manifest, background, content script e agente |
| OCR de curriculo importado | OCR pode substituir texto, mas metadados de extracao persistidos podem refletir relatorio pre-OCR | Diagnostico de qualidade fica incorreto | Atualizar relatorio efetivo apos OCR |
| Politica do agente | Web/API tem `executionTarget`; DTO do agente nao consome esse campo | Configuracao pode nao afetar runtime do agente | Sincronizar contratos entre web, API e agente |

## 5. Diagnóstico geral do projeto

### 5.1 Pontos fortes

- O dominio principal ja esta materializado em entidades, migrations e endpoints.
- Existe separacao fisica entre web, API, agente e extensao.
- O ATS possui servicos especializados: importacao, extracao, OCR, job target, evidence map, tailoring, sections, exports e review answers.
- Existem testes unitarios relevantes para parte do ATS e exportacao.
- A extensao e o agente indicam preocupacao com automacao segura via checkpoints.

### 5.2 Problemas estruturais

1. **Frontend concentrado demais em `App.tsx`**
   - Muitas telas, estados, chamadas de API e handlers estao agrupados no mesmo arquivo.
   - Isso aumenta risco de regressao e dificulta ownership por modulo.

2. **Contratos frontend/backend nao estao completamente sincronizados**
   - Exemplo confirmado: reorder de secoes chamado pelo frontend sem endpoint backend correspondente.
   - Exemplo parcial: `executionTarget` existe em web/API, mas nao aparece no DTO do agente.

3. **Documentacao do ATS e codigo nao estao no mesmo nivel de comprovacao**
   - Docs afirmam fluxo quase completo para piloto manual.
   - Codigo confirma partes relevantes, mas validacao runtime completa ainda e pendente.

4. **Regras de negocio espalhadas**
   - Score e evidencias aparecem em `ResumeEvidenceMapService`, `ResumeTailoringService` e `ResumeReviewAnswerService`.
   - Perfil, curriculo, validacao e candidatura possuem vinculos indiretos.

5. **PDF e exportacao estao implementados abaixo do padrao esperado para produto profissional**
   - TXT/DOCX sao mais defensaveis para ATS.
   - PDF atual e tecnicamente funcional, mas visual e semanticamente limitado.

6. **Extensao, agente e API possuem matriz de plataforma desalinhada**
   - InHire aparece no agente/background, mas nao esta completamente habilitado no manifest da extensao.
   - LinkedIn tem heuristicas mais ricas que Gupy/Indeed/InHire.

7. **Integridade relacional de curriculo em candidaturas parece incompleta**
   - Ha campos e indices para ResumeVersion/ResumeExport em ApplicationJob, mas a leitura do DbContext nao mostrou relacionamento EF explicito para esses vinculos.
   - Deve ser validado em migration/model snapshot antes de assumir integridade forte.

## 6. Diagnóstico técnico do frontend

### 6.1 Estrutura

Frontend identificado:
- `apps/web/src/App.tsx`
- `apps/web/src/api/client.ts`
- `apps/web/src/types.ts`
- `apps/web/src/components/layout.tsx`
- `apps/web/src/index.css`

Stack:
- React 19
- TypeScript
- Vite
- Axios
- lucide-react

### 6.2 Diagnostico

| Area | Diagnostico | Impacto | Recomendacao |
|---|---|---|---|
| Roteamento | SPA sem router formal, tabs internas | URLs nao representam estados/telas; dificil deep link e QA | Avaliar roteador leve depois da estabilizacao |
| Estado | Estado global concentrado no App | Alto acoplamento entre modulos | Extrair hooks por dominio: profile, resume, jobs, agent |
| Chamadas de API | Muitas chamadas diretas no App | Contratos dificeis de auditar | Criar clients por dominio |
| Componentes | Layout separado, mas telas grandes no App | Baixa reutilizacao e baixa legibilidade | Extrair `features/resume`, `features/jobs`, `features/agent` |
| ATS UI | Preview e controles existem | Funcao critica depende de endpoint ausente para reorder | Corrigir contrato antes de polir visual |
| Erros | Tratamento existe, mas disperso | Mensagens inconsistentes | Padronizar camada de erro/toast |
| Responsividade | CSS amplo e varios grids | Precisa validacao visual real | Testar desktop/mobile com cenarios reais |
| UX | Produto operacional, com muitas areas | Risco de sobrecarga cognitiva | Priorizar fluxos guiados para ATS e candidatura |

### 6.3 Problemas confirmados

- `App.tsx` chama `api.post('/resume/versions/${versionId}/sections/reorder', ...)`.
- Nao foi identificado endpoint correspondente em `ResumeEndpoints.cs`.
- Sidebar lista muitos modulos em uma unica experiencia, sem fronteira clara entre operar, revisar e configurar.
- Tipos do web incluem `ExecutionTarget`, mas o agente nao consome esse campo no DTO auditado.

### 6.4 Recomendacao de organizacao frontend

Estrutura sugerida apos estabilizacao:

```text
apps/web/src/
  api/
    authClient.ts
    profileClient.ts
    resumeClient.ts
    jobsClient.ts
    agentClient.ts
  features/
    dashboard/
    jobs/
    agent/
    resume/
    profile/
    settings/
  components/
    layout/
    ui/
  hooks/
  types/
```

Nao iniciar redesign amplo antes de corrigir contratos ATS/PDF.

## 7. Diagnóstico técnico do backend

### 7.1 Estrutura

Backend identificado:
- ASP.NET Core Minimal API em `apps/api/Program.cs`.
- Endpoints por arquivos em `apps/api/Endpoints`.
- Servicos de dominio em `apps/api/Services`.
- EF Core em `apps/api/Data/JobPilotDbContext.cs`.
- Entidades em `apps/api/Domain/Entities.cs`.
- Migrations em `apps/api/Migrations`.

### 7.2 Diagnostico

| Area | Diagnostico | Impacto | Recomendacao |
|---|---|---|---|
| Program.cs | Registro amplo de servicos e grupos | Crescimento pode dificultar leitura | Manter modularizacao por endpoint/service |
| Autenticacao | JWT com chave fallback dev-only | Inadequado para producao se nao configurado | Exigir secret externo forte fora de dev |
| Validacoes | Existem validacoes em servicos | Nem todas as invariantes parecem centralizadas | Criar validadores por comando/fluxo critico |
| Erros | Retornos misturam `BadRequest`, mensagens e excecoes | UX/API inconsistente | Padronizar erro com codigo e detalhe |
| ATS | Implementacao rica | Regras duplicadas e scores inconsistentes | Centralizar score/evidencia/exportacao |
| Exportacao | Sem dependencia robusta para PDF | Qualidade baixa e risco textual | Substituir motor ou isolar exportador profissional |
| Extensao | Endpoints de pairing/heartbeat/comandos | Pareamento local sem auth usa primeiro usuario | Aceitavel dev, perigoso fora de ambiente local |
| Logs | Existem eventos/traces | Cobertura runtime precisa validacao | Definir eventos obrigatorios por fluxo critico |

### 7.3 APIs principais identificadas

| Grupo | Endpoints principais |
|---|---|
| Auth | `POST /api/auth/login` |
| Profile | `GET/PUT /profile`, `/profile/validate`, `/profile/import-resume`, `/profile/import-resume-file`, `/resume/ocr/status` |
| Resume | `/resume/imports`, `/resume/documents`, `/resume/layouts`, `/resume/versions/{id}`, `/resume/tailor`, `/resume/ats-build`, `/resume/sections`, `/resume/versions/{id}/sections`, `/resume/exports` |
| Review answers | `/resume/review-answers`, `/resume/review-answers/regenerate` |
| Evidence map | `/resume/evidence-maps`, `/resume/evidence-maps/{id}` |
| Job targets | `/job-targets`, `/job-targets/{id}`, `/job-targets/organize`, `/job-targets/organize-screenshot` |
| Jobs | `/jobs`, `/jobs/{id}`, `/jobs/{id}/status`, `/jobs/{id}/resume`, auditoria |
| Runs | `/runs`, `/application-jobs/{id}/resume` |
| Agent | `/agent/policy`, `/agent/history`, `/agent/pair`, `/agent/learnings` |
| Extension | `/extension/status`, `/extension/pair`, `/extension/local-pair`, `/extension/heartbeat`, `/extension/commands` |
| AI | `/ai/providers`, `/ai/answer-draft` |
| Operational health | `/health/operational`, `/health/operational/recover` |

### 7.4 Problemas tecnicos backend

- O backend tem muitos servicos especializados, mas alguns fluxos criticos ainda nao parecem ter contrato fechado com frontend/agente.
- `ResumeReviewAnswerService.RegenerateAsync` recalcula score de evidencias por proporcao simples de `proven / total`, diferente do score ponderado do evidence map.
- `ProfileImportService` pode trocar texto por OCR, mas manter metadados da extracao original, gerando leitura errada de qualidade.
- O endpoint de reorder de secoes usado pelo frontend nao foi encontrado.
- A exportacao PDF revalida texto usando a propria normalizacao ASCII, o que reduz a capacidade de detectar perda de caracteres.

## 8. Diagnóstico do banco de dados

### 8.1 Entidades principais identificadas

| Dominio | Entidades |
|---|---|
| Usuario/perfil | `User`, `CandidateProfile` |
| Curriculo | `ResumeImport`, `ResumeDocument`, `ResumeVersion`, `ResumeSection`, `ResumeExport`, `ResumeTailoringRun`, `ResumeReviewAnswer` |
| Vaga alvo | `JobTarget`, `JobRequirement`, `ResumeEvidenceMapRun`, `ResumeRequirementEvidence` |
| Busca/vagas | `SavedSearch`, `JobLead`, `JobFilter`, `QuestionPattern` |
| Candidatura | `ApplicationRun`, `ApplicationJob`, `ExecutionEvent`, `Checkpoint` |
| Agente | `AgentRun`, `AgentStep`, `AgentTraceStep`, `ApplicationField`, `ApplicationAnswer`, `SiteWorkflowMemory`, `AgentLearning`, `AgentPolicy`, `AgentDevice` |
| Extensao | `BrowserExtensionSession`, `BrowserExtensionCommandJob` |
| IA | `AiProvider`, `AiUsageLog` |
| Credenciais | `PlatformCredential` |
| Respostas | `ReusableAnswer` |

### 8.2 Relacionamentos e indices

Confirmado no DbContext:
- Usuario possui relacoes com perfil, buscas, vagas, respostas, providers, politicas, sessoes de extensao e demais entidades por `UserId`.
- `ResumeDocument` se relaciona com `ResumeVersion`.
- `ResumeVersion` se relaciona com `ResumeSection`.
- `JobTarget` se relaciona com `JobRequirement`.
- `ResumeEvidenceMapRun` se relaciona com `ResumeRequirementEvidence`.
- `ApplicationRun` se relaciona com `ApplicationJob`.
- `ApplicationJob` se relaciona com `JobLead`.

Indices relevantes:
- Email unico em `User`.
- `JobLead` unico por `UserId + Url`.
- `ReusableAnswer` unico por `UserId + QuestionKey`.
- `QuestionPattern` unico por `UserId + PatternKey`.
- `AgentLearning` unico por `UserId + Domain + PatternKey`.
- Indices de curriculo por usuario/documento/versao/exportacao.

### 8.3 Problemas e riscos de dados

| Problema | Evidencia | Impacto | Recomendacao |
|---|---|---|---|
| Vinculo ApplicationJob -> ResumeVersion/ResumeExport parece sem FK explicita | DbContext mostra indices, nao relacionamento claro | Possivel referencia orfa ou contrato fraco | Validar migration/snapshot e adicionar FK se aplicavel |
| Dados de importacao podem ter metadado pre-OCR | `ProfileImportService` usa OCR mas persiste report original | Qualidade exibida pode estar incorreta | Persistir report efetivo pos-OCR |
| Campos JSON amplos | Sections, reports, diffs, score em JSON | Flexivel, mas dificulta consulta e validacao | Definir schema minimo e versionamento |
| Duplicidade conceitual | SavedSearch, JobFilter, scoring, JobTarget | Confusao de fronteiras | Documentar e normalizar responsabilidades |
| Segredos/credenciais | PlatformCredentials e providers | Risco de exposicao | Auditar criptografia, mascaramento e acesso |

## 9. Diagnóstico do ATS

### 9.1 Objetivo esperado do ATS

O ATS deveria permitir:

1. importar um curriculo existente;
2. estruturar o conteudo em secoes editaveis;
3. analisar uma vaga alvo;
4. identificar requisitos obrigatorios, desejaveis e desconhecidos;
5. mapear evidencias reais do candidato contra requisitos;
6. gerar uma versao direcionada sem inventar experiencia;
7. solicitar respostas para lacunas;
8. regenerar com base em novas evidencias;
9. exportar em formatos ATS-safe, especialmente TXT/DOCX e um PDF profissional;
10. vincular a versao/exportacao correta a uma candidatura.

### 9.2 Fluxo real identificado

Fluxo confirmado no codigo:

```text
Resume import
  -> ResumeDocument
  -> ResumeVersion base
  -> ResumeSections

JobTarget organize
  -> JobRequirements
  -> EvidenceMap
  -> Tailored ResumeVersion
  -> Review questions
  -> ResumeExport
  -> ApplicationJob/JobLead resume linkage
```

### 9.3 Componentes ATS principais

| Componente | Papel | Status |
|---|---|---|
| `ResumeTextService` | Extrai texto de PDF/DOCX/TXT | Ativo |
| `ResumeOcrService` | Tenta OCR com `ocrmypdf`/`tesseract` | Ativo, runtime pendente |
| `ProfileImportService` | Importa curriculo e cria estrutura inicial | Ativo, metadados OCR com risco |
| `JobTargetService` | Organiza vaga por texto, URL, lead, imagem/OCR | Ativo |
| `ResumeEvidenceMapService` | Classifica requisitos como proven/adjacent/missing/unknown | Ativo |
| `ResumeTailoringService` | Gera versoes ATS genericas e direcionadas | Ativo |
| `ResumeReviewAnswerService` | Salva respostas e regenera | Ativo, score inconsistente |
| `ResumeSectionService` | CRUD de secoes e rebuild de plain text | Ativo, sem reorder backend encontrado |
| `ResumeExportService` | Exporta TXT/DOCX/PDF | Ativo, PDF fragil |

### 9.4 Diagnostico funcional do ATS

| Area | Diagnostico | Severidade |
|---|---|---|
| Importacao | Boa cobertura de formatos e OCR opcional | Media |
| Estruturacao | Cria secoes heuristicas e raw import | Media |
| Job target | Aceita fontes variadas, mas URL parsing e OCR/AI precisam runtime | Media |
| Evidence map | Boa ideia, mas heuristicas e strings com mojibake em grupos adjacentes devem ser revisadas | Media |
| Tailoring | Tem protecao contra inventar requisitos ausentes em testes | Alta importancia positiva |
| Review answers | Fluxo existe, mas score diverge do motor principal | Alta |
| Edicao de secoes | CRUD existe; reorder parece quebrado no contrato | Alta |
| Exportacao | TXT/DOCX razoaveis; PDF insuficiente para padrao profissional | Critica |
| Vinculo com candidatura | Existe caminho para ResumeVersion/ResumeExport, mas integridade e runtime precisam validacao | Alta |

### 9.5 Redundancias e confusoes

- ATS generico e ATS direcionado compartilham conceitos, mas precisam UX mais clara.
- `Profile.ResumePath`, biblioteca de curriculos e `ResumeExport` convivem como fontes de curriculo.
- JobLead, JobTarget e ApplicationJob formam uma cadeia poderosa, mas ainda pouco evidente para o usuario.
- Evidence score e report aparecem em varios pontos e nao estao centralizados.
- Docs internas dizem "piloto manual" enquanto o codigo ainda mostra gaps que podem quebrar a experiencia.

### 9.6 Reestruturacao recomendada do ATS

Prioridade:

1. Corrigir contratos quebrados:
   - endpoint de reorder;
   - DTO de politica do agente;
   - host permissions da extensao;
   - vinculo de curriculo em ApplicationJob.

2. Centralizar score:
   - um unico servico/fonte para calcular evidencia, cobertura, risco e status.

3. Reorganizar UX:
   - Biblioteca;
   - Curriculo base;
   - Vaga alvo;
   - Mapa de evidencias;
   - Versao direcionada;
   - Exportacao;
   - Vinculo com candidatura.

4. Separar "ATS-safe" de "human review":
   - TXT/DOCX como formatos ATS-safe principais;
   - PDF como versao visual profissional, mas ainda parseavel.

## 10. Diagnóstico da geração de PDF

### 10.1 Estado atual

`ResumeExportService` exporta:

- TXT em UTF-8;
- DOCX como pacote ZIP manual com XML Word;
- PDF manual com sintaxe PDF crua, Helvetica Type1 e WinAnsi/ASCII.

Layouts reconhecidos:
- `ats_reverse_chronological`
- `ats_classic`
- `ats_compact`
- `ats_skills_first`
- `ats_modern_minimal`
- `human_review`

### 10.2 Diagnostico do PDF atual

| Criterio | Avaliacao | Problema |
|---|---|---|
| Formato tecnico | PDF manual funcional | Alto custo de manutencao |
| Fonte | Helvetica Type1 | Visual generico e limitado |
| Unicode/acento | Texto normalizado para ASCII | Perde acentos e pode trocar caracteres por `?` |
| Portugues | Fraco | Nomes, cidades, cargos e competencias podem degradar |
| Layout | Simples, linhas e headings | Pouca hierarquia visual profissional |
| Quebra de pagina | Baseada em contagem de linhas | Pode quebrar secoes em locais ruins |
| Margens | Definidas por layout | Sem refinamento visual por conteudo |
| Tabelas/colunas | Evitadas, bom para ATS | Correto para ATS, mas nao suficiente para PDF humano |
| Validacao | Reextrai com PdfPig e token coverage | Pode nao detectar perda de acentos porque compara texto ja normalizado |
| Testes | Cobrem exportacao basica | Nao cobrem Unicode, acentos, screenshots ou avaliacao visual |

### 10.3 Problema critico de compatibilidade

O PDF usa normalizacao do texto para remover diacriticos e limitar caracteres. Isso significa que:

- `São Paulo` pode virar `Sao Paulo`;
- `experiência` pode virar `experiencia`;
- `ação`, `integração`, `automação`, `gestão` perdem qualidade linguistica;
- caracteres fora de ASCII podem virar `?`;
- a validacao pode passar porque o texto esperado tambem e convertido para forma "PDF-safe".

Conclusao:
- O PDF atual nao deve ser considerado profissional.
- O PDF atual nao deve ser usado como referencia de qualidade do produto.
- TXT/DOCX sao mais confiaveis para ATS neste momento.

### 10.4 Recomendacao tecnica para PDF

Antes de novas funcionalidades secundarias:

1. Definir padrao visual e semantico do curriculo.
2. Substituir ou isolar o motor de PDF:
   - usar biblioteca com suporte real a Unicode, fonte embutida e layout;
   - ou gerar HTML/CSS controlado e renderizar via motor confiavel;
   - ou usar biblioteca PDF dedicada.
3. Criar testes obrigatorios:
   - extracao textual com acentos;
   - snapshot visual;
   - quebras de pagina;
   - nomes longos;
   - secoes vazias;
   - curriculo de 1, 2 e 3 paginas;
   - comparacao ATS-safe sem exigir colunas/tabelas.

## 11. Requisitos funcionais

- **RF01 Login**
  - Descrição: permitir autenticacao do usuario.
  - Estado atual: implementado via `/auth/login`.
  - Problema identificado: chave JWT fallback dev-only precisa governanca de ambiente.
  - Resultado esperado: login seguro com secret obrigatorio fora de desenvolvimento.

- **RF02 Perfil do candidato**
  - Descrição: manter dados pessoais, cargo alvo, senioridade, skills e resumo.
  - Estado atual: implementado.
  - Problema identificado: perfil, ResumePath e biblioteca de curriculos ainda se sobrepoem.
  - Resultado esperado: fonte de verdade clara para curriculo base.

- **RF03 Importacao de curriculo**
  - Descrição: importar PDF/DOCX/TXT e criar documento/versionamento.
  - Estado atual: implementado.
  - Problema identificado: metadados podem nao refletir OCR efetivo.
  - Resultado esperado: relatorio de extracao final correto.

- **RF04 Biblioteca de curriculos**
  - Descrição: listar documentos, versoes, secoes e exportacoes.
  - Estado atual: implementado.
  - Problema identificado: UX e contratos de edicao precisam estabilizacao.
  - Resultado esperado: biblioteca previsivel e rastreavel.

- **RF05 Curriculo ATS generico**
  - Descrição: gerar versao ATS-safe sem vaga alvo.
  - Estado atual: implementado.
  - Problema identificado: validacao runtime e qualidade visual pendentes.
  - Resultado esperado: versao base confiavel e editavel.

- **RF06 Organizar vaga alvo**
  - Descrição: transformar texto, URL, lead ou screenshot em requisitos.
  - Estado atual: implementado.
  - Problema identificado: URL parsing e OCR/AI dependem de runtime e podem ser frageis.
  - Resultado esperado: requisitos auditaveis e editaveis.

- **RF07 Mapa de evidencias**
  - Descrição: classificar requisitos como comprovados, adjacentes, ausentes ou desconhecidos.
  - Estado atual: implementado.
  - Problema identificado: heuristicas e termos adjacentes precisam revisao; score precisa centralizacao.
  - Resultado esperado: classificacao consistente e explicavel.

- **RF08 Curriculo direcionado**
  - Descrição: gerar versao adaptada a vaga sem inventar experiencia.
  - Estado atual: implementado com testes relevantes.
  - Problema identificado: depende da qualidade do evidence map e dos textos importados.
  - Resultado esperado: versao direcionada confiavel e revisavel.

- **RF09 Review answers**
  - Descrição: coletar respostas para lacunas e regenerar curriculo.
  - Estado atual: implementado.
  - Problema identificado: recalculo de score diverge do motor principal.
  - Resultado esperado: regeneracao com score consistente.

- **RF10 Edicao de secoes**
  - Descrição: editar, adicionar, remover e reordenar secoes do curriculo.
  - Estado atual: CRUD existe; reorder chamado pelo frontend nao foi encontrado no backend.
  - Problema identificado: contrato quebrado.
  - Resultado esperado: reorder implementado/testado ou removido da UI.

- **RF11 Exportacao TXT/DOCX/PDF**
  - Descrição: exportar curriculos em formatos utilizaveis.
  - Estado atual: implementado.
  - Problema identificado: PDF perde qualidade textual/visual.
  - Resultado esperado: TXT/DOCX ATS-safe e PDF profissional com Unicode.

- **RF12 Vinculo de curriculo a vaga/candidatura**
  - Descrição: associar ResumeVersion/ResumeExport a JobLead/ApplicationJob.
  - Estado atual: parcialmente implementado.
  - Problema identificado: integridade relacional e fluxo UX precisam validacao.
  - Resultado esperado: candidatura sempre usa arquivo/versao correta.

- **RF13 Automacao de candidatura**
  - Descrição: agente deve descobrir campos, preencher formularios e gerar checkpoints.
  - Estado atual: implementado parcialmente via Playwright/extensao.
  - Problema identificado: cobertura por plataforma e upload manual variam.
  - Resultado esperado: matriz clara por plataforma/driver.

- **RF14 Extensao Chrome**
  - Descrição: parear, observar paginas e executar comandos.
  - Estado atual: implementado.
  - Problema identificado: InHire esta desalinhado entre agente/background/manifest.
  - Resultado esperado: manifest e suportes alinhados.

- **RF15 Respostas reutilizaveis**
  - Descrição: reaproveitar respostas para formularios.
  - Estado atual: implementado.
  - Problema identificado: rastreabilidade com ApplicationAnswers precisa clareza.
  - Resultado esperado: pergunta real, resposta usada e aprendizado vinculados.

- **RF16 Saude operacional**
  - Descrição: monitorar extensao, API, agente e recuperacao.
  - Estado atual: implementado parcialmente.
  - Problema identificado: criterios de saude por fluxo nao estao formalizados.
  - Resultado esperado: health checks acionaveis por modulo.

## 12. Requisitos não funcionais

| Categoria | Requisito | Estado atual | Resultado esperado |
|---|---|---|---|
| Performance | Carregamento inicial nao deve depender de chamadas excessivas sequenciais | Parcialmente pendente | Carregamento por modulo e cache |
| Seguranca | JWT, credenciais, providers e extensao devem usar secrets fortes e acesso restrito | Parcial | Sem fallback inseguro em producao |
| Privacidade | Dados de curriculo e credenciais devem ser protegidos | Parcial | Criptografia, mascaramento e logs redigidos |
| Manutenibilidade | Frontend deve ser modular | Baixa no App atual | Features separadas por dominio |
| Escalabilidade | Regras de ATS devem ser centralizadas | Parcial | Score/evidencia como dominio unico |
| UX | Fluxos devem ser claros e reduzir carga cognitiva | Parcial | Jornada guiada para ATS e candidatura |
| Responsividade | Telas devem funcionar em desktop/mobile | Pendente de validacao | Testes visuais por viewport |
| PDF | PDF deve preservar acentos, hierarquia e leitura profissional | Nao atendido | Unicode, fonte embutida, layout revisado |
| ATS compatibility | Curriculos devem ser parseaveis sem tabelas/colunas complexas | Parcial | TXT/DOCX/PDF testados em extracao |
| Observabilidade | Runs, checkpoints, traces e erros devem ser rastreaveis | Parcial | Eventos obrigatorios por fluxo |
| Integridade | Vaga/curriculo/exportacao/candidatura devem ter vinculo consistente | Parcial | FKs ou validacoes fortes |
| Testabilidade | Fluxos criticos devem ter testes unitarios e integrados | Parcial | Matriz ATS/PDF/agente/extensao |

## 13. Problemas prioritários

| Prioridade | Problema | Impacto | Módulo afetado | Recomendação |
|---|---|---|---|---|
| P0 | PDF perde acentos/Unicode e tem layout manual basico | Curriculo parece menos profissional e pode degradar dados | PDF/ATS | Redesenhar motor/padrao de PDF antes de evoluir features |
| P0 | Frontend chama reorder de secoes sem endpoint backend identificado | Quebra runtime em fluxo ATS | ATS frontend/API | Corrigir contrato ou remover acao |
| P0 | Score de review answers diverge do evidence map | Usuario pode receber diagnostico inconsistente | ATS | Centralizar calculo de score |
| P1 | InHire desalinhado entre agente/background/manifest | Plataforma pode parecer suportada mas falhar | Extensao/agente | Alinhar host permissions, content scripts e matriz de suporte |
| P1 | Politica `executionTarget` nao aparece no DTO do agente | Configuracao pode nao governar runtime real | Agente/API/web | Sincronizar contrato |
| P1 | App frontend concentra muitas telas e regras | Manutencao dificil e regressao provavel | Frontend | Extrair features e clients |
| P1 | OCR pode gerar metadados inconsistentes | Usuario/API podem confiar em relatorio errado | Importacao ATS | Persistir relatorio pos-OCR |
| P1 | Vinculos de curriculo em ApplicationJob parecem sem FK explicita | Risco de inconsistencia de dados | Banco/candidatura | Validar e fortalecer integridade |
| P2 | URL parsing de vagas e heuristicas podem ser frageis | Requisitos ruins geram curriculo ruim | JobTarget | Criar validacao/edicao dos requisitos |
| P2 | Docs ATS misturam status implementado e status planejado | Decisao de produto pode ser baseada em informacao otimista | Produto/docs | Atualizar docs apos auditoria runtime |
| P2 | CSS e UX do ATS ainda dependem de validacao visual | Risco de interface confusa | Frontend/UX | Teste visual e simplificacao de jornada |
| P3 | Melhorias avancadas de IA e automacao podem ampliar complexidade | Crescimento desorganizado | Produto | Postergar ate estabilizar base |

## 14. Plano de reorganização por fases

### Fase 1  Auditoria e limpeza estrutural

Objetivo:
- Fechar inventario real, contratos quebrados e riscos P0/P1 antes de novas melhorias.

Entregas:
- Matriz de endpoints web/API/agente/extensao.
- Lista de rotas/tabs e donos por modulo.
- Confirmacao runtime dos fluxos ATS principais.
- Confirmacao do contrato de reorder.
- Confirmacao de integridade ResumeVersion/ResumeExport/ApplicationJob.
- Atualizacao dos docs ATS com status real.

Criterios de conclusao:
- Nenhuma tela chama endpoint inexistente.
- Docs distinguem implementado, testado e pendente.
- P0s mapeados com owner e decisao tecnica.

### Fase 2  Reorganização do frontend

Objetivo:
- Reduzir acoplamento de `App.tsx` e melhorar manutencao.

Entregas:
- Extracao de clients por dominio.
- Extracao das telas ATS, jobs, agent, profile e settings.
- Hooks dedicados para dados de resume, jobs e agent.
- Padrao unico de erro/loading/empty state.

Criterios de conclusao:
- `App.tsx` vira orquestrador leve.
- Cada feature tem API client, tipos e componentes proprios.
- Regressao visual minima validada.

### Fase 3  Reorganização do backend e banco de dados

Objetivo:
- Fortalecer contratos, integridade e regras centrais.

Entregas:
- Validadores por fluxo critico.
- Score ATS centralizado.
- Revisao de FKs e constraints.
- Padrao de erros.
- Revisao de secrets, credenciais e CORS/extensao.

Criterios de conclusao:
- Score nao diverge entre services.
- Integridade de candidatura/curriculo esta clara.
- Erros sao previsiveis para o frontend.

### Fase 4  Reorganização do ATS

Objetivo:
- Tornar o ATS confiavel, explicavel e operacionalmente claro.

Entregas:
- Jornada ATS redesenhada em etapas.
- Evidence map revisado.
- Requisitos editaveis/confirmaveis.
- Review answers com score consistente.
- Contrato de secoes completo, incluindo reorder.

Criterios de conclusao:
- Usuario entende por que cada requisito foi classificado.
- Curriculo direcionado nao inventa evidencias.
- Versao final e rastreavel ate vaga e evidencias.

### Fase 5  Reestruturação da geração de PDF

Objetivo:
- Substituir o PDF tecnico basico por curriculo profissional e ATS-compativel.

Entregas:
- Novo padrao visual.
- Suporte real a Unicode/acento.
- Fonte embutida.
- Controle de quebras de pagina.
- Testes com extracao textual e validacao visual.
- Comparacao TXT/DOCX/PDF.

Criterios de conclusao:
- PDF preserva nomes, acentos e secoes.
- PDF tem aparencia profissional.
- PDF e parseavel por extrator textual.
- Casos de 1, 2 e 3 paginas aprovados.

### Fase 6  Padronização visual e UX

Objetivo:
- Tornar o produto mais claro, consistente e operacional.

Entregas:
- Design system leve.
- Hierarquia de informacao por modulo.
- Estados vazios e erros padronizados.
- Fluxos guiados para ATS e candidatura.
- Revisao responsiva.

Criterios de conclusao:
- Usuario sabe proxima acao em cada modulo.
- ATS deixa de parecer conjunto de ferramentas soltas.
- Telas principais funcionam em desktop/mobile.

### Fase 7  Melhorias avançadas

Objetivo:
- Evoluir IA, automacao e produto depois da estabilizacao.

Entregas possiveis:
- Melhor ranking de vagas.
- Melhor suporte multi-plataforma.
- Mais modelos de curriculo.
- Analytics de candidatura.
- IA mais explicavel.

Criterios de conclusao:
- So iniciar apos P0/P1 resolvidos.
- Cada melhoria deve ter contrato, teste e UX definidos.

## 15. Novo padrão recomendado para currículos em PDF

### 15.1 Principios

- Uma coluna.
- Texto selecionavel.
- Unicode preservado.
- Sem tabelas complexas.
- Sem icones decorativos que atrapalhem parser.
- Hierarquia clara.
- Margens consistentes.
- Maximo de 2 paginas para maioria dos candidatos; 3 apenas quando senioridade justificar.
- PDF bonito para humano, mas sem sacrificar ATS.

### 15.2 Estrutura ideal

1. Cabecalho
   - Nome completo em destaque.
   - Cargo alvo.
   - Cidade/UF, telefone, email, LinkedIn/GitHub/portfolio quando relevante.

2. Resumo profissional
   - 3 a 5 linhas.
   - Foco em senioridade, dominio, tecnologias e impacto.

3. Competencias principais
   - Lista curta, agrupada por categoria quando fizer sentido.
   - Evitar nuvem de palavras-chave.

4. Experiencia profissional
   - Empresa, cargo, periodo e local/remoto.
   - Bullets com acao, contexto, tecnologia e resultado.
   - Ordem cronologica reversa.

5. Projetos relevantes
   - Apenas quando agregarem evidencia para a vaga.
   - Nome, contexto, stack e resultado.

6. Formacao
   - Curso, instituicao, periodo/conclusao.

7. Certificacoes
   - Nome, emissor, ano.

8. Idiomas
   - Idioma e nivel.

9. Informacoes adicionais
   - Apenas se relevantes para a vaga.

### 15.3 Regras de layout

- Fonte profissional com suporte a acentos.
- Tamanho base entre 10 e 11 pt.
- Nome entre 18 e 24 pt.
- Headings entre 11 e 13 pt.
- Espacamento consistente entre secoes.
- Bullets simples.
- Sem colunas laterais para versao ATS-safe.
- Quebra de pagina evitando separar titulo de secao do primeiro item.

### 15.4 Regras ATS

- Titulos de secoes padronizados: Resumo, Competencias, Experiencia, Projetos, Formacao, Certificacoes, Idiomas.
- Datas em formato consistente.
- Nao usar imagens para texto.
- Links como texto visivel.
- Evitar headers/footers com conteudo critico.
- Garantir extracao textual automatizada em teste.

## 16. Riscos se nada for reorganizado

- Bugs de contrato vao aparecer em fluxo critico de ATS, especialmente edicao/reordenacao.
- PDF pode prejudicar a percepcao de qualidade do produto mesmo quando o ATS textual funciona.
- Usuarios podem confiar em scores inconsistentes.
- Automacao pode prometer suporte a plataformas que a extensao nao cobre corretamente.
- O frontend ficara cada vez mais dificil de manter.
- Novas features de IA aumentarao o acoplamento em vez de resolver o problema principal.
- Dados de candidatura e curriculo podem ficar inconsistentes.
- Docs otimistas podem esconder gaps de runtime.
- A experiencia do usuario pode parecer poderosa, mas confusa e pouco previsivel.

## 17. Recomendações finais

Fazer primeiro:
- Corrigir ou desabilitar reorder de secoes ate existir endpoint backend.
- Redefinir estrategia de PDF com Unicode e layout profissional.
- Centralizar score/evidence map.
- Validar runtime do fluxo completo: importar curriculo -> gerar ATS -> organizar vaga -> tailor -> responder lacunas -> exportar -> vincular a candidatura.
- Alinhar extensao/agente/API para plataformas suportadas.

Evitar agora:
- Novos templates sofisticados de curriculo antes de corrigir PDF base.
- Novas features complexas de IA antes de estabilizar score e evidencias.
- Expansao de plataformas antes de fechar matriz de suporte real.
- Redesign amplo antes de corrigir contratos quebrados.

Priorizar:
1. ATS e PDF.
2. Contratos frontend/backend/agente/extensao.
3. Integridade banco/candidatura/curriculo.
4. Modularizacao frontend.
5. UX visual e melhorias avancadas.

Quando voltar a implementar melhorias:
- Depois que os P0/P1 estiverem resolvidos.
- Depois que os fluxos criticos tiverem testes e evidencias runtime.
- Depois que o PDF produzido for aceitavel como curriculo profissional real.

## Alterações realizadas nesta revisão

### Correcoes realizadas em relacao ao PRD original

- Reclassificado o ATS de "quase completo" para "implementacao estatica avancada com validacao runtime pendente".
- Corrigido o diagnostico do PDF: o problema nao e apenas visual; ha perda tecnica de Unicode/acento.
- Registrado que a validacao atual do PDF pode nao detectar a perda de caracteres porque compara contra texto tambem normalizado.
- Adicionado desalinhamento confirmado entre frontend e backend: chamada de reorder de secoes sem endpoint encontrado.
- Adicionado desalinhamento entre web/API/agente na politica `executionTarget`.
- Adicionado desalinhamento da extensao para InHire: suporte aparece em agente/background, mas manifest auditado nao inclui cobertura equivalente.
- Reclassificada a geracao de DOCX como mais defensavel que o PDF para ATS, embora ainda minimalista.

### Pontos aprofundados

- Fluxo ATS completo foi quebrado em importacao, job target, evidence map, tailoring, review answers, exportacao e candidatura.
- Banco de dados foi revisado por entidades, relacionamentos e riscos de integridade.
- O modulo de extensao/agente foi incluido no diagnostico de produto, nao apenas como detalhe tecnico.
- A geracao de PDF foi avaliada por fonte, encoding, pagina, layout, extracao e testes.
- A diferenca entre TXT/DOCX/PDF foi explicitada.

### Assumptions removidas ou reclassificadas

- Nao foi assumido que os docs internos provam conclusao do ATS; eles foram tratados como contexto, nao como evidencia runtime.
- Nao foi assumido que toda chamada do frontend possui endpoint correspondente.
- Nao foi assumido que suporte de plataforma no agente equivale a suporte na extensao.
- Nao foi assumido que score ATS e consistente em todos os servicos.

### Novos riscos identificados

- Endpoint de reorder ausente.
- Score divergente em regeneracao por review answers.
- Perda de acentos em PDF.
- Testes de PDF insuficientes para curriculos em portugues.
- Metadados de extracao potencialmente inconsistentes apos OCR.
- Politica do agente possivelmente nao aplicada ao runtime por contrato incompleto.
- Integridade de ResumeVersion/ResumeExport em ApplicationJob precisa confirmacao.
- Matriz de suporte de plataformas da extensao precisa alinhamento.

### Pontos dependentes de runtime

- Executar API e web para validar telas reais.
- Testar importacao PDF/DOCX/TXT com curriculos reais.
- Testar OCR com `ocrmypdf` e `tesseract` instalados.
- Testar geracao visual de PDF em curriculos com acentos e nomes longos.
- Testar fluxo de tailor com vaga real.
- Testar review answers e regeneracao.
- Testar extensao no Chrome em LinkedIn, Gupy, Indeed e InHire.
- Testar agente em modo Playwright e modo extension.
- Testar vinculo de ResumeExport em ApplicationJob e uso pelo agente.
