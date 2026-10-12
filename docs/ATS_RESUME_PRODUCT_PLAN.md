# JobPilot Curriculo ATS com IA - Pesquisa, modelagem e plano

> Status: parcialmente aproveitado. As partes de importacao, evidencia, analise de aderencia, tailoring e exportacao continuam validas; qualquer trecho sobre automacao de candidatura, agente ou extensao esta superseded pela arquitetura manual definida em [`REESTRUTURACAO_PRODUTO.md`](REESTRUTURACAO_PRODUTO.md).

Data: 2026-06-02

## 1. Tese do produto

O JobPilot deve deixar de ser apenas um cockpit de busca/aplicacao e virar tambem a origem confiavel do curriculo do candidato. O fluxo correto e:

1. Usuario importa o curriculo atual em PDF/DOCX/TXT.
2. JobPilot extrai texto nativo; se o arquivo for escaneado ou ruim, usa OCR.
3. IA estrutura o perfil em campos editaveis sem inventar experiencia.
4. Usuario revisa e completa informacoes.
5. Para cada vaga, JobPilot compara requisitos com o perfil/curriculo.
6. IA gera uma versao direcionada, preservando verdade factual.
7. Sistema exporta DOCX e PDF ATS-safe com texto nativo, uma coluna, fontes comuns e secoes padrao.
8. A automacao usa essa versao nas candidaturas.

Isso ataca duas dores reais:

- O candidato manda muitos curriculos e nao sabe se o arquivo sequer foi lido.
- O candidato usa um curriculo generico, sem palavras-chave da vaga, ou um PDF visual que parseia mal.

## 1.1 Regra arquitetural: backend-first

Toda logica de produto deve ficar no backend. O frontend nao deve fazer parsing, scoring, organizacao de vaga, decisao de ATS, OCR, tailoring, validacao de evidencia, escolha de curriculo, exportacao ou politica do agente.

Responsabilidades do frontend:

- coletar entradas do usuario;
- chamar endpoints;
- renderizar estados/diagnosticos retornados pelo backend;
- permitir edicao/revisao de campos;
- disparar acoes explicitas.

Responsabilidades do backend:

- extrair texto de curriculo e vaga;
- classificar qualidade/confianca;
- executar OCR/vision quando configurado;
- chamar IA com schemas;
- estruturar requisitos da vaga;
- mapear evidencias contra perfil/curriculo;
- gerar versoes de curriculo;
- calcular score e warnings;
- exportar DOCX/PDF/TXT;
- decidir checkpoints;
- registrar auditoria;
- entregar DTOs prontos para renderizacao.

Critério de clean code:

- endpoint fino, sem regra de negocio;
- servicos pequenos e nomeados por responsabilidade;
- contratos tipados;
- IA sempre via camada de provider/servico;
- persistencia estruturada para evitar recomputar e evitar retrabalho;
- frontend nunca duplica regra do backend.

## 2. O que concorrentes oferecem

### MeuCV / MeuCV.Pro

Fonte principal: https://www.meucv.pro/

Oferta publica observada:

- Criador de curriculo ATS com IA.
- Importar PDF e preencher campos automaticamente.
- Editor estruturado com identidade, contato, links, resumo, experiencia, formacao, projetos, skills, idiomas e certificacoes.
- Analise de pontuacao ATS.
- Adaptacao do curriculo para vagas especificas.
- Download de PDF profissional.

Outro site relacionado/concorrente, https://meucvpro.com.br/, oferece:

- Teste de compatibilidade enviando CV em PDF.
- Escrita com IA.
- Simulador de entrevista.
- Vagas selecionadas.
- Promessa de otimizacao para Gupy, LinkedIn, Vagas.com, Catho, InfoJobs e Solides.

Outros produtos pesquisados seguem padrao parecido:

- Importar PDF/DOCX/TXT.
- Criar biblioteca de curriculos.
- Adaptar para cada vaga.
- Score em tempo real.
- Exportar PDF/DOCX.
- Cartas de apresentacao.
- Entrevista simulada.
- Algumas plataformas incluem pais/mercado alvo e traducao.

## 3. O que a comunidade e fontes tecnicas dizem sobre ATS

Fontes:

- University of Minnesota Duluth Career Center: https://career.d.umn.edu/students/resume-cover-letter/applicant-tracking-system-ats-tips
- U.S. Department of Labor TAP Employment Workshop guide: https://www.dol.gov/sites/dolgov/files/VETS/files/DOL_EW_ParticipantGuide_6.0_Update1-March2026.pdf
- Jobscan sobre tabelas/colunas: https://www.jobscan.co/blog/resume-tables-columns-ats/
- Reddit / comunidade de curriculos e ATS, com relatos tecnicos e empiricos:
  - https://www.reddit.com/r/ResumesATS/comments/1rkypjj/i_reverseengineered_how_ats_parsing_actually/
  - https://www.reddit.com/r/resumeandjobsearch/comments/1qyro4i/ats_teardown_1_i_submitted_the_same_resume_in_5/
  - https://www.reddit.com/r/resumes/comments/1qkx000/ats_parsing_reality_check_8_resume_formatting/

Consenso pratico:

- ATS primeiro converte arquivo em texto/estrutura pesquisavel.
- DOCX costuma ser mais confiavel para parsing; PDF tambem funciona se for texto nativo e layout simples.
- PDF de Canva/Figma/design pesado pode ter ordem de leitura quebrada.
- PDF escaneado/imagem exige OCR e pode ter baixa qualidade.
- Tabelas, colunas, text boxes, headers/footers, icones e graficos podem ser pulados ou lidos fora de ordem.
- Secoes padrao ajudam: Summary/Profile, Work Experience, Education, Skills, Projects, Certifications.
- Palavras-chave importam, mas devem aparecer em contexto real. Nao deve haver keyword stuffing.
- A versao final precisa ser boa para ATS e para humano.
- Uma verificacao simples e util: extrair/copiar o texto final e mostrar ao usuario "o que o ATS provavelmente ve".

Implicacao para o JobPilot:

- Nao vender promessa absoluta de "passar em qualquer ATS".
- Entregar diagnostico honesto: legibilidade, texto nativo, ordem de leitura, secoes, palavras-chave, lacunas e riscos.
- Gerar DOCX e PDF simples, de uma coluna, com texto real.
- Manter uma versao "parser-safe" e, opcionalmente, uma versao visual depois.

## 4. OCR e importacao de PDF

Fontes:

- Tesseract oficial: https://tesseractocr.org/
- Tesseract input formats: https://tesseract-ocr.github.io/tessdoc/InputFormats.html
- OCRmyPDF docs: https://ocrmypdf.readthedocs.io/
- PdfPig para .NET: https://products.documentprocessing.com/parser/net/pdfpig/
- Docling: https://www.docling.ai/
- Docling PDF pipeline: https://docling-project.github.io/docling/examples/agent_skill/docling-document-intelligence/pipelines/
- Discussao tecnica comunidade sobre PDF/OCR: https://www.reddit.com/r/Python/comments/1srm1h1/pdf_extractor_ocrselectable_text/

Direcao tecnica:

OCR para PDF nao deve ser tratado como uma chamada unica. A pipeline precisa primeiro classificar o arquivo e so entao escolher o mecanismo certo. Erros comuns:

- PDF com texto nativo bom.
- PDF com texto nativo presente, mas ordem de leitura quebrada.
- PDF com texto nativo parcial mais imagens.
- PDF escaneado/imagem sem texto.
- PDF de Canva/Figma com camadas, ligatures e texto fragmentado.
- PDF com colunas/tabelas que confundem ordem de leitura.
- PDF com paginas rotacionadas, baixa resolucao ou contraste ruim.

Pipeline recomendada:

1. Extrair texto nativo primeiro com biblioteca .NET, hoje PdfPig.
2. Medir qualidade da extracao:
   - total de caracteres;
   - numero de paginas com texto;
   - proporcao de letras/numeros;
   - caracteres estranhos e ligatures;
   - palavras coladas;
   - sinais de texto embaralhado;
   - ordem aproximada por coordenadas quando necessario;
   - links extraidos;
   - se o usuario consegue obter nome/email/telefone.
3. Classificar o arquivo:
   - `native_text_ok`: usar texto nativo.
   - `native_text_suspicious`: comparar texto nativo com OCR/vision em paginas amostrais.
   - `image_only`: OCR obrigatorio.
   - `mixed`: OCR apenas nas paginas/regioes sem texto.
   - `low_confidence`: exigir revisao humana antes de preencher perfil.
4. OCR local baseline:
   - OCRmyPDF + Tesseract quando disponivel para criar texto pesquisavel.
   - Tesseract direto apenas depois de renderizar paginas em imagem.
   - Pre-processamento: DPI adequado, deskew, orientacao, contraste/binarizacao e remocao de bordas quando possivel.
5. OCR/layout local avancado:
   - Docling para preservar estrutura, hierarquia, reading order, tabelas e layout.
   - PaddleOCR/RapidOCR/EasyOCR como alternativas quando Tesseract falhar em qualidade.
   - Marker/MinerU ou ferramentas similares podem ser avaliadas para PDF -> Markdown estruturado, principalmente se Docling nao funcionar bem no Windows local.
6. OCR/cloud fallback:
   - Azure AI Document Intelligence, Google Document AI, AWS Textract ou APIs de OCR dedicadas quando precisao for mais importante que custo/localidade.
   - OpenAI vision/PDF input pode ser usado para paginas problematicas e estruturacao, mas nao deve ser o unico mecanismo de OCR.
7. Nunca depender de OCR se o PDF ja tem texto bom.
8. Nunca preencher automaticamente campos criticos quando a confianca for baixa; apresentar o texto e pedir revisao.

Ponto importante: Tesseract nao e um parser de PDF completo; normalmente e preciso converter paginas de PDF para imagem antes, ou usar OCRmyPDF. No Windows local, a estrategia deve detectar disponibilidade de binarios e reportar estado no app.

### Matriz de decisao OCR

| Situacao detectada | Acao primaria | Fallback | Nivel de confianca esperado |
| --- | --- | --- | --- |
| PDF/DOCX com texto nativo limpo | Extrair texto nativo | IA apenas estrutura | Alto |
| PDF com texto nativo curto ou ausente | OCRmyPDF/Tesseract ou Docling OCR | Cloud OCR/vision | Medio a alto |
| PDF com texto nativo mas ordem suspeita | Comparar nativo vs OCR amostral | Docling/vision para reading order | Medio |
| PDF com colunas/tabelas | Docling/layout-aware parser | Cloud Document AI | Medio |
| PDF escaneado com baixa qualidade | Pre-processar + OCR | Cloud OCR/vision | Baixo a medio |
| Manuscrito | Cloud OCR/vision especializado | Revisao manual | Baixo |

