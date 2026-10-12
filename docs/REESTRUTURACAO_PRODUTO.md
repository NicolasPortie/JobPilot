# JobPilot — auditoria e reestruturação

**Data:** 2026-09-11  
**Status:** núcleo manual implementado; hardening e migração legada em andamento.  
**Escopo:** produto, UX/UI, frontend, API, domínio, dados, IA, segurança, operação, testes e documentação.

## 1. Decisão de produto

O projeto original tentou ser um autopilot que encontra vagas, controla o navegador, preenche formulários e envia candidaturas. Isso juntou responsabilidades demais em um único sistema: navegador, login externo, credenciais, IA, filas, checkpoints, auditoria, aprendizado, extensão e interface operacional.

Essa promessa é difícil de entregar com segurança e difícil de explicar. O produto deve ser reduzido para uma tarefa que gera valor verificável:

> **JobPilot ajuda a encontrar oportunidades relevantes e preparar um currículo verdadeiro, adaptado a cada vaga.**

Fluxo de valor:

```text
foco de busca → vaga trazida pelo usuário → requisitos + fontes + confiança
     → aderência do currículo → lacunas e perguntas → versão adaptada
     → revisão/edição → exportação → candidatura manual fora do JobPilot
```

O JobPilot não deve controlar mouse, teclado, DOM ou abas; fazer login em LinkedIn/ATS; armazenar senha, cookie ou token de plataforma; preencher ou enviar formulários; monitorar vagas 24/7; ou prometer aprovação por inserir palavras-chave.

## 2. Diagnóstico do que não fazia sentido

| Problema | Por que era errado | Decisão |
|---|---|---|
| `App.tsx` e `useJobPilotApp` conheciam todos os domínios | estado global e `refresh()` viravam um segundo backend | manter apenas perfil, busca, vagas e currículo no fluxo web |
| busca iniciava agente/processo externo | erro de produto e risco operacional para uma ação simples | salvar intenção; analisar a vaga quando o usuário a trouxer |
| agente, extensão e API compartilhavam responsabilidades | fronteiras difíceis de testar e autorização frágil | legado em quarentena, sem dependência do MVP |
| credenciais de plataforma eram persistidas/decriptadas | uma falha na API poderia comprometer a conta externa | retirar da experiência e bloquear rotas |
| score aparecia como nota sem fonte | incentiva otimização cega e pode inventar experiência | aderência explicável por evidências, cobertura e confiança |
| respostas, aprendizados, runs, checkpoints e trace eram produto | eram ferramentas internas de automação, não valor principal | remover do bundle principal; manter só para migração/testes |
| defaults assumiam `.NET`, Júnior e Remoto | personalização falsa e difícil de reutilizar | defaults neutros; preferências do usuário são fonte de verdade |
| CPF, `ResumePath`, `FilePath` e URLs internas circulavam em DTOs | vazamento de PII e acoplamento ao filesystem | contratos públicos reduzidos a dados necessários e IDs opacos |
| login social, cadastro, recuperação e claims de 24/7 eram placeholders | a UI prometia recursos que não existiam | remover da experiência até haver implementação real |

## 3. Baseline implementada nesta rodada

- shell web premium e editorial com `Início`, `Vagas`, `Currículo`, `Perfil` e preferências de busca;
- busca salva tratada como foco reutilizável, sem navegador, Playwright, extensão ou processo externo;
- entrada manual por link, texto, screenshot/PDF e upload privado do currículo;
- análise de vaga com requisitos, evidências, importância, confiança e avisos;
- aderência do currículo com fatos comprovados, termos relacionados, lacunas e perguntas;
- editor de seções, versões ATS-safe, diff e exportação PDF/DOCX/TXT;
- API de automação respondendo `410 Gone`, fora do OpenAPI principal;
- DTOs públicos sem CPF, caminhos físicos, senha, token ou campos de automação;
- caminhos de OCR criados pelo servidor e validados na área privada do usuário;
- egress com bloqueio de rede privada/loopback, redirects controlados, limites de resposta e allowlist de IA;
- limite global por usuário/IP e limite específico para análise;
- chave de proteção de dados independente da chave JWT e bearer web apenas em `sessionStorage`;
- defaults de carreira e filtros neutros, sem hardcode de stack, senioridade ou localidade.

## 4. Escopo definitivo

### Manter no MVP

