# pedidos-mcp-sqlserver

Servidor [MCP](https://modelcontextprotocol.io) (stdio) **somente leitura** que conecta o Claude Code ao
SQL Server do projeto. Registrado em [`/.mcp.json`](../../.mcp.json) como `sqlserver`.

| Ferramenta | O que faz |
|---|---|
| `list_tables` | Tabelas e contagem de linhas. |
| `describe_table` | Colunas, índices (chave + `INCLUDE`) e FKs. |
| `query` | Executa **um** `SELECT`/`WITH` (máx. 200 linhas). |
| `explain` | Plano estimado (`SHOWPLAN_XML`) resumido: custo, Seek/Scan/Lookup, índices usados, avisos e índices faltantes. |

## Segurança — três camadas

1. **Validação** (`lib.mjs`): só `SELECT`/`WITH`, um statement, sem `INTO`/`EXEC`/DDL/`OPENROWSET`/`WAITFOR` (comentários e literais são removidos antes da análise).
2. **Transação sempre revertida** em toda execução de `query`.
3. **Login `mcp_readonly`**: `db_datareader` + `db_denydatawriter` + `SHOWPLAN`. Mesmo que 1 e 2 falhem, o banco nega a escrita (verificado: `UPDATE` → *Msg 229, permission denied*).

## Uso

```bash
docker compose up -d sqlserver db-init     # banco local em localhost,14333
cd tools/mcp-sqlserver && npm ci
npm test        # testes unitários da validação e do resumo do plano
npm run smoke   # chama as 4 ferramentas via protocolo MCP contra o banco real
```

No Claude Code: `/mcp` deve listar `sqlserver` como conectado.
