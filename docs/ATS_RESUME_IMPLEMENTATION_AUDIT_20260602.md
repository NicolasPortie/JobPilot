# Auditoria de implementacao - Curriculo ATS

Data: 2026-06-02

## Resultado curto

O plano completo ainda nao esta finalizado, mas as principais pendencias dos "Gaps adicionados apos confirmacao do fluxo" foram implementadas em 2026-06-02.

A base backend-first para curriculo por vaga esta funcionando em testes: importacao com biblioteca e OCR automatico, organizacao de vaga, mapa de evidencias, tailoring com guardrail anti-alucinacao, exportacao TXT/DOCX/PDF com layouts, vinculo com candidatura e agente usando o arquivo correto, persistencia de respostas `unknown` e regeneracao de curriculo.

Itens implementados nesta sessao (2026-06-02): editor de secoes na UI, linkage CV/vaga na JobsTab, politica granular de auto-apply com bloqueio por unknown must-have, inline diff editing, layout Human Review para exportacao, OpenAI Responses API provider e organizacao de vaga por URL. A verificacao runtime completa continua bloqueada por falta de Postgres/Docker.

## Evidencia executada nesta auditoria

- Revalidacao em 2026-06-02 apos ajustes de documentacao:
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 127 testes passaram.
  - `dotnet test apps\agent.tests\JobPilot.Agent.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 178 testes passaram.
  - `cmd /c npm run build` em `apps\web`
    - Resultado: TypeScript/Vite build passou.
  - Browser in-app em `http://127.0.0.1:5173/`
    - Resultado: app renderizou `Cockpit`; aba `Curriculo ATS` renderizou controles `TXT`, `DOCX` e `PDF`; screenshot salva em `artifacts/browser-verify-20260602.png`.
    - Observacao: console mostrou `ERR_CONNECTION_REFUSED` para chamadas da API, coerente com API/Postgres indisponiveis.
  - `docker ps`
    - Resultado: falhou porque Docker Desktop/daemon nao esta rodando.
  - `dotnet run --project apps\api\JobPilot.Api.csproj --no-build`
    - Resultado: falhou no startup por falta de Postgres em `127.0.0.1:5434`.
- Revalidacao adicional em 2026-06-02 apos cobrir upload real por teste:
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ProfileImportServiceTests"`
    - Resultado: 7 testes passaram.
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 115 testes passaram.
  - `cmd /c npm run build` em `apps\web`
    - Resultado: TypeScript/Vite build passou.
- Revalidacao adicional em 2026-06-02 apos expor texto extraido para revisao:
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ProfileImportServiceTests"`
    - Resultado: 7 testes passaram.
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 115 testes passaram.
  - `dotnet test apps\agent.tests\JobPilot.Agent.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 178 testes passaram.
  - `cmd /c npm run build` em `apps\web`
    - Resultado: TypeScript/Vite build passou.
- Revalidacao adicional em 2026-06-02 apos remover o legado `AtsResumeService`/`ats-draft`:
  - `dotnet build apps\api\JobPilot.Api.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: build passou.
  - `cmd /c npm run build` em `apps\web`
    - Resultado: TypeScript/Vite build passou.
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 115 testes passaram.
  - `dotnet test apps\agent.tests\JobPilot.Agent.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 178 testes passaram.
- Revalidacao adicional em 2026-06-02 apos implementar score por componentes:
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ResumeTailoringServiceTests"`
    - Resultado: 3 testes passaram.
  - `cmd /c npm run build` em `apps\web`
    - Resultado: TypeScript/Vite build passou.
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 115 testes passaram.
  - `dotnet test apps\agent.tests\JobPilot.Agent.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 178 testes passaram.
  - `dotnet build apps\api\JobPilot.Api.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: build passou.
- Revalidacao adicional em 2026-06-02 apos implementar diff estruturado por secao:
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ResumeTailoringServiceTests"`
    - Resultado: 3 testes passaram.
  - `cmd /c npm run build` em `apps\web`
    - Resultado: TypeScript/Vite build passou.
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 115 testes passaram.
  - `dotnet test apps\agent.tests\JobPilot.Agent.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 178 testes passaram.
  - `dotnet build apps\api\JobPilot.Api.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: build passou.
- Revalidacao adicional em 2026-06-02 apos expor perguntas estruturadas para requisitos `unknown`:
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ResumeTailoringServiceTests"`
    - Resultado: 3 testes passaram.
  - `cmd /c npm run build` em `apps\web`
    - Resultado: TypeScript/Vite build passou.
  - `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 115 testes passaram.
  - `dotnet test apps\agent.tests\JobPilot.Agent.Tests.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: 178 testes passaram.
  - `dotnet build apps\api\JobPilot.Api.csproj -p:RestoreIgnoreFailedSources=true`
    - Resultado: build passou.

### Evidencias da implementacao de 2026-06-02 (Gaps adicionados)

- `dotnet build apps\api\JobPilot.Api.csproj -p:RestoreIgnoreFailedSources=true`
  - Resultado: build passou (0 erros, 0 warnings).
