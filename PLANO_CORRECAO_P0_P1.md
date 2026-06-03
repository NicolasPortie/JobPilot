# Plano de Correção P0/P1  JobPilot

## 1. Resumo executivo

Antes de novas features, o JobPilot precisa corrigir os problemas P0 e P1 identificados no `PRD_AUDITORIA_PROJETO_V2.md`. O risco central não é falta de funcionalidade, mas instabilidade de contratos, inconsistência de regras ATS, baixa qualidade do PDF e desalinhamento entre Web, API, Agent e Extensão.

Correções que devem bloquear novas melhorias:

- PDF de currículo perde acentos/Unicode e tem layout manual insuficiente para um currículo profissional.
- A tela de Currículo ATS chama reorder de seções, mas a API não expõe o endpoint correspondente.
- O score após review answers diverge do cálculo do EvidenceMap.
- InHire aparece como suportado no agente/background, mas a extensão não está alinhada no manifest.
- `executionTarget` existe em Web/API, mas não está refletido no DTO do Agent.
- `App.tsx` concentra telas, estado e chamadas de API, aumentando risco de regressão.
- OCR pode trocar texto final sem atualizar corretamente os metadados persistidos.
- O vínculo ResumeVersion/ResumeExport/ApplicationJob precisa ser fortalecido e validado.

Regra de execução: corrigir primeiro contratos e regras de domínio; só depois reorganizar frontend e melhorar UX visual.

## 2. Lista priorizada de correções

| Ordem | Prioridade | Problema | Módulo | Arquivos envolvidos | Resultado esperado |
|---|---|---|---|---|---|
| 1 | P0 | Endpoint de reorder de seções chamado pelo frontend e ausente no backend | ATS frontend/API | `apps/web/src/App.tsx`, `apps/api/Endpoints/ResumeEndpoints.cs`, `apps/api/Services/ResumeSectionService.cs`, `apps/api.Tests/ResumeSectionServiceTests.cs` | Reorder funcionando com contrato testado ou ação removida da UI |
| 2 | P0 | Score de review answers diverge do EvidenceMap | ATS | `apps/api/Services/ResumeEvidenceMapService.cs`, `apps/api/Services/ResumeReviewAnswerService.cs`, `apps/api/Services/ResumeTailoringService.cs`, `apps/api.Tests/ResumeReviewAnswerServiceTests.cs`, `apps/api.Tests/ResumeEvidenceMapServiceTests.cs` | Um único cálculo de score usado por EvidenceMap, ReviewAnswers e Tailoring |
| 3 | P0 | PDF perde acentos/Unicode e tem layout manual básico | PDF/ATS | `apps/api/Services/ResumeExportService.cs`, `apps/api/JobPilot.Api.csproj`, `apps/api.Tests/ResumeExportServiceTests.cs`, `apps/web/src/index.css`, `apps/web/src/App.tsx` | PDF com Unicode preservado, layout profissional e extração textual validada |
| 4 | P1 | Vínculo ResumeVersion/ResumeExport/ApplicationJob precisa validação e integridade | Banco/candidatura/agente | `apps/api/Domain/Entities.cs`, `apps/api/Data/JobPilotDbContext.cs`, `apps/api/Services/ApplicationResumeService.cs`, `apps/api/Services/ApplicationRunService.cs`, `apps/api/Services/AgentTaskQueueService.cs`, `apps/agent/Domain/Dtos.cs`, `apps/agent/Application/ApplicationAgent.cs`, `apps/api.Tests/ApplicationResumeServiceTests.cs`, `apps/api.Tests/ApplicationRunServiceTests.cs`, `apps/api.Tests/AgentTaskQueueServiceTests.cs` | Candidatura sempre usa versão/exportação válida e arquivo correto |
| 5 | P1 | `executionTarget` desalinhado entre Web/API/Agent | Agent/API/Web | `apps/web/src/types.ts`, `apps/api/Domain/Entities.cs`, `apps/api/Services/AutomationProcessService.cs`, `apps/api/Services/OperationalHealthService.cs`, `apps/agent/Domain/Dtos.cs`, `apps/agent/Application/ApplicationAgent.cs`, `apps/api.Tests/AgentPolicyTests.cs`, `apps/api.Tests/AutomationProcessServiceTests.cs` | Política de execução governa efetivamente extension/local browser/remoto/server |
| 6 | P1 | InHire desalinhado entre agente, background e manifest da extensão | Extensão/agente | `apps/extension/manifest.json`, `apps/extension/background.js`, `apps/extension/content.js`, `apps/agent/Platforms/Inhire/*`, `apps/agent/Application/AtsDetector.cs`, `apps/agent/Application/ApplicationAgent.cs`, `apps/api/Services/JobFilterService.cs` | Matriz de suporte real por plataforma sem promessa falsa |
| 7 | P1 | OCR pode gerar metadados inconsistentes | Importação ATS | `apps/api/Services/ProfileImportService.cs`, `apps/api/Services/ResumeTextService.cs`, `apps/api/Services/ResumeOcrService.cs`, `apps/api.Tests/ProfileImportServiceTests.cs`, `apps/api.Tests/ResumeOcrServiceTests.cs` | ResumeImport, ResumeDocument e ResumeVersion refletem texto e relatório finais pós-OCR |
| 8 | P1 | App frontend concentra muitas telas e regras | Frontend | `apps/web/src/App.tsx`, `apps/web/src/api/client.ts`, `apps/web/src/types.ts`, `apps/web/src/components/layout.tsx`, `apps/web/src/index.css` | Plano de modularização com fronteiras por feature sem refatoração prematura |

