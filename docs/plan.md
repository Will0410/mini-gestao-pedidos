# Plano de implementação

> Gerado com o Claude Code em **plan mode** a partir de [`spec.md`](spec.md) e revisado por mim antes de
> qualquer linha de código. A seção 1 é o plano proposto pelo Claude (resumido); a seção 2 registra o que
> **ajustei ou discordei, e por quê**; a seção 3 é o plano final executado, na ordem dos commits.

---

## 1. Plano proposto pelo Claude (versão original, resumida)

1. Criar solução .NET 8 com um único projeto Web API (controllers) + projeto de testes.
2. Usar **EF Core Migrations** para criar o schema e `HasData` para dados de exemplo.
3. Entidades anêmicas; regras de pedido dentro do `PedidosController`.
4. Validar estoque lendo o produto, comparando `Estoque >= Quantidade` e salvando com `SaveChanges`.
5. Testes de integração com **EF Core InMemory** provider.
6. Frontend React + Vite com Axios e estado local (`useState`) para filtros.
7. Docker Compose com SQL Server, API (que aplica migrations no startup) e frontend em `vite preview`.
8. MCP: usar um servidor MCP de SQL Server da comunidade via `npx`, conectado com o usuário `sa`.
9. Query da Parte 4: trocar `ORDER BY Total` por `ORDER BY Total DESC`.

## 2. Ajustes e discordâncias (revisão humana)

| # | Proposta original | Decisão final | Por quê |
|---|---|---|---|
| A1 | EF Migrations + `HasData` | **Scripts SQL versionados** em `database/init` executados por um container `db-init` (sqlcmd); EF só mapeia. | O teste exige scripts de criação e carga na pasta `database/`. Ter duas fontes de schema (migrations + scripts) gera drift. Scripts também permitem índices com `INCLUDE`/`CHECK` explícitos e uma carga volumosa set-based (20k pedidos) para a Parte 4. |
| A2 | Um único projeto com regras no controller | **4 projetos** (Domain, Application, Infrastructure, Api) + Minimal APIs | Regras de negócio precisam de testes unitários **sem banco**. Com o domínio isolado, `Pedido.Criar(...)` e o `CriarPedidoHandler` são testados com fakes em memória. Minimal APIs reduzem cerimônia e combinam com o padrão de endpoints da skill. |
| A3 | Ler estoque → comparar → `SaveChanges` | **`UPDATE ... SET Estoque = Estoque - @q WHERE Id = @id AND Estoque >= @q`** dentro de transação, checando linhas afetadas. | A versão original tem **race condition** (check-then-act): duas requisições leem estoque 1 e ambas vendem. O UPDATE condicional é atômico no SQL Server (lock de linha). Coberto pelo CA-07 (20 requisições paralelas). A checagem prévia continua existindo só para dar uma mensagem amigável. |
| A4 | Testes de integração com EF InMemory | **Testcontainers + SQL Server real**, aplicando os mesmos scripts de `database/init`. | O teste exige banco real; InMemory não tem transações, constraints, `CHECK`, nem o comportamento de lock que o CA-07 valida. Aplicar os scripts reais também testa os próprios scripts. |
| A5 | Exceções para erros de negócio | **Result pattern** (`Result<T>` + `Error` com código) mapeado para Problem Details. | Erro de negócio não é excepcional; exceções como fluxo de controle custam caro e escondem o contrato. O `code` estável (`ESTOQUE_INSUFICIENTE`…) deixa o frontend tratar cada caso. |
| A6 | Cliente envia `precoUnitario` | Preço vem **sempre** do cadastro (RN-07). | Confiar em preço vindo do cliente é falha de segurança clássica (tampering). |
| A7 | Axios + `useState` | **fetch tipado + TanStack Query**; filtros na **URL** (`useSearchParams`). | Menos dependências; cache, loading/erro e invalidação após criar pedido de graça; filtro compartilhável por link. |
| A8 | Frontend com `vite preview` | Build estático servido por **nginx**, que faz proxy de `/api` para a API. | `vite preview` não é servidor de produção. Com proxy, não há CORS no navegador e é possível aplicar headers de segurança. |
| A9 | MCP da comunidade conectado como `sa` | **MCP próprio** (`tools/mcp-sqlserver`, Node + SDK oficial) conectado com login **somente leitura** e só aceitando `SELECT`. | Dar `sa` a um agente é risco real (um `DROP` alucinado apaga o banco). Um MCP pequeno e auditável, com allowlist de comandos e login `db_datareader`, aplica defesa em profundidade: mesmo que a validação falhe, o banco nega a escrita. |
| A10 | Corrigir só o `ORDER BY` | Corrigidos **5 problemas** (ordenação, `GROUP BY` por nome, `LEFT JOIN` anulado pelo `WHERE`, `YEAR()` não-sargável, índices ausentes) com medição de antes/depois. | Ver README §Parte 4. O seed inclui de propósito dois clientes homônimos para demonstrar o bug do `GROUP BY c.Nome`. |
| A11 | (não previsto) | Adicionados **k6** (carga), **Playwright** (E2E) e **GitHub Actions**. | Diferenciais do teste e validação do RNF-02. |
| A12 | API aplica migrations no startup | API **não** altera schema; `depends_on: db-init (service_completed_successfully)`. | Separar responsabilidades; API com login de menor privilégio não deveria ter permissão de DDL. |

