---
name: sql-query-review
description: Revisa e otimiza uma query T-SQL usando o MCP do SQL Server (schema real, dados reais, plano de execução estimado e estatísticas de I/O). Use quando o usuário pedir para analisar, depurar ou otimizar uma query, ou quando uma query estiver lenta/retornando resultado errado.
argument-hint: "<query SQL ou caminho de arquivo .sql>"
allowed-tools: mcp__sqlserver__list_tables, mcp__sqlserver__describe_table, mcp__sqlserver__query, mcp__sqlserver__explain, Read, Grep
---

# Skill: revisão de query SQL com o MCP

Entrada: `$ARGUMENTS`.

## 1. Entender o objetivo
Reescreva em uma frase o que a query **deveria** retornar. Divergência entre intenção e SQL é bug de corretude, que vem **antes** de performance.

## 2. Conferir o schema real (não confie na memória)
- `mcp__sqlserver__describe_table` para cada tabela citada: colunas, tipos, nulabilidade, índices e FKs.

## 3. Checklist de corretude
- [ ] `LEFT JOIN` anulado por filtro no `WHERE` sobre a tabela da direita (vira INNER JOIN implícito)?
- [ ] `GROUP BY` por coluna não única (ex.: nome) fundindo entidades diferentes?
- [ ] Direção do `ORDER BY` coerente com "ranking"/"top"?
- [ ] `SUM`/`COUNT` inflados por join 1:N antes da agregação (fan-out)?
- [ ] Tratamento de `NULL` (agregações sobre linhas sem correspondência)?
- [ ] Limites de período: semiaberto `>= inicio AND < fim` (evita perder o último dia com horário)?

## 4. Checklist de performance
- [ ] Funções sobre colunas no `WHERE`/`JOIN` (`YEAR(col)`, `CONVERT`, `ISNULL(col,..)`) → não-sargável.
- [ ] FKs sem índice; índices sem `INCLUDE` causando *Key Lookup*.
- [ ] `SELECT *` / colunas desnecessárias.
- [ ] Conversão implícita de tipo (ex.: `varchar` vs `nvarchar`).

## 5. Provar com dados
- Rode a query original e a corrigida via `mcp__sqlserver__query` com os **mesmos parâmetros** e compare as linhas (prove o bug com um caso concreto, ex.: clientes homônimos).
- Use `mcp__sqlserver__explain` nas duas: compare operadores (Scan vs Seek), custo estimado, *Key Lookups*, avisos.
- Se precisar de `SET STATISTICS IO/TIME`, gere o script em `database/queries/` para o usuário rodar com `sqlcmd` (o MCP é somente leitura e não roda DDL).

## 6. Entregar
1. Tabela "Problema → Impacto → Correção".
2. Query final comentada.
3. Índices sugeridos como script **idempotente** em `database/init/` (nunca execute DDL pelo MCP).
4. Números antes/depois (logical reads, tempo, operadores do plano).