## 3. Correções P0

### P0.1  Endpoint de reorder de seções chamado pelo frontend e ausente no backend

- Diagnóstico:
  - A tela de Currículo ATS chama `api.post('/resume/versions/${versionId}/sections/reorder', { sectionIds })`.
  - Em `ResumeEndpoints.cs`, foram identificados endpoints para listar, criar, atualizar e remover seções, mas não para reordenar.
  - `ResumeSectionService` faz CRUD e reconstrói `PlainText`, mas não expõe método de reorder.

- Causa provável:
  - A UI foi evoluída com suporte a reordenação antes de o contrato backend ser implementado.
  - Não há teste de contrato cobrindo chamadas frontend/API para o módulo resume.

- Arquivos envolvidos:
  - `apps/web/src/App.tsx`
  - `apps/api/Endpoints/ResumeEndpoints.cs`
  - `apps/api/Services/ResumeSectionService.cs`
  - `apps/api/Domain/Entities.cs`
  - `apps/api.Tests/ResumeSectionServiceTests.cs`

- Impacto:
  - Usuário tenta reordenar seções do currículo e recebe erro runtime.
  - A experiência do ATS fica quebrada em um fluxo central.
  - Pode haver divergência entre ordem exibida no frontend e ordem persistida/exportada.

- Solução recomendada:
  - Implementar contrato backend de reorder ou remover temporariamente a ação da UI.
  - Recomendação técnica: implementar, porque o fluxo de currículo editável depende de ordem persistida.
  - O endpoint deve validar:
    - versão pertence ao usuário;
    - todos os IDs enviados pertencem à versão;
    - nenhum ID duplicado;
    - nenhuma seção existente fica fora da lista, salvo decisão explícita;
    - `SortOrder` é recalculado de forma determinística.

- Passos de execução:
  - Confirmar modelo atual de ordenação em `ResumeSection.SortOrder`.
  - Adicionar método `ReorderAsync(userId, versionId, sectionIds)` em `ResumeSectionService`.
  - Adicionar endpoint `POST /resume/versions/{id:guid}/sections/reorder`.
  - Recalcular `PlainText` após reorder.
  - Ajustar frontend apenas se o payload esperado mudar.
  - Adicionar erro claro para lista inválida.

- Testes necessários:
  - Unitário: reorder válido altera `SortOrder`.
  - Unitário: reorder com seção de outra versão rejeita.
  - Unitário: reorder com ID duplicado rejeita.
  - Unitário: reorder com seção faltante rejeita ou aplica regra documentada.
  - Integração/API: endpoint retorna sucesso e lista posterior vem ordenada.
  - Manual: mover seção no Currículo ATS, recarregar página e confirmar ordem persistida.
  - Exportação: exportar TXT/DOCX/PDF e confirmar que a ordem das seções segue a UI.

- Critério de aceite:
  - A ação de reorder não retorna erro.
  - Ordem persistida é a mesma após reload.
  - Exportações respeitam a nova ordem.
  - Testes de `ResumeSectionService` cobrem sucesso e falhas.

- Riscos:
  - Rebuild de `PlainText` pode alterar conteúdo além da ordem.
  - Se a regra permitir lista parcial, pode haver perda de previsibilidade.
  - Alterar ordem sem lock/concurrency pode gerar disputa se houver edição simultânea.

### P0.2  Score de review answers diverge do EvidenceMap

