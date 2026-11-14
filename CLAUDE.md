# CLAUDE.md — Mini Gestão de Pedidos

Regras do projeto para o Claude Code. Leia antes de qualquer alteração.
A especificação é a fonte da verdade: [`docs/spec.md`](docs/spec.md). Plano: [`docs/plan.md`](docs/plan.md).

## Fluxo de trabalho obrigatório (SDD)

1. Mudança de comportamento → **atualize `docs/spec.md` primeiro** (regra `RN-xx` e critério `CA-xx`).
2. Use **plan mode** para mudanças que tocam mais de um projeto/camada.
3. Escreva/atualize o teste que referencia o `CA-xx` **antes** ou junto do código.
4. Rode build + testes (o hook já roda o build após edições; rode os testes antes de commitar).
5. Commits pequenos, em português, no padrão Conventional Commits: `feat(api): ...`, `fix(db): ...`,
   `test(backend): ...`, `docs: ...`, `chore(docker): ...`. **Nunca** reescreva histórico (sem squash/force push).

## Arquitetura

```
database/init/        Scripts SQL (fonte da verdade do schema). Executados em ordem numérica pelo db-init.
database/queries/     Queries de diagnóstico (Parte 4).
backend/src/
  Pedidos.Domain/          Entidades + regras puras. SEM dependência de EF, ASP.NET ou pacotes externos.
  Pedidos.Application/     Casos de uso (handlers), contratos (DTOs), validadores FluentValidation, interfaces de repositório.
  Pedidos.Infrastructure/  EF Core (DbContext, mapeamentos, repositórios, UnitOfWork). Implementa interfaces da Application.
  Pedidos.Api/             Minimal APIs (Endpoints/*.cs), Problem Details, DI, rate limit, CORS, health.
backend/tests/
  Pedidos.UnitTests/         xUnit, sem banco, fakes em memória (tests/Fakes).
  Pedidos.IntegrationTests/  xUnit + Testcontainers (SQL Server real) + WebApplicationFactory.
frontend/             React 19 + TypeScript + Vite + TanStack Query. src/api (cliente tipado), src/features/*.
frontend/e2e/         Playwright.
tools/mcp-sqlserver/  Servidor MCP somente leitura para o SQL Server.
tests/load/           Scripts k6.
```

Dependências entre camadas: `Api → Application → Domain` e `Api → Infrastructure → Application`.
**Domain não referencia ninguém.** Nunca use `DbContext` dentro de Application ou Domain.

## Convenções de código

### C#
- Idioma do domínio: **português** (`Pedido`, `ItemPedido`, `ValorTotal`); termos técnicos em inglês (`Handler`, `Repository`, `Endpoint`).
- File-scoped namespaces; `sealed` por padrão; `record` para DTOs/requests/responses; nullable habilitado; warnings = erros.
- Nomes: `PascalCase` para tipos/métodos/propriedades, `_camelCase` para campos privados, `camelCase` para locais, sufixo `Async` em métodos assíncronos.
- Erros de negócio: **retorne `Result`/`Result<T>` com `Error`** (`Errors.Pedido.EstoqueInsuficiente(...)`, catálogo em `Domain/Errors.cs`). Não lance exceção para regra de negócio.
- Testes usam xUnit v3 sobre Microsoft.Testing.Platform (opt-in em `backend/global.json`): rode com `dotnet test --project <csproj>`.
- Códigos de erro (`Error.Code`) são contrato com o frontend: `VALIDACAO`, `CLIENTE_NAO_ENCONTRADO`, `PRODUTO_NAO_ENCONTRADO`, `ESTOQUE_INSUFICIENTE`, `NAO_ENCONTRADO`.
- Mapeamento HTTP: `Validation→400`, `NotFound→404`, `Unprocessable→422`, `Conflict→409` (ver `Api/Http/ResultExtensions.cs`).
- Endpoints: um arquivo por agrupamento em `Api/Endpoints/` com `MapXxxEndpoints(this IEndpointRouteBuilder)`; sempre `.WithName()`, `.WithTags()`, `.Produces...()`.
- Queries de leitura: `AsNoTracking()` + projeção direta para DTO (`Select`), nunca carregar entidade inteira para listar.
- Toda escrita que envolve estoque passa por `IProdutoRepository.TentarBaixarEstoqueAsync` (UPDATE condicional atômico) dentro de `IUnitOfWork.ExecutarEmTransacaoAsync`.
- Datas sempre em UTC (`TimeProvider` injetado — não use `DateTime.Now`).

