# Vaga Automatica: leitura competitiva

Pesquisa feita em 2026-05-29 a partir da pagina publica, Termos, Chrome Web Store e reclamacoes publicas indexadas.

## O que parece funcionar na tese

- A dor e clara: candidatos perdem muito tempo repetindo formularios e buscando vagas.
- A promessa de valor e simples: configurar uma vez e aplicar em volume.
- O canal de extensao Chrome faz sentido porque usa a sessao real do usuario e evita abrir um navegador separado.
- LinkedIn e o primeiro alvo natural porque tem fluxo repetitivo e Easy Apply.

## Onde eles parecem fracos

- Promessa ampla demais: o site vende LinkedIn, Gupy e Indeed, mas os Termos descrevem a extensao como automacao para LinkedIn.
- Confiabilidade questionavel: reclamacoes publicas citam extensao que nao envia vagas, fluxo que para e necessidade de atualizar pagina.
- Expansao de plataformas pouco comprovada: ha relatos de que Gupy e Indeed nao estavam funcionando na pratica.
- Baixa transparencia operacional: a proposta fala em aplicar centenas/milhares de vagas, mas nao mostra claramente auditoria por candidatura, motivo de falha, checkpoint ou politicas de risco.
- Risco de conta e compliance: o FAQ menciona bloqueio, mas a experiencia precisa deixar limites e consentimento muito claros.
- Suporte vira parte do produto: quando a automacao falha, sem trace e sem explicacao o usuario sente que comprou uma caixa-preta.

## Como o JobPilot deve ser melhor

1. Extensao-first, nao navegador controlado.
   A extensao observa e executa comandos pequenos dentro da aba real do usuario. O usuario nao precisa deixar um segundo browser aberto.

2. LinkedIn confiavel antes de prometer multiplataforma.
   O MVP deve declarar LinkedIn/Easy Apply como primeiro conector. Gupy, Indeed e outros entram como conectores com status separado: experimental, beta ou estavel.

3. Automacao com auditoria.
   Cada candidatura precisa registrar: vaga, pagina, campos detectados, respostas usadas, acoes executadas, bloqueios, screenshots/evidencias e resultado final.

4. Checkpoint como recurso, nao falha.
   Perguntas ambiguas, dados sensiveis, captcha, login, salario, diversidade, documentos e submit de alto risco pausam com pedido claro para o usuario.

5. Politicas explicitas de volume e submit.
   Nada de "1000 por dia" como comportamento invisivel. O usuario escolhe limites por execucao/dia, dominios permitidos, dry-run/review e auto-submit.

6. Melhor que automacao burra.
   O motor deve ranquear vagas, ignorar vagas ruins, adaptar respostas aprovadas, aprender com revisoes e explicar por que aplicou ou nao aplicou.

7. Privacidade como produto.
   Senhas e cookies ficam no browser. O JobPilot guarda somente o minimo necessario, com redaction em trace e controle claro de credenciais/respostas.

## Estrategia de produto

- `MVP`: extensao Chrome + painel JobPilot + LinkedIn Easy Apply + fila + auditoria + checkpoints.
- `Beta`: conectores Gupy/Indeed com observacao e preenchimento assistido, mas submit conservador.
- `Pro`: automacao continua com limites, scoring, respostas reutilizaveis, aprendizado aprovado e relatorios.

## Implicacoes tecnicas

- A extensao deve ser o driver/transport principal.
- O harness continua no backend/agente local: decisao, risco, guardrails, LLM, redaction, checkpoints e trace.
- O content script nao decide o que preencher. Ele apenas observa DOM, marca elementos e executa comandos aprovados.
- O popup da extensao deve mostrar estado da pagina e permitir pausar/retomar.
- O backend precisa de um canal real com a extensao: Native Messaging, WebSocket local, long-polling autenticado ou pareamento local invisivel. Token manual deve ser detalhe interno, nunca tarefa do usuario comum.

## Fontes consultadas

- Pagina publica da VagaAutomatica: `https://www.vagaautomatica.com/`
- Termos de Uso da Vaga Automatica: `https://www.vagaautomatica.com/termos`
- Chrome Web Store: `https://chromewebstore.google.com/detail/vagaautomatica-automatize/fmghmiebmiiimnojcecknmacpedoiklm`
- Reclamacoes publicas no Reclame Aqui sobre falhas de extensao, Gupy/Indeed e suporte.