- Diagnóstico:
  - `ResumeEvidenceMapService` calcula `EvidenceScore` com pesos por status, importância e confiança.
  - `ResumeReviewAnswerService.RegenerateAsync` recalcula `EvidenceScore` por proporção simples de itens `proven / total`.
  - Isso cria dois conceitos de score para o mesmo domínio.

- Causa provável:
  - O fluxo de review answers foi implementado depois do evidence map e criou cálculo local para fechar a regeneração.
  - A regra de score não foi extraída para uma API de domínio reutilizável.

- Arquivos envolvidos:
  - `apps/api/Services/ResumeEvidenceMapService.cs`
  - `apps/api/Services/ResumeReviewAnswerService.cs`
  - `apps/api/Services/ResumeTailoringService.cs`
  - `apps/api/Domain/Entities.cs`
  - `apps/api.Tests/ResumeEvidenceMapServiceTests.cs`
  - `apps/api.Tests/ResumeReviewAnswerServiceTests.cs`
  - `apps/api.Tests/ResumeTailoringServiceTests.cs`

- Impacto:
  - Usuário pode responder lacunas e ver score incompatível com o diagnóstico original.
  - Tailoring pode receber evidence map com score calculado de forma diferente.
  - Métricas de qualidade ATS perdem confiabilidade.

- Solução recomendada:
  - Centralizar o cálculo em um componente de domínio reutilizável.
  - Expor função única para:
    - score geral;
    - confidence;
    - warnings;
    - contagem por status;
    - breakdown por requisito obrigatório/desejável.
  - `ResumeEvidenceMapService` e `ResumeReviewAnswerService` devem chamar a mesma rotina.

- Passos de execução:
  - Extrair regra de score de `ResumeEvidenceMapService` para classe interna/serviço dedicado.
  - Substituir cálculo simples em `ResumeReviewAnswerService.RegenerateAsync`.
  - Garantir que warnings pós-review sejam recalculados pela mesma regra.
  - Revisar `ResumeTailoringService` para não recalcular conceitos equivalentes com nomes divergentes.
  - Atualizar reports JSON para incluir versão do algoritmo de score.

- Testes necessários:
  - Unitário: mesmo conjunto de evidências retorna mesmo score em EvidenceMap e ReviewAnswers.
  - Unitário: must-have missing pesa mais negativamente que nice-to-have missing.
  - Unitário: adjacent não equivale a proven.
  - Unitário: unknown mantém tratamento específico.
  - Integração: review answer altera evidência e score esperado.
  - Regressão: testes existentes de tailoring continuam passando.

- Critério de aceite:
  - Não existe cálculo alternativo de `EvidenceScore` em `ResumeReviewAnswerService`.
  - EvidenceMap e regeneração usam a mesma função de score.
  - Reports e UI exibem score consistente para o mesmo conjunto de evidências.

- Riscos:
  - Scores históricos podem mudar.
  - Usuários podem perceber queda/subida de score após correção.
  - Se o algoritmo não for versionado, comparações antigas podem ficar confusas.

### P0.3  PDF perde acentos/Unicode e tem layout manual básico

- Diagnóstico:
  - `ResumeExportService` gera PDF manualmente com sintaxe PDF crua.
  - O texto passa por `ToPdfSafeText`, que normaliza/remove diacríticos e limita caracteres.
  - A validação reextrai texto com PdfPig, mas compara contra texto também convertido para forma PDF-safe.
  - Testes atuais validam criação e extração básica, mas não cobrem acentos, Unicode, layout visual, nomes longos ou quebras profissionais.

- Causa provável:
  - O PDF foi implementado como exportador mínimo e ATS-safe, sem motor de layout profissional.
  - Houve preferência por PDF simples sem tabela/coluna, mas isso sacrificou Unicode, tipografia e aparência.

- Arquivos envolvidos:
  - `apps/api/Services/ResumeExportService.cs`
  - `apps/api/JobPilot.Api.csproj`
  - `apps/api.Tests/ResumeExportServiceTests.cs`
  - `apps/api/Domain/Entities.cs`
  - `apps/web/src/App.tsx`
  - `apps/web/src/index.css`

- Impacto:
  - Currículos em português perdem qualidade textual: nomes, cidades, cargos e competências podem ser degradados.
  - O PDF pode parecer pouco profissional para recrutadores.
  - O produto pode prometer currículo ATS/profissional sem entregar qualidade mínima.
  - Perda de acento pode passar despercebida nos testes atuais.

