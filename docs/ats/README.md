# Pesquisa e decisao para curriculos ATS

Data da pesquisa: 2026-06-04.

## Regras que guiam os templates

- ATS-friendly nao significa visual generico sem criterio. Significa leitura previsivel por parsers: uma coluna, ordem de leitura de cima para baixo, texto real e secoes reconheciveis.
- Evitar tabelas, colunas, caixas de texto, graficos, icones, fotos, barras laterais, headers/footers com informacao importante e fontes incomuns.
- Usar fonte comum entre 10 e 12 pt, margens entre 0.5 e 1 pol, secoes padrao e datas consistentes.
- PDF deve ser texto pesquisavel, nunca imagem. DOC/DOCX/Word deve manter texto como texto, nao como desenho.
- Uma pagina e preferivel para perfil junior ou inicio de carreira, mas nao deve empobrecer evidencias importantes. Duas paginas podem fazer sentido quando ha experiencia, projetos e certificacoes relevantes.

## Fontes consultadas

- USC Career Center: recomenda estrutura minima, uma pagina como padrao nos EUA, fonte Times New Roman ou Arial 11-12 pt, margens de 1 pol e alerta que ATS nao reconhece bem colunas, graficos, fotos ou fontes elaboradas. https://careers.usc.edu/resources/resume-format-guidelines/
- Indeed: recomenda templates simples com secoes claras e conteudo textual. https://www.indeed.com/career-advice/resumes-cover-letters/ats-resume-template
- Jobscan: reforca que tabelas, colunas e caixas de texto podem baguncar a ordem de leitura. https://www.jobscan.co/blog/resume-tables-columns-ats/
- Microsoft OpenXML: DOCX/Word e uma estrutura de paragrafos e runs em WordprocessingML. Isso confirma que texto estruturado e melhor do que gerar desenho ou PDF manual. https://learn.microsoft.com/en-us/office/open-xml/word/how-to-open-and-add-text-to-a-word-processing-document
- Playwright PDF: o Chromium consegue gerar PDF a partir de HTML/CSS de print, permitindo usar o mesmo template da previa. https://playwright.dev/dotnet/docs/api/class-page#page-pdf

## Decisao tecnica

O JobPilot passa a ter um template HTML/CSS canonico para previa e exportacao. O PDF e gerado imprimindo esse HTML. O Word e exportado como documento HTML compativel com Microsoft Word, com extensao `.doc`, para manter o mesmo template visual da previa.

O modelo recomendado por padrao e `ATS padrao`, nao `ATS compacto`. Compacto existe para tentar caber em uma pagina; padrao preserva mais evidencias reais e ainda continua seguro para ATS.

TXT foi removido da UI porque nao representa uma experiencia de curriculo, e sim um artefato tecnico de debug.