- perfil profissional e links que o candidato escolheu compartilhar;
- upload e revisão do currículo base;
- biblioteca de documentos, versões e seções editáveis;
- focos de busca salvos;
- oportunidade importada por texto, URL, PDF, screenshot ou fonte de leitura autorizada;
- requisitos com trecho de origem, importância e confiança;
- aderência explicável e sugestões baseadas em evidência;
- adaptação determinística + IA estruturada, sempre revisável;
- diff, perguntas de confirmação, edição manual e exportação;
- status manual simples: nova, salva, revisar, arquivada e candidatura feita.

### Remover ou congelar

- controle de navegador, mouse, teclado, DOM e abas;
- extensão que executa comandos ou faz polling;
- login externo automático, cookies, senhas e tokens de plataforma;
- `auto-submit`, fila, batch apply, runs e workers de navegador;
- `AgentPolicy`, `AgentDevice`, `AgentRun`, `AgentStep`, `AgentTraceStep` e memória de workflow como produto;
- `ApplicationRun`, `ApplicationJob`, campos e respostas de preenchimento automático;
- respostas reutilizáveis, padrões de pergunta e rascunhos para autofill;
- captcha/2FA como fluxo de produto;
- multi-tenant, SaaS remoto, monitoramento 24/7 e conectores autenticados;
- claims de “garantir aprovação”, “aumentar pontos” ou experiência inexistente;
- telas de IA, credenciais, agente, aprendizado, auditoria técnica e configurações fictícias.

## 5. Arquitetura alvo

```text
React SPA
    ↓ origem local confiável / HTTPS
ASP.NET Core API
    ├── Auth e escopo do usuário
    ├── Perfil e currículo
    ├── Focos e oportunidades
    ├── Análise de requisitos e aderência
    ├── Tailoring, revisão e exportação
    └── gateway de IA estruturada
    ↓
PostgreSQL + armazenamento privado
```

Organização desejada por feature:

```text
apps/api/
  Features/Auth
  Features/Profile
  Features/Resume
  Features/Opportunities
  Features/Tailoring
  Domain/
  Infrastructure/Persistence
  Infrastructure/FileStorage
  Infrastructure/Ai
apps/web/src/
  features/auth
  features/profile
  features/jobs
  features/searches
  features/resume
  app/              # composição fina e sessão
  api/              # clients pequenos, sem regras de negócio
```

Regras:

- controller recebe DTO e nunca conhece `DbContext` ou filesystem;
- cada consulta recebe o `userId` autenticado e valida pai + filho;
- domínio não depende de React, HTTP, Playwright ou provider;
- cada intenção de IA tem contrato próprio e saída schema-first;
- conteúdo de vaga e saída de IA são dados, nunca comandos;
- web → API; feature → application/domain; infrastructure implementa interfaces;
- nenhum caminho físico cruza o contrato público.

## 6. Modelo de dados e migração

### Núcleo atual

`AppUser`, `CandidateProfile`, `ResumeImport`, `ResumeDocument`, `ResumeVersion`, `ResumeSection`, `SavedSearch`, `JobLead`, `JobTarget`, `JobRequirement`, mapas de evidência, tailoring e exportações.

### Compatibilidade temporária

As tabelas de agente, aplicação, credenciais, respostas e trace podem continuar nas migrações enquanto dados existentes são identificados. Elas não devem receber novos writes do fluxo web. As tabelas da extensão (`BrowserExtensionSessions`, `BrowserExtensionCommandJobs`) foram removidas pela migration `RemoveExtensionProtocol`.

Ordem segura:

1. congelar novas features e fazer backup;
2. parar writes e retirar telas/clients;
3. exportar dados que tenham valor para histórico;
4. migrar oportunidades, versões e status manuais;
5. testar dois usuários e downloads;
6. remover controllers, serviços, tabelas e projetos legados em uma mudança separada;
7. executar migração fora do boot produtivo, com backup e rollback.

## 7. UX/UI premium e não genérica

Direção visual já aplicada:

- linguagem editorial, quente e controlada, com tipografia de leitura e um acento coral/teal sem arco-íris de estados;
- composição com hierarquia e respiro, sem dashboard de cards repetitivos, gradientes clichês ou “AI glow”;
- hero orientada à decisão, não a métricas inventadas;
- score sempre acompanhado de motivo e fonte;
- ações com verbos claros: `Salvar busca`, `Trazer vaga`, `Adaptar currículo`, `Revisar`, `Exportar`;
- estados de vazio, erro, carregamento e baixa confiança com próximo passo;
- foco visível, hit areas adequadas, contraste e `prefers-reduced-motion`;
- GSAP limitado à entrada de workspace e microinterações com cleanup, transform e respeito à redução de movimento.