- Solução recomendada:
  - Separar a decisão em duas camadas:
    - TXT/DOCX como exportações ATS-safe prioritárias.
    - PDF como exportação profissional com Unicode real, fonte embutida e layout controlado.
  - Substituir o PDF manual por uma abordagem com suporte real a Unicode:
    - biblioteca .NET de PDF/layout com fonte embutida; ou
    - HTML/CSS renderizado por motor confiável; ou
    - serviço interno dedicado de geração PDF.
  - O layout deve manter uma coluna, seções claras, bullets simples, margens consistentes e quebras de página previsíveis.

- Passos de execução:
  - Definir padrão visual do PDF antes de escolher biblioteca.
  - Escolher motor de geração com suporte a Unicode, fonte embutida e testes automatizáveis.
  - Criar camada `ResumePdfRenderer` isolada de `ResumeExportService`.
  - Manter `ResumeExportService` como orquestrador de exportação.
  - Remover normalização destrutiva para PDF.
  - Adicionar validação de extração textual com acentos preservados.
  - Adicionar fixtures de currículo em português.
  - Validar layout em 1, 2 e 3 páginas.

- Testes necessários:
  - Unitário: PDF preserva `São Paulo`, `experiência`, `integração`, `automação`, `gestão`.
  - Unitário: texto extraído por PdfPig contém nome completo e seções esperadas.
  - Unitário: headings e ordem de seções respeitam `SortOrder`.
  - Unitário: currículo longo gera múltiplas páginas sem perder seções.
  - Visual/manual: abrir PDF e validar hierarquia, margens, espaçamento e aparência profissional.
  - Comparativo: exportar TXT, DOCX e PDF do mesmo currículo e validar consistência de conteúdo.

- Critério de aceite:
  - PDF preserva acentos e caracteres relevantes.
  - PDF tem texto selecionável e parseável.
  - PDF não usa tabelas ou colunas complexas na versão ATS-safe.
  - PDF tem aparência aceitável para envio profissional.
  - Testes falham se acentos forem removidos.

- Riscos:
  - Introdução de nova biblioteca pode afetar build e licenciamento.
  - HTML-to-PDF pode exigir runtime adicional.
  - Layout mais bonito pode prejudicar ATS se usar colunas, ícones ou headers críticos.
  - Mudança de exportador pode afetar arquivos já gerados se houver reexportação.

## 4. Correções P1

### P1.1  Vínculo ResumeVersion/ResumeExport/ApplicationJob precisa validação e integridade

- Diagnóstico:
  - `ApplicationJob` possui `ResumeVersionId`, `ResumeExportId` e `ResumeFilePath`.
  - Existem testes cobrindo atribuição de currículo e uso de exportação em `ApplicationResumeServiceTests`, `ApplicationRunServiceTests` e `AgentTaskQueueServiceTests`.
  - O PRD V2 marcou risco de integridade porque o vínculo precisa ser validado de ponta a ponta entre banco, API, web e agente.

- Causa provável:
  - O vínculo de currículo foi adicionado ao fluxo de candidatura depois da modelagem inicial de ApplicationJob.
  - Parte da integridade é garantida por serviço/teste, mas precisa ser confirmada no modelo relacional e no runtime.

- Arquivos envolvidos:
  - `apps/api/Domain/Entities.cs`
  - `apps/api/Data/JobPilotDbContext.cs`
  - `apps/api/Data/Migrations/20260602102000_AddApplicationJobResume.cs`
  - `apps/api/Services/ApplicationResumeService.cs`
  - `apps/api/Services/ApplicationRunService.cs`
  - `apps/api/Services/AgentTaskQueueService.cs`
  - `apps/api/Endpoints/RunEndpoints.cs`
  - `apps/agent/Domain/Dtos.cs`
  - `apps/agent/Application/ApplicationAgent.cs`
  - `apps/web/src/App.tsx`
  - `apps/web/src/types.ts`
  - `apps/api.Tests/ApplicationResumeServiceTests.cs`
  - `apps/api.Tests/ApplicationRunServiceTests.cs`
  - `apps/api.Tests/AgentTaskQueueServiceTests.cs`

- Impacto:
  - Agente pode usar currículo errado ou cair para `Profile.ResumePath`.
  - Uma candidatura pode ficar vinculada a exportação incompatível com a versão.
  - Falha no arquivo exportado pode aparecer apenas no momento de upload.

