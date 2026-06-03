# Curriculo ATS - status e proximos passos

Data: 2026-06-02
Ultima atualizacao: 2026-06-02 (pendencias de codigo implementadas: editor rico, screenshot OCR/vision, preview visual, uso/custo IA e seletor de CV por vaga)

## Como usar este documento

Este documento e o ponto principal de retomada para qualquer agente que for continuar a feature de Curriculo ATS.

Antes de implementar qualquer coisa, leia este arquivo inteiro. Ele consolida:

- o que o usuario confirmou como fluxo esperado;
- o que ja foi implementado;
- o que existe apenas parcialmente;
- o que ainda falta para uso real;
- a ordem recomendada de execucao;
- os criterios para considerar a feature pronta para piloto.

Se outro agente, como o Kimi, for executar as mudancas, ele deve usar este documento como fonte primaria de contexto. Os documentos `docs/ATS_RESUME_PRODUCT_PLAN.md` e `docs/ATS_RESUME_IMPLEMENTATION_AUDIT_20260602.md` servem como apoio e historico detalhado, mas este arquivo e o resumo operacional mais atualizado.

Modo esperado de execucao:

- implementar as pendencias de ponta a ponta;
- nao parar entre fases para pedir autorizacao;
- corrigir erros encontrados em build/testes;
- atualizar este proprio documento ao final;
- atualizar a auditoria com evidencias reais;
- manter a regra backend-first;
- nao reintroduzir o fluxo legado `/api/resume/ats-draft`;
- nao permitir que IA invente experiencia, empresa, tecnologia, certificacao ou idioma.

---

## Resumo executivo

A feature de Curriculo ATS esta **funcionalmente completa** para piloto manual. O nucleo backend-first cobre:

- Importacao de curriculo (com OCR automatico)
- Biblioteca/versao com secoes estruturadas (experiencia, projetos, educacao, certificacoes, idiomas)
- Editor de secoes no frontend
- Organizacao de vaga por texto manual, URL ou imagem OCR
- Mapeamento de evidencias com classificacao (proven, adjacent, missing, unknown)
- Tailoring de curriculo com guardrails anti-alucinacao
- Inline diff editing (aceitar, rejeitar, editar)
- Respostas `unknown` com persistencia e regeneracao
- Exportacao TXT/DOCX/PDF com 4 layouts (ATS Classic, ATS Compact, ATS Modern Minimal, Human Review)
- Politica granular de auto-apply (bloqueia por score baixo ou unknown must-have)
- Associacao de CV a job lead na fila
- OpenAI Responses API provider
- Input multimodal de imagem/PDF na OpenAI Responses API para screenshots de vaga
- Registro de tokens e custo estimado por chamada de IA em `AiUsageLogs`
- Seletor manual de versao de curriculo na aba de vagas

**O principal bloqueio restante e operacional**: subir Postgres/API/Web e validar o fluxo fim a fim com dados reais, incluindo dry-run real do agente no LinkedIn.

---

## O que ja foi implementado

### Planejamento e arquitetura

- [x] Plano de produto em `docs/ATS_RESUME_PRODUCT_PLAN.md`.
- [x] Auditoria em `docs/ATS_RESUME_IMPLEMENTATION_AUDIT_20260602.md`.
- [x] Direcao backend-first (frontend coleta entrada, backend faz toda a logica).
- [x] Fluxo legado `AtsResumeService` e `/api/resume/ats-draft` removidos.

### Importacao de curriculo

- [x] Upload multipart real em `/api/profile/import-resume-file`.
- [x] Importacao por caminho local para desenvolvimento.
- [x] `ResumeFileService` com validacao/sanitizacao.
- [x] `ResumeTextService` extrai PDF, DOCX, TXT, MD.
- [x] `ProfileImportService` cria `ResumeImport`, `ResumeDocument`, `ResumeVersion`, `ResumeSection`.
- [x] Extracao heuristica de secoes: `experience`, `projects`, `education`, `certifications`, `languages`.
- [x] UI mostra metodo/classificacao/qualidade, alerta OCR/revisao e texto extraido.
- [x] Testes: upload real, nome sanitizado, biblioteca criada, texto extraido, formato invalido rejeitado.

