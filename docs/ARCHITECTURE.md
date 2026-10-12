# JobPilot — arquitetura do produto atual

O JobPilot é um workspace local-first para transformar uma oportunidade em uma candidatura bem preparada. O sistema coleta contexto fornecido pelo usuário, organiza requisitos, compara evidências do currículo e gera uma versão adaptada para revisão humana.

O limite central é intencional: a aplicação não controla o computador, não abre ou navega em contas externas, não salva credenciais de plataformas e não envia formulários. O usuário continua no comando da candidatura.

## Limites de runtime

- `apps/web`: interface principal — perfil, entrada de vaga, currículo, aderência, edição e exportação.
- `apps/api`: autenticação, isolamento por usuário, persistência, ingestão de texto/URL/anexo, análise de requisitos, tailoring e downloads autorizados.
- armazenamento privado: arquivos recebidos e exportações ficam fora da raiz pública, com nomes opacos e acesso por autorização.
- `infra`: PostgreSQL e configuração de desenvolvimento.
- `apps/agent`: legado em quarentena. Não é dependência do fluxo web; suas rotas HTTP estão desativadas ou protegidas por uma policy de migração.
- Extensão do navegador: **removida do produto** (código, protocolo `api/extension/*`, tabelas `BrowserExtensionSessions`/`BrowserExtensionCommandJobs` e opções de pareamento/CORS). Não existe mais nada para instalar ou parear.

## Fluxo principal

```text
vaga trazida pelo usuário → requisitos + fonte + confiança
     → comparação com currículo → perguntas/lacunas → versão adaptada
     → revisão/edição → exportação → candidatura manual
```

### 1. Entrada fornecida pelo usuário

O perfil reúne cargo, habilidades e contexto profissional. A entrada de uma vaga é link, texto, PDF ou screenshot trazido pelo usuário. Buscas salvas foram removidas do modelo pela migração `RemoveSavedSearches`; a busca externa automática não faz parte do produto atual.

### 2. Ingestão de vaga

`JobTargetService` recebe texto ou uma URL fornecida pelo usuário. A URL passa por uma política de egress dedicada, sem redirects abertos, limite de resposta e bloqueio de redes privadas. Anexos são persistidos em área privada e passam por limites antes de serem enviados à análise.

### 3. Análise e evidências

O analisador determinístico e, quando habilitada, a IA estruturam cargo, empresa, modalidade, senioridade, resumo e requisitos. Cada requisito conserva evidência textual, importância, palavra normalizada e confiança. Conteúdo da vaga é dado não confiável: nunca vira comando, ferramenta ou navegação.

### 4. Currículo

O currículo enviado é convertido em documento, versão base e seções editáveis. `ResumeTailoringService` cruza requisitos com habilidades e evidências reais. A saída separa cobertura direta, termos relacionados, itens excluídos, perguntas que precisam de confirmação e avisos. A IA pode melhorar redação, mas não pode inventar fatos.

### 5. Saída

O usuário edita seções, escolhe um layout ATS-safe e exporta PDF, DOCX ou TXT. A API devolve identificadores e metadados; nunca devolve caminhos físicos. O download verifica usuário, versão, exportação e formato antes de transmitir o arquivo.

## Contratos públicos

Os contratos da web são deliberadamente menores que o legado:

- perfil: dados profissionais necessários para adaptação; CPF e `ResumePath` não saem pelo endpoint público;
- vaga: título, empresa, descrição, origem, link, score, motivo, status e IDs opacos de currículo; campos de automação e caminhos físicos não saem;
- importação: upload multipart; o caminho é criado pelo servidor e só existe internamente;
- exportação: id, versão, formato, avisos e data; o caminho do arquivo permanece privado;
- automação/extensão: `410 Gone` ou policy de legado, sem aparecer no OpenAPI principal.

## Segurança por desenho

- JWT de usuário e chave de proteção de dados são independentes.
- Os tokens de acesso e renovação ficam em `sessionStorage`, limitados à sessão da aba.
- Limite global por usuário/IP e limite específico para análise reduzem abuso e custo de IA.
- Uploads têm limite de tamanho, extensão permitida, diretório privado, nome sanitizado e validação de assinatura; DOCX também passa por limite de entradas/expansão. A fonte de screenshot é removida após a análise; originais, exportações e logs de IA passam pela política de retenção. O antivírus está desativado por padrão e permanece uma pendência de operação.
- Perfil, vaga, versão, seção, mapa de evidência e exportação são sempre consultados com `UserId` e relação pai-filho.
- URL de vaga, links públicos do currículo e endpoint de IA passam por allowlist/egress seguro.
- Logs e prompts devem ser redigidos; CPF, tokens, senhas e caminhos não entram em traces públicos.

## O que ficou fora do núcleo

Não fazem parte da experiência principal: `AgentRun`, `AgentStep`, `AgentTraceStep`, checkpoints de navegador, memória de workflow, `PlatformCredential`, respostas reutilizáveis para autofill, login de LinkedIn, fila de aplicação, `auto-submit`, polling de extensão e drivers Playwright de navegação.

Esses modelos ainda podem existir nas migrações e nos testes históricos para permitir uma retirada controlada. O runtime legado está desligado por padrão em `Features:EnableLegacyRuntime=false`, e tokens comuns não recebem a claim necessária para acessar suas policies.

## Decisões de manutenção

- A API é a fonte de verdade dos contratos; o frontend não deve recriar regras de negócio ou caminhos de arquivo.
- A UI deve usar os termos foco, oportunidade, aderência, evidência, revisão e exportação. Termos agent, run, queue, checkpoint e browser não aparecem no fluxo normal.
- Toda nova integração externa precisa de consentimento explícito, egress restrito, limites, auditoria e uma decisão de produto separada.
- Antes de remover tabelas legadas, exportar/backup dos dados e executar migração reversível. A remoção física de agente/extensão é uma etapa posterior, não uma dependência para o MVP.
