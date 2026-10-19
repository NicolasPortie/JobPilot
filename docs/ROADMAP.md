# JobPilot — roadmap

## Posição do produto

JobPilot ajuda uma pessoa a pesquisar oportunidades, entender se uma vaga combina com seu repertório e preparar um currículo específico. O valor está na clareza da decisão e na qualidade da preparação, não em disparar candidaturas em volume.

## Entregue nesta reconstrução

- shell web editorial com quatro áreas: Início, Vagas, Currículo e Perfil;
- foco de busca salvo, sem promessa de monitoramento ou busca infinita;
- entrada manual por URL, texto, screenshot/PDF e upload privado de currículo;
- análise de requisitos com evidência, confiança, score explicável e perguntas de revisão;
- editor de seções, versões ATS-safe e exportação PDF/DOCX/TXT;
- respostas públicas sem CPF, caminhos físicos ou credenciais;
- egress seguro para URLs e provedores de IA, rate limits e chaves independentes;
- automação, extensão e credenciais retiradas da experiência principal e desativadas por padrão.

## Próxima sequência

### P0 — fechar confiança

- validar assinatura real de PDF/DOCX, limites de páginas e antivírus;
- definir retenção/exclusão para originais, texto extraído, prompts, respostas e exports;
- adicionar testes de dois usuários para cada recurso filho e cada download;
- manter migração/backup antes de qualquer migração destrutiva;
- atualizar o OpenAPI gerado para refletir somente os contratos atuais.

### P1 — tornar o trabalho mais rápido

- colar uma vaga e importar o contexto em uma única entrada com preview antes de salvar;
- mostrar comparação lado a lado entre requisito, evidência do currículo e sugestão;
- permitir notas privadas e etapas manuais por oportunidade;
- melhorar o editor com undo, histórico de versões e visualização ATS antes do download;
- oferecer modelos de foco por intenção, sem hardcode de carreira, senioridade ou tecnologia.

### P2 — descoberta responsável

- conectores somente de leitura e opt-in para fontes permitidas;
- sincronização explícita, deduplicação, origem e data de captura;
- nenhuma abertura de sessão, preenchimento ou envio automático;
- revisão jurídica e de termos de uso antes de qualquer conector externo.

## Fora do roadmap atual

Login automático, armazenamento de senhas de plataformas, controle do navegador, extensão que executa comandos, `auto-submit`, filas de candidatura e agentes que tomam decisões irreversíveis. Qualquer retorno desses temas exige uma nova decisão de produto e uma revisão de segurança independente.