- Solução recomendada:
  - Confirmar e documentar a fonte de verdade:
    - `ApplicationJob.ResumeExportId + ResumeFilePath` quando houver exportação;
    - `ApplicationJob.ResumeVersionId` como referência sem arquivo;
    - fallback para `Profile.ResumePath` somente quando explicitamente permitido.
  - Fortalecer validações de serviço e banco.
  - Adicionar testes runtime do fluxo completo.

- Passos de execução:
  - Auditar migration e snapshot para confirmar FK de `ResumeVersionId` e ausência/presença de FK de `ResumeExportId`.
  - Validar que `ApplicationResumeService` rejeita exportação de outra versão/usuário.
  - Garantir que `ResumeFilePath` existe quando `ResumeExportId` existe.
  - Garantir que agente prioriza `ApplicationJob.ResumeFilePath`.
  - Definir comportamento quando arquivo foi apagado do disco.
  - Atualizar UI para exibir claramente "CV vinculado" versus "CV gerado".

- Testes necessários:
  - Unitário: exportação de outro usuário rejeitada.
  - Unitário: exportação de outra versão rejeitada.
  - Unitário: arquivo ausente gera erro/checkpoint antes do upload.
  - Integração: run cria ApplicationJob com ResumeVersion/ResumeExport.
  - Agent runtime: task usa `ApplicationJob.ResumeFilePath`.
  - Manual: vincular currículo a vaga, iniciar candidatura e confirmar arquivo usado.

- Critério de aceite:
  - Toda candidatura com exportação aponta para versão/exportação compatíveis.
  - Agente nunca usa fallback quando `ResumeFilePath` válido existe.
  - Erro de arquivo ausente é explícito e recuperável.

- Riscos:
  - Adicionar FK tardia pode exigir limpeza de dados existentes.
  - Arquivos locais podem sair de sincronia com banco.
  - Reexportar currículo pode deixar ApplicationJobs antigos apontando para arquivo obsoleto.

### P1.2  `executionTarget` desalinhado entre Web/API/Agent

- Diagnóstico:
  - Web tipa `executionTarget`.
  - API persiste `AgentPolicy.ExecutionTarget` e `AutomationProcessService` usa esse valor para decidir extension/local browser/remoto/server.
  - DTO do Agent auditado não inclui `executionTarget` no record `AgentPolicy`.

- Causa provável:
  - `executionTarget` foi adicionado na API/Web e não propagado ao contrato do Agent.
  - O Agent ainda opera mais por parâmetro de execução/driver do que por política recebida.

- Arquivos envolvidos:
  - `apps/web/src/types.ts`
  - `apps/api/Domain/Entities.cs`
  - `apps/api/Data/Migrations/20260530133000_AddAgentPolicyExecutionTarget.cs`
  - `apps/api/Endpoints/AgentManagementEndpoints.cs`
  - `apps/api/Services/AutomationProcessService.cs`
  - `apps/api/Services/OperationalHealthService.cs`
  - `apps/agent/Domain/Dtos.cs`
  - `apps/agent/Program.cs`
  - `apps/agent/Application/ApplicationAgent.cs`
  - `apps/api.Tests/AgentPolicyTests.cs`
  - `apps/api.Tests/AutomationProcessServiceTests.cs`
  - `apps/api.Tests/OperationalHealthServiceTests.cs`

- Impacto:
  - Usuário pode selecionar um target no frontend e o agente não obedecer em runtime.
  - Health pode reportar um modo enquanto o agente executa outro.
  - Debug de automação fica confuso.

- Solução recomendada:
  - Definir contrato único de política de execução.
  - Incluir `executionTarget` no DTO do Agent se o Agent consome política da API.
  - Se o Agent continuar recebendo driver por CLI, documentar precedência:
    - CLI override;
    - API policy;
    - default local.
  - API, Web e Agent devem compartilhar os mesmos valores aceitos.

- Passos de execução:
  - Mapear onde o Agent busca política e onde define driver.
  - Atualizar DTO do Agent para refletir `executionTarget`.
  - Validar normalização dos valores em API e Agent.
  - Adicionar logs indicando target resolvido.
  - Ajustar health para mostrar target efetivo, não apenas configurado.

- Testes necessários:
  - Unitário API: normalização de targets.
  - Unitário Agent: desserialização de política com `executionTarget`.
  - Integração: target `extension` exige extensão conectada.
  - Integração: target `local_dedicated_browser` seleciona Playwright/local.
  - Manual: trocar target no Web e confirmar comportamento operacional.

- Critério de aceite:
  - Web, API e Agent aceitam o mesmo enum.
  - Target configurado é o target executado ou há override explícito registrado.
  - Health mostra target efetivo.