Estrutura das telas:

- **Início:** próxima decisão, focos salvos, oportunidades recentes e pendências reais;
- **Vagas:** biblioteca comparável, filtros simples, detalhe, origem, aderência e status manual;
- **Currículo:** importar, escolher vaga, analisar, adaptar, editar, visualizar e exportar;
- **Perfil:** fatos profissionais, links, qualidade da base e histórico de recomendações;
- **Preferências:** focos e critérios de busca, sem automação.

## 8. Segurança e privacidade

### Regras obrigatórias

- `Auth:JwtKey` e `Security:SecretKey` independentes, com rotação documentada;
- fallback de criptografia legado somente com flag explícita de migração;
- produção sem origem genérica de extensão (removida) e sem pareamento local não autenticado (removido);
- JWT de agente sem acesso por padrão; runtime legado desligado;
- uploads fora da raiz pública, nomes opacos por usuário, limites de tamanho, validação de assinatura/pacote e limpeza em falha;
- assinatura real de PDF/DOCX, antivírus e retenção antes de abrir uma instalação pública;
- URL de vaga e links públicos com HTTPS, DNS/IP privado bloqueado, redirect seguro, timeout e limite de bytes;
- provider de IA configurado/allowlist, nunca endpoint arbitrário com chave do usuário;
- prompt mínimo: excluir CPF, caminhos, tokens, senha, e-mail/telefone quando não necessários;
- logs, telemetria e respostas redigidos; nunca persistir segredo em trace;
- rate limit em login, upload, análise, exportação e limite global por usuário/IP;
- exclusão de conta/dados e TTL para originais, texto, prompts, respostas e exports;
- testes de IDOR para perfil, vaga, documento, versão, seção, evidência e exportação.

### Superfícies legadas

`/api/automation/*` está desativado com `410 Gone`. Controllers de agente, credenciais e rascunho de resposta estão fora do OpenAPI ou exigem `LegacyRuntime`; o runtime fica `false` por padrão. Isso é uma barreira de migração, não uma promessa de suporte. A extensão foi removida por completo: `api/extension/*` não existe mais (retorna `404`).

## 9. Qualidade e observabilidade

Gates permanentes:

- build e testes unitários da API;
- testes HTTP/OpenAPI e integração PostgreSQL;
- lint, testes e build do frontend;
- análise de dependências e segredos;
- testes de autorização cruzada, upload, URL, IA e download;
- verificação de que a requisição do produto não inicia navegador, agente ou extensão;
- testes visuais por fluxo, teclado, viewport pequeno e `prefers-reduced-motion`;
- logs estruturados sem currículo, PII ou segredos.

O orçamento de linhas é apenas alerta. A métrica principal é responsabilidade única, contrato claro, estado compreensível e fluxo verificável.

## 10. Ordem de execução

1. decisão de produto e linguagem — concluída;
2. navegação e bundle web — concluídos para o fluxo principal;
3. contratos públicos e limites de segurança — implementados, com antivírus e política de retenção de currículos como próximo hardening;
4. fluxo vaga → aderência → currículo → exportação — implementado;
5. limpar/migrar entidades e serviços legados — em andamento;
6. melhorar velocidade do trabalho com comparação lado a lado, histórico/undo e notas manuais;
7. somente com uso comprovado, avaliar captura somente leitura e opt-in — nunca automação de candidatura.

## 11. Critérios de aceite

A reestruturação está correta quando uma pessoa consegue:

- entrar sem credencial previsível;
- importar currículo e revisar a extração;
- salvar um foco sem iniciar automação;
- trazer uma vaga e ver requisitos com fonte;
- entender o que é comprovado, relacionado, ausente ou incerto;
- gerar e editar uma versão adaptada sem invenção;
- exportar o resultado sem receber caminho físico;
- marcar a candidatura manualmente;
- usar tudo isso sem conhecer agente, fila, checkpoint ou browser driver.

## 12. Fontes de verdade

- `README.md` — setup e contratos principais;
- `docs/ARCHITECTURE.md` — fronteiras técnicas atuais;
- `docs/ROADMAP.md` — próximos incrementos e futuro permitido;
- `SECURITY.md` — requisitos de segurança;
- `docs/ATS_RESUME_PRODUCT_PLAN.md` — regras de currículo, evidência e exportação;
- documentos de automação, agente e extensão — históricos, não critérios de aceite.
