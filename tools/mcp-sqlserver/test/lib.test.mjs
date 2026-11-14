import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validateReadOnly, summarizePlan } from '../lib.mjs';

test('aceita SELECT e CTE simples', () => {
  assert.equal(validateReadOnly('SELECT TOP 5 * FROM dbo.Pedidos'), null);
  assert.equal(validateReadOnly('WITH x AS (SELECT 1 AS a) SELECT a FROM x;'), null);
});

test('aceita palavras proibidas dentro de literais e comentários', () => {
  assert.equal(validateReadOnly("SELECT * FROM dbo.Clientes WHERE Nome = 'DROP TABLE'"), null);
  assert.equal(validateReadOnly('-- delete everything\nSELECT 1'), null);
});

test('rejeita escrita, DDL e múltiplos statements', () => {
  for (const q of [
    'DELETE FROM dbo.Pedidos',
    'UPDATE dbo.Produtos SET Estoque = 0',
    'SELECT 1; DROP TABLE dbo.Pedidos',
    'SELECT * INTO dbo.Copia FROM dbo.Pedidos',
    "EXEC sp_executesql N'SELECT 1'",
    'WITH x AS (SELECT 1 a) DELETE FROM dbo.Pedidos',
    'SELECT * FROM OPENROWSET(BULK \'x\', SINGLE_CLOB) a',
    "SELECT 1 WAITFOR DELAY '00:00:10'",
    '',
  ]) {
    assert.notEqual(validateReadOnly(q), null, `deveria rejeitar: ${q}`);
  }
});

test('resume plano: operadores, custo e índice faltante', () => {
  const xml = `<ShowPlanXML><StmtSimple StatementSubTreeCost="1.25">
    <MissingIndexes><MissingIndexGroup Impact="87.5"><MissingIndex Database="[PedidosDb]" Schema="[dbo]" Table="[Pedidos]"></MissingIndex></MissingIndexGroup></MissingIndexes>
    <RelOp NodeId="0" PhysicalOp="Hash Match" EstimateRows="10"><RelOp NodeId="1" PhysicalOp="Index Scan" EstimateRows="20000">
    <Object Database="[PedidosDb]" Schema="[dbo]" Table="[Pedidos]" Index="[IX_Pedidos_DataPedido]" /></RelOp></RelOp></StmtSimple></ShowPlanXML>`;
  const s = summarizePlan(xml);
  assert.equal(s.custoEstimado, 1.25);
  assert.deepEqual(s.operadores, { 'Hash Match': 1, 'Index Scan': 1 });
  assert.deepEqual(s.objetosAcessados, ['Pedidos.IX_Pedidos_DataPedido']);
  assert.deepEqual(s.indicesFaltantes, [{ tabela: 'Pedidos', impactoEstimado: '87.5%' }]);
});