- Riscos:
  - Compatibilidade com agentes antigos sem o campo.
  - Confusão se CLI e API policy competirem sem regra clara.
  - Valores futuros (`remote_browser`, `server_connector`) podem parecer disponíveis sem implementação.

### P1.3  InHire desalinhado entre agente, background e manifest da extensão

- Diagnóstico:
  - `background.js` lista `inhire.app` como host suportado.
  - `content.js` detecta `inhire.app`.
  - Agent possui módulo InHire e detector ATS para InHire.
  - `manifest.json` não inclui `inhire.app` em `host_permissions` nem em `content_scripts.matches`.

- Causa provável:
  - Suporte InHire foi evoluído no Agent/content/background sem atualizar o manifest.
  - Não há matriz formal de suporte por plataforma e driver.

- Arquivos envolvidos:
  - `apps/extension/manifest.json`
  - `apps/extension/background.js`
  - `apps/extension/content.js`
  - `apps/extension/README.md`
  - `apps/agent/Platforms/Inhire/InhirePlatformModule.cs`
  - `apps/agent/Platforms/Inhire/InhireApplicationFlow.cs`
  - `apps/agent/Application/AtsDetector.cs`
  - `apps/agent/Application/ApplicationAgent.cs`
  - `apps/api/Services/JobFilterService.cs`
  - `apps/api/Data/JobPilotDbContext.cs`

- Impacto:
  - Produto pode indicar suporte a InHire, mas a extensão não operar na página.
  - Comandos de observação/click/fill podem falhar por ausência de permissão/script.
  - Usuário perde confiança no agente.

- Solução recomendada:
  - Decisão explícita:
    - Suportar InHire na extensão agora; ou
    - Marcar InHire como Playwright-only/manual até a extensão estar pronta.
  - Se suportar, alinhar manifest, content script, background, README e testes manuais.
  - Criar matriz `plataforma x driver x capacidade`.

- Passos de execução:
  - Confirmar domínios reais da InHire usados nas vagas.
  - Atualizar manifest apenas se o suporte for aprovado.
  - Validar que `content.js` consegue observar e classificar superfície InHire.
  - Validar que upload de currículo continua gerando checkpoint quando exige seleção manual.
  - Atualizar documentação da extensão.

- Testes necessários:
  - Manual Chrome: extensão injeta `content.js` em `https://*.inhire.app/*`.
  - Manual: popup mostra página suportada em InHire.
  - Manual: comando observe retorna campos visíveis.
  - Agent runtime: navegação para InHire resolve módulo correto.
  - Regressão: LinkedIn/Gupy/Indeed continuam funcionando.

- Critério de aceite:
  - Manifest, background, content script, Agent e docs dizem a mesma coisa.
  - InHire não aparece como suportado se não estiver operacional.
  - Pelo menos um fluxo de observação InHire é validado manualmente.

- Riscos:
  - Host permission adicional amplia superfície da extensão.
  - InHire pode ter múltiplos subdomínios ou fluxos distintos.
  - Suporte parcial pode ser pior que não prometer suporte.

### P1.4  OCR pode gerar metadados inconsistentes

- Diagnóstico:
  - `ProfileImportService` lê currículo via `ResumeTextService`.
  - Se o relatório exige OCR, chama `ResumeOcrService.RunOcr`.
  - Quando OCR melhora o texto, `resumeText` é atualizado.
  - Porém a persistência usa campos de `extraction.Report` original para `ResumeImport`, `ResumeDocument` e `ResumeVersion`.

- Causa provável:
  - OCR foi adicionado como melhoria posterior no fluxo de importação.
  - O relatório final não foi recomposto após substituição do texto.

- Arquivos envolvidos:
  - `apps/api/Services/ProfileImportService.cs`
  - `apps/api/Services/ResumeTextService.cs`
  - `apps/api/Services/ResumeOcrService.cs`
  - `apps/api/Domain/Entities.cs`
  - `apps/api.Tests/ProfileImportServiceTests.cs`
  - `apps/api.Tests/ResumeOcrServiceTests.cs`

- Impacto:
  - Banco pode registrar `RequiresOcr=true` mesmo após OCR útil.
  - `TextQualityScore`, `WordCount`, `CharacterCount` e warnings podem não refletir o texto final.
  - UI e decisões ATS podem tratar currículo como pior do que está.