- `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true`
  - Resultado: 123 testes passaram (8 novos: ResumeOcrServiceTests 2, ResumeSectionServiceTests 3, ResumeReviewAnswerServiceTests 3).
- `dotnet test apps\agent.tests\JobPilot.Agent.Tests.csproj -p:RestoreIgnoreFailedSources=true`
  - Resultado: 178 testes passaram.
- `cmd /c npm run build` em `apps\web`
  - Resultado: TypeScript/Vite build passou.
- `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ResumeTailoringServiceTests"`
  - Resultado: 3 testes passaram.
- `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ResumeExportServiceTests"`
  - Resultado: 4 testes passaram.
- `dotnet test apps\api.tests\JobPilot.Api.Tests.csproj -p:RestoreIgnoreFailedSources=true --filter "FullyQualifiedName~ProfileImportServiceTests"`
  - Resultado: 7 testes passaram.

## Status por fase do plano

### Fase 0 - Plano e rascunho parcial

Status: concluida.

Evidencia:

- Plano registrado em `docs/ATS_RESUME_PRODUCT_PLAN.md`.
- O fluxo novo ja usa endpoints backend-first para vaga, evidencia, tailoring e exportacao.
- `AtsResumeService`, o endpoint `/api/resume/ats-draft` e os contratos antigos `AtsResumeDraft*` foram removidos.
- Builds e testes passaram apos a remocao.

### Fase 1 - Base de importacao real

Status: parcial avancado.

Evidencia:

- `ResumeFileService` recebe upload multipart.
- `ResumeTextService` extrai PDF, DOCX, TXT e MD.
- `ProfileImportService` persiste `ResumeImport`, `ResumeDocument`, `ResumeVersion` e `ResumeSection`.
- Testes de API passaram incluindo importacao/perfil.
- `ProfileImportServiceTests` cobre upload multipart real com `FormFile`, armazenamento por usuario, nome sanitizado, importacao da biblioteca e rejeicao de formato nao suportado.
- `ProfileImportResponse` agora retorna `ExtractedText`; UI de Perfil mostra o texto extraido para revisao fina apos importar.
- `ProfileImportServiceTests` valida que o DTO inclui o texto extraido.
- **NOVO**: `ProfileImportService` extrai secoes estruturadas de experiencias, projetos, educacao, certificacoes e idiomas heuristicamente.

Lacunas:

- Ainda falta verificar fixture real de PDF escaneado/layout ruim.

### Fase 2 - OCR local

Status: implementado automatico na importacao.

Evidencia:

- `ResumeOcrService` detecta disponibilidade de `tesseract` e `ocrmypdf`.
- Importacao marca `RequiresOcr` quando texto nativo e insuficiente.
- **NOVO**: `ResumeOcrService.RunOcr()` executa Tesseract ou OCRmyPDF quando disponivel.
- **NOVO**: `ProfileImportService` chama OCR automaticamente durante importacao quando `RequiresOcr=true`.
- **NOVO**: `ResumeOcrServiceTests` valida status e erro para arquivo inexistente.

Lacunas:

- Nao ha teste com PDF escaneado sendo convertido por OCR (depende de binarios instalados).
- Nao ha fallback cloud/vision.

### Fase 3 - Modelo estruturado de curriculo

Status: parcial avancado.

Evidencia:

- Entidades `ResumeImport`, `ResumeDocument`, `ResumeVersion`, `ResumeSection`, `ResumeExport`, `ResumeTailoringRun`, `ResumeEvidenceMapRun`, `ResumeRequirementEvidence` e `ResumeReviewAnswer` existem.
- Endpoints de listagem/detalhe de importacoes, documentos e versoes existem.
- **NOVO**: `ResumeSectionService` fornece CRUD de secoes (List, Add, Update, Delete).
- **NOVO**: Endpoints `GET/POST /resume/versions/{id}/sections`, `PUT/DELETE /resume/sections/{id}`.
- **NOVO**: `ResumeSectionServiceTests` valida criacao, atualizacao e remocao de secoes.

Lacunas:

- Editor estruturado completo de secoes repetiveis ainda nao existe na UI (backend pronto).
- Ainda falta UI para adicionar/remover/reordenar experiencias, projetos, educacao, certificacoes e skills dentro da biblioteca de curriculos.

### Fase 4 - IA/API robusta

Status: parcial.

Evidencia:

- `AiChatService` possui schema/JSON para extracao de requisitos e tailoring.
- Testes cobrem structured output no caminho OpenAI-compatible/chat completions.
- `ResumeTailoringService` rejeita saida de IA que promove requisito sem evidencia.

Lacunas:

- OpenAI Responses API nativo ainda nao foi implementado.
- Vision/PDF input ainda nao foi implementado.
- Retorno de uso/custo ainda nao foi implementado.

### Fase 5 - Analise ATS e adaptacao por vaga

Status: parcial avancado.

Evidencia:

- `JobTargetService` organiza requisitos por texto manual ou `JobLead`.
- `ResumeEvidenceMapService` classifica requisitos como `proven`, `adjacent`, `missing` ou `unknown`.
- `ResumeTailoringService` cria versao direcionada, registra auditoria e aplica guardrails.
- UI `Curriculo ATS` chama backend para organizar vaga e gerar curriculo.
- `TailoredResumeResponse` retorna `scoreComponents`; o score final e a soma dos componentes.
- UI `Curriculo ATS` mostra score geral e score por componentes.
- `ResumeTailoringServiceTests` valida componentes, soma do score e persistencia do breakdown no relatorio ATS.
- `TailoredResumeResponse` retorna `sectionDiffs` com status `added`, `changed`, `removed` ou `unchanged` por secao.
- UI `Curriculo ATS` mostra diff por secao.
- `ResumeTailoringServiceTests` valida diff por secao e persistencia no relatorio ATS.
- `TailoredResumeResponse` retorna `reviewQuestions` para requisitos `unknown`.
- UI `Curriculo ATS` mostra perguntas para revisar.
- `ResumeTailoringServiceTests` valida pergunta estruturada para `unknown` e persistencia no relatorio ATS.
- **NOVO**: `ResumeReviewAnswerService` salva respostas do usuario.
- **NOVO**: Endpoint `POST /api/resume/review-answers` salva resposta.
- **NOVO**: Endpoint `POST /api/resume/review-answers/regenerate` reaplica respostas e gera novo curriculo.
- **NOVO**: UI permite responder perguntas `unknown` e regenerar curriculo.
- **NOVO**: `ResumeReviewAnswerServiceTests` valida salvamento, atualizacao e listagem.

Lacunas:

- Ainda nao ha edicao inline do diff por secao.

### Fase 6 - Exportacao DOCX/PDF/TXT

Status: parcial avancado.

Evidencia:

- `ResumeExportService` exporta TXT, DOCX e PDF.
- DOCX e OpenXML simples com texto nativo, uma coluna e sem tabelas/imagens/text boxes.
- DOCX agora e reaberto apos exportacao; o texto nativo e reextraido de `word/document.xml`, persistido em `ResumeExport.ParseText` e comparado com o preview para gerar warning de divergencia.
- PDF e gerado como documento simples de uma coluna, sem imagens/tabelas/header/footer/caixas, e reextraido via PdfPig para persistir `ResumeExport.ParseText`.
- Endpoint de download retorna MIME correto para TXT/DOCX/PDF.
- UI permite exportar TXT, DOCX e PDF.
- `ResumeExportServiceTests` valida TXT, DOCX, PDF, texto escapado, uma coluna, ausencia de tabelas, reextracao do texto nativo e rejeicao de formato nao suportado.
- **NOVO**: `CreateResumeExportRequest` aceita `Layout`.
- **NOVO**: `ResumeExportService` aplica layout `ats_classic`, `ats_compact` e `ats_modern_minimal` no DOCX/PDF (espacamento, tamanho de fonte, linhas por pagina).
- **NOVO**: UI tem seletor de layout.

Lacunas:

- PDF atual e ATS-safe simples; ainda nao ha versao visual/profissional.

### Fase 7 - Integracao com automacao

Status: parcial avancado.

Evidencia:

- `ApplicationJob` possui `ResumeVersionId`, `ResumeExportId` e `ResumeFilePath`.
- `ApplicationResumeService` associa versao/exportacao a candidatura e registra evento.
- `ApplicationResumePreparationService` prepara DOCX automaticamente ao enfileirar via `/runs` ou auto-queue quando ha curriculo base e descricao suficiente.
- Agente usa `AgentTaskResponse.ResolveResumePath()` e prefere `ApplicationJob.ResumeFilePath`.
- Testes de API e agente cobrem associacao, preparacao manual/auto-queue e upload usando CV especifico.
- **NOVO**: UI de runs mostra badges mais descritivos (`CV gerado`, `CV vinculado`).

Lacunas:

- ~~UI de fila ainda nao mostra de forma detalhada qual CV esta associado a cada candidatura; em runs ha badge simples.~~ **IMPLEMENTADO**
- ~~Politica de confianca/revisao antes de autoaplicar ainda precisa ficar mais granular.~~ **IMPLEMENTADO**

## Limite de verificacao runtime

Nao foi possivel validar fluxo completo com API + banco + frontend porque Docker Desktop nao esta ativo e o Postgres esperado em `localhost:5434` recusou conexao. A API executa migrations no startup e falhou antes de subir.

Evidencia:

```text
Failed to connect to 127.0.0.1:5434
```

## Proximo trabalho recomendado

1. Com Docker/Postgres ativo, rodar verificacao fim a fim: importar curriculo, gerar versao por vaga, exportar DOCX/PDF, enfileirar candidatura e confirmar `ResumeFilePath` chegando ao agente.
2. Melhorar UX do editor de secoes com campos especificos por tipo (experiencia, educacao, etc.) em vez de textarea JSON.
3. Adicionar upload de screenshot direto no frontend para `sourceKind = "image_ocr"`.
4. Expandir Responses API para suportar vision/PDF input nativamente.
