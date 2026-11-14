#!/usr/bin/env node
/**
 * MCP server (stdio) SOMENTE LEITURA para o SQL Server do projeto.
 *
 * Por que um MCP próprio? (ver docs/plan.md A9)
 *  - ~200 linhas auditáveis, sem dependência de terceiros desconhecidos com acesso ao banco.
 *  - Três camadas de proteção: (1) validação de comando (só SELECT/WITH, um único statement);
 *    (2) execução dentro de transação sempre revertida; (3) login mcp_readonly com
 *    db_denydatawriter — mesmo que (1) e (2) falhem, o banco nega a escrita.
 *
 * Ferramentas: list_tables, describe_table, query, explain.
 */
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import sql from 'mssql';
import { z } from 'zod';
import { validateReadOnly, summarizePlan } from './lib.mjs';

const MAX_ROWS = Number(process.env.MCP_MAX_ROWS ?? 200);

const config = {
  server: process.env.MSSQL_HOST ?? 'localhost',
  port: Number(process.env.MSSQL_PORT ?? 14333),
  database: process.env.MSSQL_DATABASE ?? 'PedidosDb',
  user: process.env.MSSQL_USER ?? 'mcp_readonly',
  password: process.env.MSSQL_PASSWORD ?? 'Mcp@ReadOnly2026!',
  options: { encrypt: true, trustServerCertificate: true },
  requestTimeout: 15_000,
  pool: { max: 1, min: 0 }, // 1 conexão: SET SHOWPLAN_XML precisa da mesma sessão
};

let poolPromise;
const getPool = () => (poolPromise ??= new sql.ConnectionPool(config).connect());

// ---------- Execução (camada 2) ----------
async function runReadOnly(query) {
  const pool = await getPool();
  const tx = new sql.Transaction(pool);
  await tx.begin(sql.ISOLATION_LEVEL.READ_COMMITTED);
  try {
    const result = await new sql.Request(tx).query(query);
    return result.recordset ?? [];
  } finally {
    await tx.rollback(); // nunca persiste nada
  }
}

const text = (value) => ({ content: [{ type: 'text', text: typeof value === 'string' ? value : JSON.stringify(value, null, 2) }] });
const fail = (msg) => ({ content: [{ type: 'text', text: msg }], isError: true });

// ---------- Servidor ----------
const server = new McpServer({ name: 'pedidos-sqlserver', version: '1.0.0' });

server.registerTool(
  'list_tables',
  { description: 'Lista as tabelas do banco com contagem aproximada de linhas.', inputSchema: {} },
  async () => {
    try {
      return text(await runReadOnly(`
        SELECT s.name AS [schema], t.name AS tabela, SUM(p.rows) AS linhas
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
        GROUP BY s.name, t.name ORDER BY t.name`));
    } catch (e) { return fail(String(e.message ?? e)); }
  },
);

server.registerTool(
  'describe_table',
  {
    description: 'Mostra colunas, índices (com INCLUDE) e chaves estrangeiras de uma tabela.',
    inputSchema: { table: z.string().regex(/^[A-Za-z_][A-Za-z0-9_]{0,127}$/).describe('Nome da tabela, ex.: Pedidos') },
  },
  async ({ table }) => {
    try {
      const pool = await getPool();
      const req = () => pool.request().input('t', sql.NVarChar(128), table);
      const [cols, idx, fks] = await Promise.all([
        req().query(`SELECT c.name AS coluna, ty.name AS tipo, c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity
                     FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                     WHERE c.object_id = OBJECT_ID(N'dbo.' + @t) ORDER BY c.column_id`),
        req().query(`SELECT i.name AS indice, i.type_desc AS tipo, i.is_unique,
                       STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name + IIF(ic.is_descending_key = 1, ' DESC', '') END, ', ')
                         WITHIN GROUP (ORDER BY ic.key_ordinal) AS chave,
                       STRING_AGG(CASE WHEN ic.is_included_column = 1 THEN c.name END, ', ') AS include
                     FROM sys.indexes i
                     JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                     JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                     WHERE i.object_id = OBJECT_ID(N'dbo.' + @t)
                     GROUP BY i.name, i.type_desc, i.is_unique`),
        req().query(`SELECT fk.name AS fk, COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS coluna,
                       OBJECT_NAME(fk.referenced_object_id) AS referencia, fk.delete_referential_action_desc AS on_delete
                     FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                     WHERE fk.parent_object_id = OBJECT_ID(N'dbo.' + @t)`),
      ]);
      if (cols.recordset.length === 0) return fail(`Tabela dbo.${table} não encontrada.`);
      return text({ tabela: `dbo.${table}`, colunas: cols.recordset, indices: idx.recordset, chavesEstrangeiras: fks.recordset });
    } catch (e) { return fail(String(e.message ?? e)); }
  },
);

server.registerTool(
  'query',
  {
    description: `Executa um SELECT somente leitura (máx. ${MAX_ROWS} linhas retornadas). Comandos de escrita/DDL são rejeitados.`,
    inputSchema: { sql: z.string().min(1).max(20_000).describe('Um único SELECT ou WITH ... SELECT') },
  },
  async ({ sql: query }) => {
    const err = validateReadOnly(query);
    if (err) return fail(err);
    try {
      const rows = await runReadOnly(query);
      return text({ totalLinhas: rows.length, truncado: rows.length > MAX_ROWS, linhas: rows.slice(0, MAX_ROWS) });
    } catch (e) { return fail(String(e.message ?? e)); }
  },
);

server.registerTool(
  'explain',
  {
    description: 'Retorna o plano de execução ESTIMADO (SHOWPLAN_XML) de um SELECT, resumido: custo, operadores (Seek/Scan/Lookup), índices usados, avisos e índices faltantes. A query não é executada.',
    inputSchema: {
      sql: z.string().min(1).max(20_000),
      includeXml: z.boolean().optional().describe('Inclui o XML completo do plano'),
    },
  },
  async ({ sql: query, includeXml }) => {
    const err = validateReadOnly(query);
    if (err) return fail(err);
    const pool = await getPool();
    try {
      await pool.request().batch('SET SHOWPLAN_XML ON');
      const r = await pool.request().batch(query);
      const xml = Object.values(r.recordset?.[0] ?? {})[0] ?? '';
      const summary = summarizePlan(String(xml));
      return text(includeXml ? { ...summary, xml } : summary);
    } catch (e) {
      return fail(String(e.message ?? e));
    } finally {
      await pool.request().batch('SET SHOWPLAN_XML OFF').catch(() => {});
    }
  },
);

await server.connect(new StdioServerTransport());