### Criterios de confianca

O importador deve calcular e exibir:

- `extractionMethod`: native, ocr_local, ocr_layout, ocr_cloud, vision_ai, mixed.
- `confidence`: 0 a 1.
- `textQualityScore`: 0 a 100.
- `pagesRead`, `pagesWithNativeText`, `pagesWithOcr`.
- `warnings`: texto curto, possivel ordem quebrada, muitos caracteres estranhos, campos essenciais ausentes.
- `requiresReview`: true quando confianca baixa ou campos importantes forem inferidos.

Campos como nome, email, telefone, empresas, datas, cargos e tecnologias devem vir com origem/evidencia. Se a origem nao existir, a IA deve perguntar ao usuario, nao inventar.

## 5. IA/API

Fontes OpenAI:

- Structured Outputs: https://platform.openai.com/docs/guides/structured-outputs
- Images and vision: https://platform.openai.com/docs/guides/images-vision
- PDF/file inputs: https://platform.openai.com/docs/guides/pdf-files
- Responses API reference: https://platform.openai.com/docs/api-reference/responses

Direcao:

- Para extrair/estruturar curriculo, usar saida estruturada por JSON Schema.
- Para adaptar por vaga, usar JSON estruturado com:
  - secoes propostas;
  - mudancas feitas;
  - evidencias de origem;
  - riscos de invencao;
  - palavras-chave adicionadas;
  - palavras-chave que nao devem ser adicionadas por falta de evidencia.
- Para OCR com IA/vision, usar imagens/PDF como entrada apenas se o parser local falhar.
- O backend atual tem `AiChatService` com Ollama, Kimi/Anthropic-style e OpenAI-compatible via chat completions. Para essa feature, precisa evoluir para:
  - suporte explicito a `openai-responses`;
  - structured outputs no provedor OpenAI;
  - maior limite de tokens;
  - retorno de uso/custo quando disponivel;
  - prompts com politica anti-alucinacao;
  - contratos de DTO fortemente tipados.

## 6. Estado atual do JobPilot

Ja existe:

- `ResumeTextService`: extrai texto de PDF/DOCX/TXT/MD usando PdfPig e OpenXML simples.
- `ProfileImportService`: importa curriculo, extrai links, chama IA para estruturar perfil e cria respostas reutilizaveis.
- Biblioteca de curriculos com `ResumeDocument`, `ResumeVersion`, `ResumeSection`, `ResumeImport`, `ResumeExport`, mapa de evidencias e versoes por vaga.
- Upload real de curriculo pela UI de Perfil via multipart em `/api/profile/import-resume-file`, mantendo caminho local como opcao de desenvolvimento.
- Exportacao ATS-safe em TXT, DOCX e PDF com texto nativo e auditoria de parseabilidade.
- Vinculo de curriculo/exportacao com candidatura e agente usando o arquivo especifico da vaga.
- Perfil editavel no frontend.
- Provedores de IA configuraveis.
- Automacao de candidatura que usa perfil/respostas.

Lacunas:

- Nao ha OCR.
- O modelo de secoes ainda e basico; ainda nao ha editor estruturado completo de experiencias, educacao, projetos, certificacoes e idiomas.
- O perfil atual ainda e raso para montar um curriculo completo sem revisao humana.
- A IA atual retorna JSON por prompt, mas nao usa structured outputs robusto para OpenAI.
- Ainda nao ha OpenAI Responses API nativo com vision/PDF input.
- Ainda nao ha edicao inline do diff nem persistencia/uso das respostas `unknown`.
- Ainda falta verificacao runtime fim a fim com Postgres local ativo.

Existiu tambem um rascunho parcial criado em 2026-06-02 com `AtsResumeService` e endpoint `/api/resume/ats-draft`. Esse legado foi removido apos o fluxo backend-first assumir modelos persistidos, evidencias, tailoring, exportacao TXT/DOCX/PDF e integracao com automacao. O fluxo atual ainda nao cobre OCR nem OpenAI Responses/vision/PDF input.

## 7. Requisitos funcionais

### Importacao

- Upload de PDF, DOCX ou TXT pelo navegador.
- Manter tambem opcao de caminho local para desenvolvimento.
- Extrair texto nativo.
- Detectar PDF escaneado ou texto insuficiente.
- Executar OCR quando necessario.
- Gerar relatorio de importacao:
  - metodo usado: texto nativo, OCR local, OCR IA;
  - confianca;
  - paginas lidas;
  - campos extraidos;
  - avisos.
- IA deve preencher campos estruturados:
  - dados pessoais;
  - links;
  - resumo;
  - experiencias;
  - projetos;
  - educacao;
  - certificacoes;
  - idiomas;
  - skills tecnicas, ferramentas e soft skills.

### Editor

- Editor estruturado com secoes repetiveis.
- Preview lado a lado.
- Usuario pode adicionar/remover/reordenar experiencias, projetos, educacao, certificacoes e skills.
- Salvar curriculo base.
- Salvar multiplas versoes.
- Mostrar avisos de campos obrigatorios.

### Analise ATS

- Verificar:
  - texto nativo extraivel;
  - ordem de leitura;
  - tamanho;
  - secoes padrao;
  - contato;
  - links;
  - datas;
  - skills;
  - palavras-chave da vaga;
  - possiveis riscos de layout.