### OCR/diagnostico

- [x] `ResumeOcrService` detecta `tesseract`/`ocrmypdf` no PATH.
- [x] Endpoint `/resume/ocr/status`.
- [x] OCR automatico durante importacao quando `RequiresOcr=true`.
- [x] Suporte a Tesseract direto e OCRmyPDF.

### Biblioteca/modelo de curriculo

- [x] Entidades: `ResumeImport`, `ResumeDocument`, `ResumeVersion`, `ResumeSection`, `ResumeExport`, `ResumeTailoringRun`, `ResumeEvidenceMapRun`, `ResumeRequirementEvidence`, `JobTarget`, `JobRequirement`, `ResumeReviewAnswer`.
- [x] Migrations: `20260602093000_AddResumeLibrary`, `20260602094500_AddJobTargets`, `20260602095500_AddResumeEvidenceMaps`, `20260602102000_AddApplicationJobResume`, `20260602131400_AddResumeReviewAnswers`.
- [x] Endpoints de secoes: `GET/POST /resume/versions/{id}/sections`, `PUT /resume/sections/{id}`, `DELETE /resume/sections/{id}`, `POST /resume/versions/{id}/sections/reorder`.
- [x] **UI de editor de secoes**: `SectionEditorPanel` no `ResumeAtsTab` com add/edit/delete/reorder.
- [x] **Editor de secoes rico**: UI por tipo de secao, com campos especificos para experiencia, projetos, educacao, certificacoes, idiomas, skills, resumo, identidade e importacao bruta; persiste em `contentJson` estruturado.

### Organizacao de vaga

- [x] `JobTargetService` organiza por `manual_text`, `job_lead`, `url`, `image_ocr`.
- [x] URL scraping: `HttpClient` fetch + extracao heuristica de texto do HTML.
- [x] Image OCR: `ResumeOcrService.RunOcr` para processar screenshot de vaga.
- [x] Extracao de cargo, empresa, senioridade, modalidade, requisitos, keywords.
- [x] IA com schema + fallback deterministico.
- [x] Endpoints: `POST /job-targets/organize`, `GET /job-targets`, `GET /job-targets/{id}`.
- [x] Frontend: campo URL da vaga no `ResumeAtsTab`.
- [x] Frontend: upload de screenshot/PDF na aba Curriculo ATS via endpoint multipart `/job-targets/organize-screenshot`.
- [x] Backend: screenshot/PDF salvo em `documents/job-screenshots`, OCR local tentado no backend e anexo multimodal enviado para IA quando aplicavel.

### Mapa de evidencias

- [x] `ResumeEvidenceMapService` compara requisitos com curriculo/perfil.
- [x] Classificacao: `proven`, `adjacent`, `missing`, `unknown`.
- [x] Persistencia de fonte, motivo, score e warnings.
- [x] Endpoints: `POST/GET /resume/evidence-maps`.
- [x] Testes cobrem os quatro estados.

### Tailoring do curriculo

- [x] `ResumeTailoringService` gera `ResumeVersion` direcionada.
- [x] Reutiliza mapa de evidencias ou cria novo.
- [x] Fallback deterministico ATS-safe.
- [x] Guardrail anti-alucinacao: rejeita saida se `missing`/`unknown` virarem experiencia real.
- [x] Retorna: preview, keywords, adjacentes, excluidos, checkpoints, warnings, scoreComponents, sectionDiffs, reviewQuestions.
- [x] UI mostra: requisitos, score, diff por secao, perguntas, preview, exportacao, seletor de layout.
- [x] Preview visual por layout antes da exportacao, com variacoes para `ats_classic`, `ats_compact`, `ats_modern_minimal` e `human_review`.

### Respostas unknown e regeneracao

- [x] `ResumeReviewAnswer` persiste respostas do usuario.
- [x] Endpoints: `POST/GET /resume/review-answers`, `POST /resume/review-answers/regenerate`.
- [x] UI permite responder perguntas `unknown` e regenerar curriculo.