### SQL
- Tabelas no plural, PascalCase, schema `dbo`. PK `Id`. FK `<Entidade>Id`.
- Constraints nomeadas: `PK_Tabela`, `FK_Filha_Pai`, `CK_Tabela_Coluna`, `UX_` (único), `IX_` (não único).
- Todo FK tem índice. Scripts idempotentes. Nunca use `YEAR()`/funções em colunas no `WHERE` (não-sargável) — use intervalo semiaberto `>= inicio AND < fim`.

### TypeScript / React
- `strict` ligado; sem `any`. Tipos da API em `src/api/types.ts` espelham os DTOs do backend.
- Componentes `PascalCase.tsx`, hooks `useXxx.ts`, um componente por arquivo.
- Acesso a dados só via hooks do TanStack Query em `src/features/**/queries.ts`.
- Atributos `data-testid` em elementos usados pelo Playwright.

## Comandos

| Ação | Comando |
|---|---|
| Subir tudo | `docker compose up --build` (front: http://localhost:8080 · API: http://localhost:5080/swagger) |
| Só o banco (dev local) | `docker compose up -d sqlserver db-init` |
| Build backend | `dotnet build backend/Pedidos.sln` (.NET 10 SDK) |
| Testes unitários | `cd backend && dotnet test --project tests/Pedidos.UnitTests` |
| Testes de integração | `cd backend && dotnet test --project tests/Pedidos.IntegrationTests` (requer Docker; Testcontainers) |
| Todos os testes backend sem SDK local | `./scripts/test-backend-docker.sh` (roda em container, usa o SQL do compose) |
| Benchmark da Parte 4 | `./scripts/parte4-benchmark.sh` |
| API local | `dotnet run --project backend/src/Pedidos.Api` (usa SQL em `localhost,14333`) |
| Frontend dev | `cd frontend && npm install && npm run dev` (proxy `/api` → `localhost:5080`) |
| Lint/typecheck front | `cd frontend && npm run lint && npm run typecheck` |
| Testes unitários front | `cd frontend && npm test` |
| E2E | `docker compose up -d --build && cd frontend && npx playwright test` |
| Carga | `docker run --rm -i --network host grafana/k6 run - < tests/load/pedidos.k6.js` |

## Ferramentas do Claude neste repo

- **Hooks** (`.claude/settings.json`):
  - `PostToolUse` (Edit/Write) → `.claude/hooks/post-edit-check.mjs`: compila o backend ao editar `.cs` e faz typecheck ao editar `.ts/.tsx`; erros voltam para o Claude corrigir.
  - `PreToolUse` (Bash/Edit/Write) → `.claude/hooks/guard.mjs`: bloqueia comandos destrutivos (`git push --force`, `git reset --hard`, `DROP DATABASE`, `rm -rf /`) e edição de `.env`.
- **Skills** (`.claude/skills/`): `new-endpoint` (cria endpoint ponta a ponta no padrão), `sql-query-review` (analisa query via MCP: plano, sargabilidade, índices).
- **Subagente** (`.claude/agents/code-reviewer.md`): revisão crítica antes de commit.
- **MCP** (`.mcp.json` → `sqlserver`): consultas **somente leitura** no banco local. Use para conferir schema, dados de seed e planos de execução — nunca para alterar dados (o login nem tem permissão).

## Não faça

- Não altere schema pelo EF (`EnsureCreated`/migrations). Schema muda em `database/init/*.sql`.
- Não exponha entidades de domínio na API; use DTOs.
- Não aceite preço do cliente da API.
- Não use `sa` em connection strings da aplicação.
- Não adicione pacotes sem justificar no commit.