- Mostrar score por componentes, nao apenas numero unico:
  - Parseabilidade;
  - Estrutura;
  - Match com vaga;
  - Qualidade de conteudo;
  - Risco de alucinacao/irrelevancia.

### Adaptacao por vaga

- Entrada: descricao da vaga ou `JobLead`.
- Extrair requisitos da vaga com IA:
  - obrigatorios;
  - desejaveis;
  - tecnologias;
  - senioridade;
  - dominio;
  - responsabilidades.
- Comparar com perfil e historico real.
- Gerar versao direcionada:
  - reorganizar skills;
  - priorizar experiencias/projetos relevantes;
  - reescrever bullets com impacto;
  - inserir palavras-chave somente quando ha evidencia.
- Mostrar diff do que mudou.
- Mostrar "por que esta palavra foi incluida".
- Bloquear sugestoes sem evidencia ou marcar como "adicione se for verdade".

### Exportacao

- Exportar DOCX ATS-safe.
- Exportar PDF ATS-safe gerado a partir do DOCX/HTML simples.
- Gerar texto plano `.txt` para teste de parsing.
- Nome de arquivo limpo: `Nome_Sobrenome_Cargo_Empresa.docx/pdf`.
- Preview do texto extraido do arquivo exportado para validar que o ATS consegue ler.

### Integracao com candidatura

- Ao aplicar para uma vaga, permitir escolher:
  - curriculo base;
  - curriculo adaptado para aquela vaga;
  - gerar novo antes de aplicar.
- Registrar no audit trail qual curriculo foi usado.
- Para Easy Apply/ATS externo, automatizar upload do arquivo certo.

## 8. Modelo de dados proposto

Entidades novas:

- `ResumeDocument`
  - id, userId, title, sourceFileName, sourceKind, baseProfileId, createdAt, updatedAt
- `ResumeVersion`
  - id, resumeDocumentId, userId, title, targetJobLeadId, targetCompany, targetRole, status, atsScore, createdAt, updatedAt
- `ResumeSection`
  - id, resumeVersionId, sectionType, title, order, contentJson
- `ResumeImport`
  - id, userId, sourceFileName, sourcePath, extractionMethod, confidence, rawText, warningsJson, createdAt
- `ResumeExport`
  - id, resumeVersionId, format, filePath, parseText, parseWarningsJson, createdAt
- `JobRequirement`
  - id, jobLeadId, category, label, importance, evidenceText
- `ResumeTailoringRun`
  - id, resumeVersionId, jobLeadId, providerModel, promptVersion, resultJson, warningsJson, createdAt

Status parcial em 2026-06-02:

- Implementadas entidades/tabelas `ResumeImport`, `ResumeDocument`, `ResumeVersion`, `ResumeSection`, `ResumeExport` e `ResumeTailoringRun`.
- Criada migration `20260602093000_AddResumeLibrary`.
- Importacao de curriculo agora persiste:
  - relatorio bruto da extracao em `ResumeImport`;
  - documento base em `ResumeDocument`;
  - versao inicial em `ResumeVersion`;
  - secoes iniciais em `ResumeSection`.
- Criados endpoints:
  - `GET /api/resume/imports`;
  - `GET /api/resume/documents`;
  - `GET /api/resume/versions/{id}`.
- Testes de `ProfileImportServiceTests` validam que a importacao cria biblioteca/versao/secao e que upload multipart salva arquivo por usuario, sanitiza nome e importa a biblioteca.

Secoes estruturadas em JSON:

- identity/contact
- summary
- experience[]
- projects[]
- education[]
- certifications[]
- languages[]
- skills[]

## 9. Arquitetura proposta

Servicos backend:

- `ResumeFileService`
  - recebe upload, salva em storage local seguro, valida extensao/tamanho.
- `ResumeExtractionService`
  - PdfPig, DOCX parser, TXT parser.
- `ResumeOcrService`
  - detecta disponibilidade de Tesseract/OCRmyPDF;
  - renderiza paginas se necessario;
  - retorna texto e confianca.
- `ResumeStructuringService`
  - chama IA com structured outputs;
  - cria secoes estruturadas.
- `JobRequirementExtractionService`
  - extrai requisitos da vaga com IA.
- `ResumeAtsAnalysisService`
  - score e diagnostico.
- `ResumeTailoringService`
  - gera versao por vaga com evidencias.
- `ResumeExportService`
  - DOCX via Open XML SDK;
  - PDF via conversor local ou HTML print service;
  - texto plano de validacao.

## 9.1 Fluxo vaga -> requisitos -> curriculo especifico

Esta e a segunda metade do produto. Um curriculo ATS generico ajuda, mas o ganho real vem de gerar uma versao especifica para cada vaga, preservando verdade factual.

Fontes e sinais da pesquisa:

- Guias de carreira recomendam comecar pela descricao da vaga e incorporar palavras-chave somente quando aplicavel.
- A comunidade relata que tailoring por vaga nao e apenas adicionar keywords; envolve trocar ordem, priorizar bullets e mapear evidencias.
- Layout ATS-safe continua sendo single-column, headings padrao, sem tabelas/text boxes/headers/footers/imagens.
- Em volumes altos, tailoring manual de 20-45 minutos por vaga nao escala; o JobPilot deve automatizar mantendo revisao e auditoria.

### Entrada manual da vaga

O usuario deve poder criar uma "vaga alvo" por:

1. Colar texto completo da vaga.
2. Colar URL da vaga.
3. Enviar imagem/screenshot da vaga.
4. Selecionar uma vaga ja descoberta pelo JobPilot.

Para texto colado:

- IA extrai requisitos diretamente.

Para imagem/screenshot:

- Usar OCR/vision para transformar imagem em texto.
- Como screenshot de vaga costuma ter texto de tela, pode ser mais simples que PDF escaneado, mas ainda precisa de confianca e revisao.
- Se o texto extraido estiver ruim, mostrar preview e pedir correcao antes de gerar curriculo.

Para vaga descoberta automaticamente:

- Usar `JobLead.Title`, `Company`, `Description`, `ApplyUrl`, `ApplyPlatform` e dados observados pelo agente.
- Se a descricao estiver curta, o agente pode coletar mais texto da pagina antes de gerar requisitos.

### `JobRequirementExtractionService`

Entrada:

- texto da vaga;
- origem: manual_text, image_ocr, job_lead, page_observation;
- URL/empresa/cargo quando houver.

Saida estruturada:

- cargo alvo normalizado;
- empresa;
- senioridade detectada;
- modalidade;
- requisitos obrigatorios;
- requisitos desejaveis;
- responsabilidades;
- tecnologias/ferramentas;
- dominio de negocio;
- idiomas;
- certificacoes;
- palavras-chave exatas importantes;
- sinais de alerta: senioridade acima, vaga fora do alvo, stack sem evidencia, presencial/hibrido etc.

Cada requisito deve ter:

- `label`;
- `category`;
- `importance`: must_have, nice_to_have, context;
- `evidenceText`: trecho da vaga que justificou o requisito;
- `normalizedKeyword`;
- `exactPhrases`.

Status parcial em 2026-06-02:

- Implementadas entidades `JobTarget` e `JobRequirement`.
- Criada migration `20260602094500_AddJobTargets`.
- Implementado `JobTargetService` no backend:
  - resolve fonte por texto manual ou `JobLead`;
  - chama IA com schema via `ExtractJobRequirementsAsync`;
  - usa fallback deterministico quando IA falha/nao e usada;
  - normaliza senioridade, modalidade, importancia e categorias;
  - persiste vaga alvo e requisitos.
- Criados endpoints finos:
  - `POST /api/job-targets/organize`;
  - `GET /api/job-targets`;
  - `GET /api/job-targets/{id}`.
- Teste `JobTargetServiceTests` valida persistencia e fallback.
- Frontend ainda nao recebeu logica; a UI futura deve apenas enviar input e renderizar `JobTargetDetail`.

### `ResumeEvidenceMap`

Antes de gerar curriculo, o sistema deve mapear requisito -> evidencia no perfil/curriculo base.

Estados possiveis:

- `proven`: existe evidencia clara no perfil/curriculo/projetos.
- `adjacent`: existe experiencia proxima, mas nao igual.
- `missing`: nao ha evidencia.
- `unknown`: precisa perguntar ao usuario.

Regra critica:

- A IA so pode inserir requisitos `proven`.
- Requisitos `adjacent` podem virar frase honesta, por exemplo "experiencia com APIs REST em .NET" quando a vaga pede "microservices", se isso for plausivel e marcado como adjacente.
- Requisitos `missing` nunca entram no curriculo como se fossem reais.
- Requisitos `unknown` viram pergunta no editor: "Voce tem experiencia real com X?"

Status parcial em 2026-06-02:

- Implementadas entidades `ResumeEvidenceMapRun` e `ResumeRequirementEvidence`.
- Criada migration `20260602095500_AddResumeEvidenceMaps`.
- Implementado `ResumeEvidenceMapService` no backend:
  - compara `JobRequirement` contra `ResumeVersion`, secoes e `CandidateProfile`;
  - classifica cada requisito como `proven`, `adjacent`, `missing` ou `unknown`;
  - persiste evidencia, fonte, motivo, score e warnings;
  - calcula `EvidenceScore` e status `ready`/`needs_review`.
- Criados endpoints finos:
  - `POST /api/resume/evidence-maps`;
  - `GET /api/resume/evidence-maps/{id}`.
- Teste `ResumeEvidenceMapServiceTests` valida `proven`, `adjacent`, `missing` e `unknown`.
- Regra de produto: `ResumeTailoringService` so podera inserir fatos `proven`; `adjacent` exige redacao honesta; `missing` e `unknown` geram lacuna/checkpoint e perguntas estruturadas para revisao.
- Ajustado matcher de evidencias para casar termos normalizados com separadores equivalentes, por exemplo `asp.net-core` contra texto `ASP.NET Core`.

### `ResumeTailoringService`

Entrada:

- `ResumeVersion` base;
- requisitos estruturados da vaga;
- mapa de evidencias;
- template/layout escolhido;
- politica de agressividade: conservative, balanced, aggressive.

Saida:

- nova `ResumeVersion` com `JobLeadId`;
- secoes reordenadas;
- resumo adaptado;
- skills reordenadas por relevancia;
- bullets/projetos priorizados;
- palavras-chave adicionadas em contexto real;
- diff de mudancas;
- warnings de lacunas;
- score por componentes:
  - parseabilidade;
  - match com vaga;
  - evidencia;
  - concisao;
  - risco de alucinacao.

Status parcial em 2026-06-02:

- Implementado `ResumeTailoringService` no backend.
- Criado endpoint fino:
  - `POST /api/resume/tailor`.