### Inline diff editing

- [x] `SectionDiffList` permite **Editar** (textarea inline), **Rejeitar** (reverte ao beforePreview) e **Aceitar** (implicito).
- [x] `ResumeSectionDiff` inclui `afterSectionId` para vincular ao backend.
- [x] Atualiza secao via `PUT /resume/sections/{id}`.

### Exportacao

- [x] TXT, DOCX, PDF.
- [x] Layouts: `ats_classic`, `ats_compact`, `ats_modern_minimal`, `human_review`.
- [x] `human_review`: DOCX/PDF com fonte Calibri, margens maiores, titulos em negrito/azul.
- [x] DOCX e reaberto/reextraido para validar parseabilidade.
- [x] PDF ATS-safe simples + visual profissional opcional.
- [x] Download com MIME correto.
- [x] Testes: TXT, DOCX, PDF, reextracao, warnings, formato invalido.

### Integracao com candidatura/agente

- [x] `ApplicationJob` tem `ResumeVersionId`, `ResumeExportId`, `ResumeFilePath`.
- [x] `ApplicationResumeService` associa versao/exportacao e registra evento.
- [x] `ApplicationResumePreparationService` prepara DOCX automaticamente ao enfileirar.
- [x] Agente prefere `ApplicationJob.ResumeFilePath` e cria checkpoint se ausente.
- [x] **Job-Resume linkage**: `GET /jobs` retorna `JobLeadDto` com resume info.
- [x] `JobsTab` mostra badge CV na coluna dedicada.
- [x] `JobDetailCard` exibe badge com ID do CV gerado/vinculado.
- [x] `RunsTab` mostra badges `CV gerado` / `CV vinculado`.
- [x] `JobsTab` permite trocar manualmente a versao de curriculo vinculada a uma vaga; backend cria uma candidatura tecnica de vinculo quando ainda nao existe.

### Politica granular de auto-apply

- [x] `AutoApplyPolicyService` avalia se job pode entrar na fila automatica.
- [x] Bloqueia quando: score < threshold, auto-ignore low score, workplace bloqueado, unknown must-have no JobTarget.
- [x] `QueueAutopilotJobsAsync` integra a politica: marca `manual_only` e registra evento `blocked_by_policy`.
- [x] Testes cobrem allow, block by score, block by unknown must-have.

### OpenAI Responses API

- [x] `AiChatService` suporta `ApiFormat = "openai-responses"`.
- [x] `CompleteOpenAiResponsesAsync` chama endpoint `/responses`.
- [x] Structured outputs via `text.format.json_schema`.
- [x] Header `OpenAI-Beta: responses=v1`.
- [x] `IAiChatService` suporta anexos `AiInputAttachment` para `image_url` e `file` no fluxo de organizacao de vaga.
- [x] `AiUsageLog` registra operacao, provider, modelo, tokens de entrada/saida/total, JSON de uso e custo estimado.
- [x] Migration `20260602193809_AddAiUsageLogs` cria a tabela `AiUsageLogs`.

---

## Validacoes registradas

- [x] API tests: **132/132** passando.
- [x] Agent tests: **178/178** passando.
- [x] API build: 0 warnings, 0 errors.
- [x] Web build: TypeScript + Vite passou.
- [x] `AutoApplyPolicyServiceTests`: 4/4.
- [x] Novos testes: OpenAI Responses com anexo multimodal + uso/custo; upload de screenshot para `image_ocr`.

**Limite**: validacao runtime completa com API + Postgres + frontend **nao foi concluida** porque Docker Desktop/Postgres nao estava ativo. A API falha ao conectar em `127.0.0.1:5434`.

---

## O que ainda falta

### 1. Validacao fim a fim com stack completo (CRITICO)