### 2.1 Ajustes feitos durante a execução (não previstos no plano)

| # | Situação encontrada | Decisão | Por quê |
|---|---|---|---|
| E1 | O plano dizia .NET 8, mas o suporte do .NET 8 terminou em **10/11/2026** (4 dias antes da entrega). | **.NET 10 (LTS)**. | O teste aceita ".NET 8 ou superior"; entregar em runtime sem patches de segurança seria um erro. |
| E2 | xUnit v3 + SDK 10 exige Microsoft.Testing.Platform. | Opt-in em `global.json`; `dotnet test --project`. | Erro de build explícito do SDK; documentado no CLAUDE.md para o Claude não "voltar" ao comando antigo. |
| E3 | A máquina de desenvolvimento (corporativa) tem Windows Application Control, que bloqueou executáveis de teste recém-compilados. | `scripts/test-backend-docker.sh` roda a suíte num container do SDK, usando o SQL do compose. | Problema de ambiente, não de código. O script também serve para quem avalia sem SDK instalado. |
| E4 | Revisão de segurança achou 3 falhas que geravam 500 ou anulavam proteções (rate limit atrás do proxy, overflow de página, overflow de data). | Testes reproduzindo cada uma **antes** da correção; RN-12/RN-13 atualizadas na spec. | Spec continua sendo a fonte da verdade; os limites viraram regra explícita. |
| E5 | `OrderBy` após projeção para record no EF não é traduzível (500 em `GET /pedidos/{id}`). | Ordenar antes da projeção. | Pego pelo teste de integração — o smoke manual não cobria a rota. |

## 3. Plano final (ordem de execução = histórico de commits)

### Fase 0 — Fundação
- [x] Repositório, `.gitignore`, `.editorconfig`, `.env.example`
- [x] `docs/spec.md` e `docs/plan.md`
- [x] `CLAUDE.md` + `.claude/` (settings, hooks, skills, agente revisor) + `.mcp.json`

### Fase 1 — Banco
- [x] `01-schema.sql` (tabelas, PK/FK, CHECKs, índices), `02-seed.sql` (carga determinística), `03-logins.sql` (app e MCP)
- [x] Container `db-init` que espera o SQL Server ficar saudável e roda os scripts (idempotentes)
- [x] MCP `tools/mcp-sqlserver` (read-only)

### Fase 2 — Backend
- [x] Domain: `Cliente`, `Produto`, `Pedido`, `ItemPedido`, `Result`, `Error`
- [x] Application: contratos, validadores (FluentValidation), `CriarPedidoHandler`, queries
- [x] Infrastructure: `PedidosDbContext`, repositórios, baixa atômica de estoque, `UnitOfWork`
- [x] Api: endpoints, Problem Details, rate limiting, CORS, health check, Swagger

### Fase 3 — Testes backend
- [x] Unitários: domínio, validadores, handler (CA-02..06, 08, 11)
- [x] Integração: Testcontainers + scripts reais (CA-01, 03, 05, 07, 09..13)

### Fase 4 — Frontend
- [x] Vite + React + TS, cliente de API tipado, listagem com filtros/paginação, modal de criação, painel top clientes

### Fase 5 — Entrega
- [x] Dockerfiles multi-stage (API não-root, nginx), `docker-compose.yml` completo
- [x] Parte 4: script de diagnóstico com `STATISTICS IO/TIME` antes/depois
- [x] Playwright E2E (CA-14), GitHub Actions (CA-15), k6 (RNF-02)
- [x] README

## 4. Riscos identificados no planejamento

| Risco | Mitigação |
|---|---|
| SQL Server demora a subir → API falha no start | `healthcheck` com `sqlcmd` + `depends_on: condition: service_completed_successfully` no `db-init`. |
| Scripts rodando duas vezes (restart) | Scripts idempotentes (`IF NOT EXISTS`/`IF OBJECT_ID(...) IS NULL`; seed só se a tabela estiver vazia). |
| Testcontainers lento no CI | Um único container por coleção de testes (`ICollectionFixture`); dados isolados por teste. |
| Imagem do SQL Server não roda em ARM (Mac M1) | Documentado no README (Rosetta/`platform: linux/amd64`). |
| Código gerado por IA sem revisão | Hook roda build/testes após edições; subagente `code-reviewer`; revisão manual registrada no README. |