- Solução recomendada:
  - Criar conceito de `effectiveExtraction`.
  - Após OCR bem-sucedido, recalcular ou montar relatório final com:
    - método `ocr` ou `native_pdf+ocr`;
    - novo word count;
    - novo character count;
    - quality score recalculado;
    - warnings preservando histórico sem manter estado falso.

- Passos de execução:
  - Isolar cálculo de `ResumeExtractionReport` para permitir recomputação.
  - Criar relatório final após OCR.
  - Persistir `effectiveExtraction` em vez da extração inicial.
  - Retornar no response o mesmo relatório persistido.
  - Adicionar warning claro quando OCR foi tentado e falhou.

- Testes necessários:
  - Unitário: OCR melhora texto e relatório persistido reflete texto final.
  - Unitário: OCR falha e relatório mantém requires review.
  - Unitário: OCR não melhora word count e texto original permanece.
  - Manual: importar PDF escaneado com OCR disponível.
  - Runtime: testar ambiente sem `tesseract`/`ocrmypdf`.

- Critério de aceite:
  - Response, ResumeImport e ResumeVersion usam o mesmo relatório final.
  - OCR bem-sucedido não deixa metadados contraditórios.
  - Warnings explicam claramente método usado.

- Riscos:
  - Recalcular score pode alterar status de currículos importados.
  - Ambiente local pode não ter OCR instalado.
  - OCR pode introduzir texto ruidoso; precisa preservar `RequiresReview` quando qualidade ainda for baixa.

### P1.5  App frontend concentra muitas telas e regras

- Diagnóstico:
  - `apps/web/src/App.tsx` concentra carregamento inicial, estados, handlers e várias telas.
  - `api/client.ts` é genérico, sem clients por domínio.
  - Tipos globais em `types.ts` atendem múltiplos módulos.

- Causa provável:
  - O frontend cresceu rapidamente por incrementos de produto.
  - Não houve etapa de modularização por feature após a expansão de ATS, agente e credenciais.

- Arquivos envolvidos:
  - `apps/web/src/App.tsx`
  - `apps/web/src/api/client.ts`
  - `apps/web/src/types.ts`
  - `apps/web/src/components/layout.tsx`
  - `apps/web/src/index.css`

- Impacto:
  - Qualquer mudança em ATS, Jobs ou Agent pode gerar regressão em telas não relacionadas.
  - Contratos de API ficam difíceis de auditar.
  - Testes e revisão de PR ficam maiores do que deveriam.

- Solução recomendada:
  - Não iniciar refatoração ampla antes dos P0.
  - Preparar modularização incremental por fronteiras:
    - `features/resume`
    - `features/jobs`
    - `features/agent`
    - `features/profile`
    - `features/settings`
  - Extrair primeiro clients de API, depois hooks, depois componentes.

- Passos de execução:
  - Após P0/P1 de contrato, criar inventário dos handlers por feature.
  - Extrair `resumeClient`, `jobsClient`, `agentClient`, `profileClient`.
  - Extrair tipos por domínio mantendo compatibilidade.
  - Extrair tela `ResumeAtsTab` depois que reorder/PDF estiverem estabilizados.
  - Extrair telas de Agent e Jobs.
  - Manter `App.tsx` como shell/orquestrador.

- Testes necessários:
  - Build TypeScript.
  - Smoke test manual de todas as tabs.
  - Teste visual básico desktop/mobile.
  - Teste de carregamento inicial com API ativa.
  - Regressão dos fluxos ATS e candidatura.

- Critério de aceite:
  - Nenhuma extração muda comportamento.
  - Clients por domínio cobrem chamadas principais.
  - `App.tsx` reduz responsabilidade sem reescrever UX.

- Riscos:
  - Refatoração prematura pode atrasar P0.
  - Extração sem testes pode apenas mover complexidade.
  - Dividir tipos cedo demais pode quebrar contratos silenciosamente.

## 5. Ordem recomendada de implementação

Sequência ideal:

1. **Congelar novas features em ATS/PDF/agente**  
   Até P0/P1 fecharem, aceitar apenas correções e testes.

2. **Corrigir contrato de reorder**  
   É a menor correção P0 e desbloqueia fluxo direto do usuário.

3. **Centralizar score EvidenceMap/ReviewAnswers**  
   Deve vir antes de ajustes visuais no ATS, porque afeta confiança do diagnóstico.

4. **Fortalecer vínculo ResumeVersion/ResumeExport/ApplicationJob**  
   Deve ser resolvido antes de mexer no PDF final, porque exportação precisa chegar corretamente à candidatura.

