# Política de segurança

## Versões suportadas

| Componente | Versão | Suporte |
|---|---|---|
| .NET | 10 (LTS) | até nov/2028 |
| Node.js (build do frontend) | 24 (LTS) | até abr/2028 |
| SQL Server | 2022 (`-latest`: recebe os CUs) | — |

## Como reportar uma vulnerabilidade

Não abra issue pública. Use **Security → Report a vulnerability** neste repositório (aviso privado) ou
fale diretamente com o mantenedor. Inclua passos para reproduzir e o impacto esperado.

## Controles em vigor

- **Dependências e código**: Dependabot (atualizações + alertas de segurança), CodeQL (SAST de C# e TypeScript), secret scanning com push protection; o CI falha com pacote NuGet
  High/Critical e com `npm audit` de produção ≥ high.
- **Código**: consultas parametrizadas (EF Core), validação de entrada com limites, Problem Details sem stack
  trace, rate limit no POST, CORS restrito, headers de segurança (CSP, `X-Frame-Options`, `nosniff`).
- **Banco**: princípio do menor privilégio — a API usa `pedidos_app` (sem DDL); o MCP usa `mcp_readonly`
  com `db_denydatawriter`.
- **Containers**: API como usuário não-root; imagens finais sem SDK/node.
- **Agente de IA**: hook `guard.mjs` bloqueia comandos destrutivos e edição de `.env`.

Detalhes e riscos aceitos: [README §5](README.md#5-segurança).