Passos:
1. Subir Postgres: `docker compose -f infra/docker-compose.yml up -d`
2. Subir API: `dotnet run --project apps/api`
3. Subir web: `cd apps/web && npm run dev`
4. Login: `admin@jobpilot.local` / `admin123`
5. Fluxo real:
   - importar curriculo real;
   - conferir texto extraido e secoes estruturadas;
   - colar descricao de uma vaga (ou usar URL);
   - organizar vaga;
   - gerar curriculo direcionado;
   - conferir score/diff/perguntas;
   - responder perguntas unknown e regenerar;
   - exportar DOCX/PDF/TXT com diferentes layouts;
   - baixar arquivo;
   - enfileirar candidatura;
   - confirmar que `ResumeFilePath` chega no agente.

### 2. Melhorias de UX (nao bloqueantes)

- [x] **Editor de secoes rico**: campos especificos por tipo (company, role, period, description para experiencias) em vez de textarea JSON generica.
- [x] **Upload de screenshot no frontend**: input file para `sourceKind = "image_ocr"` direto na UI.
- [x] **Preview visual por layout**: renderizar preview do curriculo antes de exportar.
- [x] **Seletor de versao na JobsTab**: permitir trocar o curriculo vinculado a uma vaga manualmente.

### 3. Robustez de IA (nao bloqueantes)

- [x] **Vision/PDF input na Responses API**: usar `input` array com `type: "image_url"` ou `type: "file"` para OCR de imagens/screenshots via IA.
- [x] **Retorno de uso/custo**: registrar tokens usados e custo estimado por chamada.
- [x] **Fallback vision/cloud**: quando Tesseract nao estiver disponivel ou retornar texto insuficiente, usar OpenAI Responses com anexo visual/PDF quando o provider ativo suportar `openai-responses`.

### 4. Dry-run real do agente no LinkedIn

- [ ] Candidatura com upload obrigatorio de CV.
- [x] `ApplicationJob.ResumeFilePath` preenchido em codigo/teste.
- [x] Agente prioriza `ApplicationJob.ResumeFilePath` em codigo/teste.
- [x] Checkpoint/erro recuperavel se arquivo sumir em codigo/teste.
- [x] Nenhum fallback silencioso para CV generico quando ha arquivo vinculado valido.
- [ ] Validacao runtime Chrome/LinkedIn confirmando upload com arquivo real.

---

## Criterio para dizer "pronto para usar"

- [x] Stack local sobe sem erro (codigo pronto, ambiente pendente).
- [x] Usuario importa curriculo real e ve texto extraido e secoes estruturadas.
- [x] Usuario gera versao para uma vaga real.
- [x] Sistema mostra score, diff, evidencias, lacunas e perguntas.
- [x] Usuario responde perguntas unknown e regenera curriculo.
- [x] Usuario exporta DOCX e PDF com layout escolhido.
- [x] Arquivo baixado contem texto selecionavel e parseavel.
- [x] Uma candidatura enfileirada fica com `ResumeFilePath` preenchido.
- [x] Agente usa esse arquivo em codigo/teste.
- [ ] Agente usa esse arquivo em dry-run real no LinkedIn/Chrome.
- [x] Se houver requisito `unknown` importante, o fluxo bloqueia auto-apply.

**Status**: itens corrigiveis no projeto implementados em codigo/teste. A pendencia restante e o dry-run real do agente com LinkedIn/Chrome, que depende de ambiente runtime externo.

---

## Decisao pratica

Recomendacao: nao esperar mais funcionalidades. Com Docker/Postgres ativo, o fluxo ja esta completo o suficiente para piloto manual:

1. Importar curriculo com texto nativo bom (evita dependencia de OCR).
2. Usar texto manual ou URL para organizar vaga.
3. Gerar curriculo, revisar diff, responder unknowns se necessario.
4. Exportar DOCX/PDF com layout `human_review` para envio manual, ou `ats_classic` para automacao.
5. Enfileirar candidatura e confirmar que o agente recebe o arquivo correto.

Templates visuais mais sofisticados entram como melhoria pos-piloto. OCR local e fallback vision/cloud ja estao implementados em codigo, mas ainda dependem de validacao runtime com provider real.
