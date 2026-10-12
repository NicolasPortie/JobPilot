# Testes de migração PostgreSQL

Os testes de integração usam PostgreSQL real e são opt-in localmente. Sem
`JOBPILOT_TEST_POSTGRES`, xUnit os reporta como **ignorados**, com o motivo.
Com a variável definida, conexão inválida ou falta de permissão falham o teste;
não há fallback para InMemory.

Use um cluster exclusivo de teste (PostgreSQL 16, como o CI). A conexão fornecida
é administrativa: o usuário precisa de `CREATEDB` e permissão para remover os
bancos que criou. O banco indicado na conexão nunca recebe migrações nem é
apagado. Cada teste cria `jobpilot_test_<GUID>` sem pooling e remove somente
esse banco após concluir, inclusive em falhas. Não use credenciais de produção.
Se o processo for encerrado abruptamente, podem sobrar bancos de teste; confira
a origem antes de qualquer limpeza manual.

```powershell
$env:JOBPILOT_TEST_POSTGRES = 'Host=127.0.0.1;Port=5434;Database=postgres;Username=postgres;Password=<senha-do-cluster-de-teste>'
dotnet test apps/api.tests --configuration Release --filter Category=PostgreSQL
Remove-Item Env:JOBPILOT_TEST_POSTGRES
```

O job `postgres-smoke` de `.github/workflows/quality.yml` usa PostgreSQL 16 e
executa essa categoria antes de iniciar a API e verificar `/health/ready`.
Os resultados TRX são publicados mesmo em falhas. O backend sem a variável
continua executando os demais testes e informa explicitamente os ignorados.

Os cenários cobrem instalação vazia, atualização de
`20260602102000_AddApplicationJobResume` com dados em todas as tabelas afetadas,
rollback específico de respostas de revisão, reaplicação por EF e execução
dupla do script SQL idempotente. Comparam conteúdo completo dos registros,
referências, histórico, colunas, índices e constraints. O teste da infraestrutura
verifica que o banco gerado é diferente do fornecido e só ele é removido.

## Correção do histórico (A01 / ticket 02)

`20260602175220_AddResumeReviewAnswers` repetia objetos já pertencentes a
`AddResumeLibrary`, `AddJobTargets`, `AddResumeEvidenceMaps` e
`AddApplicationJobResume`. A instalação vazia falhava com SQLSTATE `42701`.
O ajuste conserva o ID e o target model da migração, cria apenas
`ResumeReviewAnswers` e seus dois índices e limita o Down a essa tabela.
Não usa `IF NOT EXISTS` para esconder divergências de esquema e não modifica
as relações do modelo atual. Os snapshots continuam descrevendo o modelo
cumulativo; as quatro migrações anteriores mantêm a propriedade dos objetos.

O rollback remove os dados de respostas de revisão, como esperado ao remover
a funcionalidade, mas preserva integralmente os objetos e dados anteriores.
Instalações com a migração já registrada não a executam novamente. Instalações
com histórico manipulado ou esquema divergente exigem diagnóstico e backup;
esses testes não validam históricos arbitrários nem autorizam marcar uma
migração como aplicada manualmente.

A execução real do SQL idempotente também revelou SQLSTATE `42601` em
`AddJobFilterRoleKeywords`: o UPDATE não terminava em `;` dentro do bloco
gerado por EF. Foi adicionado apenas o terminador, mantendo seu comportamento.

Verificações de modelo e geração sem conectar ao banco (configure as chaves
de desenvolvimento conforme o setup existente):

```powershell
dotnet ef migrations has-pending-model-changes --project apps/api --startup-project apps/api --configuration Release --no-build
dotnet ef migrations script --idempotent --project apps/api --startup-project apps/api --configuration Release --no-build --output .build/migrations.sql
```

Gerar SQL não prova que ele pode ser executado: a categoria PostgreSQL executa
o script completo em banco próprio. PostgreSQL 18 pode ser usado localmente,
mas não substitui a validação de PostgreSQL 16 no CI.