5. **Redefinir e implementar estratégia de PDF**  
   Deve usar o contrato de seções e versão/exportação já estabilizado.

6. **Sincronizar `executionTarget` Web/API/Agent**  
   Garante que automação execute conforme política.

7. **Alinhar suporte InHire**  
   Decidir se será suportado pela extensão agora ou explicitamente limitado.

8. **Corrigir metadados OCR**  
   Pode ocorrer em paralelo com PDF se houver cuidado nos testes de importação.

9. **Planejar modularização frontend**  
   Só executar a refatoração depois de contratos críticos estabilizados.

## 6. Testes obrigatórios antes de seguir para novas features

### Testes unitários

- `ResumeSectionServiceTests`
  - reorder válido;
  - IDs inválidos;
  - duplicidade;
  - persistência de `SortOrder`;
  - rebuild de `PlainText`.

- `ResumeEvidenceMapServiceTests`
  - score ponderado;
  - proven/adjacent/missing/unknown;
  - must-have versus nice-to-have.

- `ResumeReviewAnswerServiceTests`
  - regeneração usando mesmo score do EvidenceMap;
  - resposta que transforma missing em proven;
  - resposta insuficiente mantendo pending/missing.

- `ResumeExportServiceTests`
  - PDF preserva acentos;
  - PDF extrai texto nativo;
  - PDF mantém ordem de seções;
  - DOCX/TXT permanecem compatíveis.

- `ProfileImportServiceTests`
  - OCR bem-sucedido atualiza relatório final;
  - OCR falho mantém revisão necessária;
  - importação TXT/DOCX/PDF preserva metadados coerentes.

- `ApplicationResumeServiceTests`
  - exportação correta;
  - exportação de outra versão rejeitada;
  - arquivo ausente tratado.

- `AgentPolicyTests` / `AutomationProcessServiceTests`
  - `executionTarget` normalizado;
  - target extension exige extensão;
  - target local usa driver local.

### Testes de integração

- API:
  - `POST /resume/versions/{id}/sections/reorder`
  - `POST /resume/review-answers/regenerate`
  - `POST /resume/versions/{id}/exports`
  - `PATCH /application-jobs/{id}/resume`
  - `/health/operational` refletindo execution target.

- Banco:
  - migrations aplicam limpas em PostgreSQL local;
  - constraints de curriculo/candidatura não quebram dados existentes;
  - índices de ResumeVersion/ResumeExport/ApplicationJob continuam válidos.

### Testes manuais/runtime

- Importar currículo real com acentos.
- Gerar currículo ATS genérico.
- Organizar vaga real.
- Gerar currículo direcionado.
- Reordenar seções e recarregar página.
- Responder lacunas e regenerar.
- Exportar TXT/DOCX/PDF.
- Abrir PDF e validar aparência profissional.
- Extrair texto do PDF e conferir acentos.
- Vincular exportação a vaga/candidatura.
- Rodar agente em modo Playwright.
- Rodar agente em modo extension.
- Testar extensão em LinkedIn, Gupy, Indeed e, se aprovado, InHire.

### Testes visuais

- Currículo PDF de 1 página.
- Currículo PDF de 2 páginas.
- Currículo PDF de 3 páginas.
- Nome longo.
- Experiência longa.
- Seção vazia.
- Competências com termos acentuados.
- Layout em desktop e mobile da tela ATS.

## 7. Critérios para liberar a próxima fase

Só iniciar melhorias visuais amplas, novas features ou expansão de plataformas quando:

- Todos os P0 estiverem corrigidos, testados e validados em runtime.
- P1 de integridade, executionTarget, OCR e InHire tiverem decisão implementada ou limitação explicitada no produto.
- Nenhuma tela ativa chamar endpoint inexistente.
- Score ATS for consistente entre EvidenceMap, ReviewAnswers e Tailoring.
- PDF preservar acentos, ter texto selecionável e aparência profissional mínima.
- Exportação vinculada à candidatura for usada corretamente pelo Agent.
- Extensão e Agent tiverem matriz de suporte por plataforma documentada e coerente.
- Testes unitários e integração dos fluxos críticos estiverem verdes.
- Houver checklist manual executado para o fluxo completo de currículo: importar -> gerar ATS -> organizar vaga -> tailor -> revisar -> exportar -> vincular -> aplicar.

Depois disso, a próxima fase pode priorizar:

- modularização real do frontend;
- padronização visual;
- melhoria de UX do ATS;
- novos layouts de currículo;
- expansão de plataformas;
- melhorias avançadas de IA.