- Contrato:
  - `TailorResumeRequest(baseResumeVersionId, jobTargetId, evidenceMapRunId?, layout)`;
  - `TailoredResumeResponse` com nova versao, mapa usado, score, score por componentes, diff por secao, keywords incluidas, adjacentes, excluidas, checkpoints e warnings.
- Comportamento atual:
  - cria uma nova `ResumeVersion` no mesmo `ResumeDocument`;
  - reusa `ResumeEvidenceMapRun` informado ou gera um novo mapa;
  - monta rascunho deterministico ATS-safe como fallback;
  - quando `useAi=true`, chama IA com JSON schema para reescrever o curriculo dentro do contrato de evidencias;
  - rejeita a saida da IA se um requisito `missing`/`unknown` aparecer como fato antes da secao de pendencias;
  - insere competencias `proven` como evidencia direta;
  - coloca `adjacent` em secao separada de competencias relacionadas, sem afirmar experiencia direta;
  - nao insere `missing`/`unknown` como competencia;
  - transforma `missing`, `unknown` e `adjacent must_have` em checkpoints;
  - retorna `scoreComponents` com evidencia direta, cobertura de requisitos, aderencia relacionada, confianca da analise e risco de revisao;
  - retorna `sectionDiffs` com secoes adicionadas, alteradas, removidas ou sem mudanca;
  - retorna `reviewQuestions` para requisitos `unknown`;
  - registra auditoria em `ResumeTailoringRun`.
- UI `Curriculo ATS` mostra score geral, score por componentes, diff por secao e perguntas para revisar.
- Teste `ResumeTailoringServiceTests` valida persistencia da versao direcionada, reuso de mapa, regra anti-alucinacao, score por componentes, diff por secao, pergunta estruturada para `unknown` e rejeicao de texto da IA quando ela tenta promover requisito sem evidencia a experiencia direta.
- Limitacao atual: ainda falta tornar o diff editavel, persistir/usar respostas `unknown` e enriquecer bullets/projetos editaveis. A protecao principal ja existe: o backend monta fallback seguro e valida a saida da IA antes de persistir.

### Fluxo manual esperado na UI

1. Usuario abre `Curriculo ATS`.
2. Escolhe `Nova versao por vaga`.
3. Cola texto, URL ou envia imagem da vaga.
4. Clica `Organizar vaga`.
5. JobPilot mostra:
   - cargo/empresa detectados;
   - requisitos obrigatorios/desejaveis;
   - keywords exatas;
   - lacunas;
   - perguntas ao usuario.
6. Usuario revisa/corrige.
7. Clica `Gerar curriculo para esta vaga`.
8. JobPilot mostra:
   - preview;
   - diff;
   - score;
   - evidencias;
   - botoes DOCX/PDF/TXT.

Status parcial em 2026-06-02:

- A aba `Curriculo ATS` do frontend agora usa o fluxo backend-first:
  - carrega `GET /api/resume/documents`;
  - permite escolher a `ResumeVersion` base;
  - envia a descricao para `POST /api/job-targets/organize`;
  - envia a versao base e a vaga organizada para `POST /api/resume/tailor`;
  - renderiza requisitos estruturados, keywords com evidencia direta, adjacentes, excluidas, checkpoints, warnings e preview.
  - permite exportar a versao gerada em TXT, DOCX e PDF via endpoints de `ResumeExport`.
- A tela nao faz parsing, scoring, evidencia ou tailoring; ela apenas coleta entrada, chama endpoints e renderiza DTOs.
- Limitacao atual: ainda falta persistir/usar respostas `unknown` e edicao inline do diff. TXT, DOCX e PDF ja estao integrados.

### Fluxo automatico do agente

Quando o JobPilot encontra uma vaga elegivel:

1. `JobLead` e criado/atualizado com titulo, empresa, descricao e URL.
2. `JobRequirementExtractionService` estrutura a vaga.
3. `ResumeTailoringService` gera uma `ResumeVersion` especifica para aquele `JobLead`.
4. Se a confianca for alta e a politica permitir:
   - exporta DOCX/PDF;
   - associa a versao ao `ApplicationJob`;
   - agente faz upload desse arquivo.
5. Se houver lacuna/risco:
   - cria checkpoint para revisao humana.
6. Audit trail registra:
   - versao do curriculo usada;
   - requisitos extraidos;
   - evidencias;
   - arquivo enviado;
   - score e warnings.

### Layouts de curriculo

Layouts devem ser renderizacoes do mesmo conteudo estruturado, nao documentos soltos.

Recomendacao inicial:

1. `ATS Classic`
   - uma coluna;
   - headings padrao;
   - sem cor ou quase sem cor;
   - default para aplicacao automatica.
2. `ATS Compact`
   - uma coluna;
   - menor espacamento;
   - ideal para 1 pagina.
3. `ATS Modern Minimal`
   - uma coluna;
   - leve destaque no nome/secao;
   - ainda sem tabelas, icones ou caixas.
4. `Human Review`
   - visual um pouco melhor para envio manual/recrutador;
   - nunca deve ser default para ATS se usar layout complexo.

Evitar como default:

- duas colunas;
- sidebar;
- icones;
- foto;
- graficos/barras;
- tabelas;
- text boxes;
- header/footer com contato;
- elementos que viram imagem.

Critério de aceite dos layouts:

- Exportar DOCX/PDF com texto selecionavel.
- Reextrair texto do arquivo exportado.
- Confirmar ordem: nome -> contato -> resumo -> skills -> experiencia/projetos -> formacao/certificacoes.
- Confirmar que contato e keywords aparecem no texto extraido.
- Confirmar que o arquivo nao depende de imagens para conteudo essencial.

Frontend:

- Aba `Curriculo`
  - Biblioteca, importacoes e versoes.
- Fluxo `Importar`
  - upload, progresso, resultado, revisao.
- Editor estruturado
  - formulario + preview.
- Tela `Adaptar para vaga`
  - selecao de vaga, requisitos, diff, score, versao final.
- Tela `Exportar`
  - DOCX/PDF/TXT, preview de texto parseavel.

## 10. Fases de implementacao

### Fase 0 - Corrigir rascunho parcial

- Remover `AtsResumeService` e endpoint `/api/resume/ats-draft` legado.
- Garantir que o build nao fique quebrado.
- Registrar plano no repo.

Status em 2026-06-02:

- `AtsResumeService`, `/api/resume/ats-draft` e contratos `AtsResumeDraft*` foram removidos.
- Build da API, build web e suites API/agente passaram apos a remocao.

### Fase 1 - Base de importacao real

- Endpoint de upload multipart.
- Persistencia de arquivo importado.
- Extracao PDF/TXT/DOCX.
- Heuristica de qualidade de texto.
- UI de importacao com relatorio.

Aceite:

- Upload do `documents/Curriculo Nicolas Portie.pdf` funciona.
- O app mostra metodo de extracao, texto extraido e campos sugeridos.

Status parcial em 2026-06-02:

- Implementado upload multipart em `/api/profile/import-resume-file`.
- Implementada extracao nativa para PDF, DOCX, TXT e MD em `ResumeTextService`.
- Implementado `ResumeExtractionReport` com metodo, classificacao, score de qualidade, confianca, contagem de paginas/palavras/caracteres, necessidade de OCR e necessidade de revisao.
- UI de Perfil mostra metodo/classificacao/qualidade, alerta `OCR necessario` ou `Revisar` e texto extraido para revisao fina.
- Testes cobrem upload multipart, armazenamento por usuario, importacao da biblioteca, texto extraido no DTO e rejeicao de formato nao suportado.
- Ainda falta cobrir fixtures de PDF escaneado/layout ruim.

### Fase 2 - OCR local

- Detectar Tesseract/OCRmyPDF.
- Se ausente, mostrar aviso e fallback.
- Converter paginas para imagem ou integrar OCRmyPDF quando disponivel.
- Retornar texto OCR e confianca.

Aceite:

- Um PDF escaneado de teste e importado com OCR.
- Se OCR nao estiver instalado, UI explica exatamente o que falta.

### Fase 3 - Modelo estruturado de curriculo

- Migrations e entidades.
- Criar curriculo base a partir da importacao.
- Editor de secoes.
- Salvar versoes.

Aceite:

- Usuario importa, revisa campos, salva curriculo base e reabre depois.

### Fase 4 - IA/API robusta

- Adicionar provedor OpenAI Responses API.
- Structured outputs para:
  - extracao de perfil;
  - requisitos de vaga;
  - tailoring;
  - analise ATS.
- Guardrails:
  - cada afirmacao precisa de evidencia;
  - sugestoes sem evidencia viram perguntas ao usuario;
  - nunca inventar anos, empresas ou tecnologias.

Aceite:

- Com API key configurada, IA gera JSON tipado.
- Sem API key, app continua com parser local e mostra limitacao.

Status parcial em 2026-06-02:

- O caminho `openai-chat-completions`/OpenAI-compatible agora envia `response_format: { type: "json_schema" }` quando o servico recebe um schema.
- `AnalyzeResumeProfileAsync` passa a usar schema real nesse caminho, em vez de apenas pedir JSON no prompt.
- Teste `AiChatServiceTests` valida que o corpo enviado ao provedor contem `response_format.json_schema` com `strict: true`.
- Ainda falta implementar caminho OpenAI Responses API nativo, suporte a vision/PDF input e retorno de uso/custo.

### Fase 5 - Analise ATS e adaptacao por vaga

- Tela de score por componentes.
- Extrair requisitos de uma vaga salva ou texto colado.
- Gerar versao adaptada com diff e evidencias.

Aceite:

- Uma vaga .NET/React gera requisitos.
- O curriculo adaptado prioriza skills reais e aponta lacunas sem inventar.

### Fase 6 - Exportacao DOCX/PDF/TXT

- DOCX com Open XML SDK.
- PDF com texto nativo.
- TXT de validacao.
- Reextrair texto do arquivo exportado e comparar com preview.

Aceite:

- Arquivo exportado tem texto selecionavel.
- Texto reextraido contem nome, contato, secoes e keywords.

Status parcial em 2026-06-02:

- Implementado `ResumeExportService` para exportacao TXT, DOCX e PDF ATS-safe.
- Criados endpoints:
  - `POST /api/resume/versions/{id}/exports`;
  - `GET /api/resume/exports/{id}/download`.
- A exportacao:
  - usa `ResumeVersion.PlainText`;
  - normaliza quebras de linha e excesso de espacos;
  - salva arquivo por usuario em diretorio configuravel;
  - persiste auditoria em `ResumeExport`;
  - no DOCX, reabre `word/document.xml`, reextrai o texto nativo e persiste esse texto como `ParseText`;
  - no PDF, gera documento simples de uma coluna com texto nativo e reextrai o conteudo com PdfPig;
  - compara o texto reextraido do DOCX/PDF com o preview e gera warning se houver divergencia/perda relevante;
  - retorna warnings basicos de parseabilidade.
