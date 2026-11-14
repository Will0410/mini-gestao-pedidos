// Smoke test ponta a ponta: sobe o servidor via stdio, como o Claude Code faz, e chama cada ferramenta.
// Requer o banco do docker compose rodando:  docker compose up -d sqlserver db-init  &&  npm run smoke
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { fileURLToPath } from 'node:url';

const server = fileURLToPath(new URL('../index.mjs', import.meta.url));
const client = new Client({ name: 'smoke', version: '1.0.0' });
await client.connect(new StdioClientTransport({ command: process.execPath, args: [server], env: { ...process.env } }));

const call = async (name, args = {}) => {
  const r = await client.callTool({ name, arguments: args });
  const body = r.content[0].text;
  console.log(`\n### ${name} ${r.isError ? '(erro esperado?)' : ''}\n${body.slice(0, 600)}`);
  return r;
};

console.log('Ferramentas:', (await client.listTools()).tools.map((t) => t.name).join(', '));
await call('list_tables');
await call('describe_table', { table: 'ItensPedido' });
await call('query', { sql: "SELECT Id, Nome, Email FROM dbo.Clientes WHERE Nome = N'Maria Silva'" });
await call('explain', { sql: 'SELECT ClienteId, SUM(ValorTotal) FROM dbo.Pedidos WHERE YEAR(DataPedido) = 2026 GROUP BY ClienteId' });
const blocked = await call('query', { sql: 'DELETE FROM dbo.Pedidos' });
if (!blocked.isError) throw new Error('DELETE deveria ter sido bloqueado');

await client.close();
console.log('\nSMOKE OK');