- DOCX e gerado como documento OpenXML simples:
  - texto nativo em `word/document.xml`;
  - uma coluna;
  - sem tabelas, imagens, text boxes, header/footer com conteudo essencial ou elementos decorativos;
  - fonte comum e margens simples.
- PDF e gerado como documento simples:
  - uma coluna;
  - fonte base Helvetica;
  - sem imagens, tabelas, header/footer ou caixas;
  - texto reextraivel via PdfPig.
- O endpoint de download retorna MIME correto para TXT, DOCX e PDF.
- A aba `Curriculo ATS` permite exportar a versao gerada em TXT, DOCX ou PDF.
- Teste `ResumeExportServiceTests` valida criacao TXT, criacao DOCX, criacao PDF, texto XML escapado, uma coluna, ausencia de tabelas, reextracao de texto nativo do DOCX/PDF, persistencia de auditoria e rejeicao de formato nao suportado.
- Limitacao atual: PDF e ATS-safe simples, nao visual/profissional. DOCX segue como formato principal recomendado para ATS e TXT segue como validacao de texto parseavel.

### Fase 7 - Integracao com automacao

- Associar `ResumeVersion` a `ApplicationJob`.
- Upload automatico usa arquivo certo.
- Audit trail registra versao usada.

Status parcial em 2026-06-02:

- `ApplicationJob` agora possui:
  - `ResumeVersionId`;
  - `ResumeExportId`;
  - `ResumeFilePath`.
- Criada migration `20260602102000_AddApplicationJobResume`.
- Implementado `ApplicationResumeService` para vincular uma versao/exportacao a uma candidatura concreta.
- Criado endpoint:
  - `PATCH /api/application-jobs/{id}/resume`.
- O vinculo valida:
  - candidatura pertence ao usuario;
  - `ResumeVersion` pertence ao usuario;
  - `ResumeExport`, quando informado, pertence a mesma versao;
  - arquivo exportado existe no disco.
- O vinculo registra evento `application.resume_assigned`.
- Como `AgentTaskResponse` ja retorna o `ApplicationJob`, o agente passa a receber `resumeVersionId`, `resumeExportId` e `resumeFilePath` junto da tarefa.
- Teste `ApplicationResumeServiceTests` valida associacao correta e rejeicao de exportacao de outra versao.
- O agente agora prefere `ApplicationJob.ResumeFilePath` ao anexar curriculo em formularios:
  - `ApplicationJob` no contrato do agente inclui `resumeVersionId`, `resumeExportId` e `resumeFilePath`;
  - `AgentTaskResponse.ResolveResumePath()` centraliza a escolha do arquivo;
  - planner, resolver local e executor usam o caminho especifico da candidatura antes do curriculo generico do perfil;
  - se o arquivo especifico nao existir, o agente cria checkpoint em vez de enviar outro arquivo silenciosamente.
- Teste `Camada3_ActionPlannerSafetyTests` valida que o upload obrigatorio de curriculo usa o arquivo exportado da candidatura quando disponivel.
- Implementado `ApplicationResumePreparationService`:
  - ao enfileirar candidaturas manualmente via `/runs` ou automaticamente via auto-queue, tenta preparar curriculo por candidatura;
  - seleciona a versao base mais recente com texto;
  - organiza a vaga a partir do `JobLead`;
  - gera `ResumeVersion` direcionada com `ResumeTailoringService`;
  - exporta DOCX ATS-safe;
  - associa `ResumeVersionId`, `ResumeExportId` e `ResumeFilePath` ao `ApplicationJob`;
  - registra eventos `application.resume_prepared`, `application.resume_prepare_skipped` ou `application.resume_prepare_failed`;
  - nao bloqueia a fila quando falta base, descricao suficiente ou arquivo.
- Testes `ApplicationRunServiceTests` e `AgentTaskQueueServiceTests` validam que vagas enfileiradas manualmente e via auto-queue recebem DOCX associado automaticamente quando ha curriculo base e descricao suficiente.

Aceite:

- Uma candidatura enfileirada mostra qual curriculo sera usado.

## 11. Criterios de qualidade

- Nao inventar experiencia.
- Usuario sempre revisa antes de exportar/aplicar.
- Dados sensiveis ficam locais por padrao.
- IA externa e opcional/configuravel.
- OCR externo/vision so com consentimento implicito via provedor configurado.
- Logs nao devem vazar CPF, telefone completo ou arquivo bruto.
- Testes devem cobrir:
  - extracao;
  - OCR indisponivel;
  - parsing JSON IA;
  - score ATS;
  - export DOCX/PDF;
  - fluxo frontend basico.

## 12. Decisoes recomendadas

1. Manter foco inicial em curriculo base + versao por vaga.
2. Exportar DOCX primeiro, PDF depois, porque DOCX e mais seguro para ATS e mais simples de validar.
3. OCR local primeiro; OpenAI vision apenas fallback premium/configurado.
4. Nao criar template visual complexo nesta fase.
5. Usar OpenAI Responses API + Structured Outputs para o caminho cloud.
6. Manter Ollama como fallback local, mas tratar como menor confiabilidade para JSON complexo.
7. Mostrar evidencia e lacunas ao usuario, nao so score.
