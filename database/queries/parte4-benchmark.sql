/* =====================================================================================
   PARTE 4 — Benchmark reproduzível: original x corrigida, com e sem índices adequados.
   Mede páginas lidas (STATISTICS IO) e tempo (STATISTICS TIME). Rodar como sa:
     ./scripts/parte4-benchmark.sh
   "Sem índices" é simulado com hints que forçam o índice clusterizado (equivale a não
   existir IX_Pedidos_DataPedido nem UX_ItensPedido_PedidoId_ProdutoId).
   ===================================================================================== */
SET NOCOUNT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
GO

PRINT '======== [1] ORIGINAL (índices atuais) ========';
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total;
GO

PRINT '======== [2] ORIGINAL (sem índices nas FKs / data) ========';
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p WITH (INDEX(PK_Pedidos)) ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i WITH (INDEX(PK_ItensPedido)) ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total;
GO

PRINT '======== [3] CORRIGIDA (índices atuais) ========';
SELECT RANK() OVER (ORDER BY SUM(i.Quantidade * i.PrecoUnitario) DESC) AS Posicao,
       c.Id AS ClienteId, c.Nome, COUNT(DISTINCT p.Id) AS QtdPedidos,
       SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM dbo.Pedidos p
INNER JOIN dbo.ItensPedido i ON i.PedidoId = p.Id
INNER JOIN dbo.Clientes c ON c.Id = p.ClienteId
WHERE p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'
GROUP BY c.Id, c.Nome
ORDER BY Total DESC, c.Id;
GO

PRINT '======== [4] VARIANTE DA API: soma ValorTotal (índice de cobertura) ========';
SELECT TOP (5) p.ClienteId, SUM(p.ValorTotal) AS Total
FROM dbo.Pedidos p
WHERE p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'
GROUP BY p.ClienteId
ORDER BY Total DESC, p.ClienteId;
GO

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
GO

PRINT '======== Planos (operador de acesso a Pedidos) ========';
GO
SET SHOWPLAN_TEXT ON;
GO
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c LEFT JOIN Pedidos p ON p.ClienteId = c.Id LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026 GROUP BY c.Nome ORDER BY Total;
GO
SELECT c.Id, c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM dbo.Pedidos p JOIN dbo.ItensPedido i ON i.PedidoId = p.Id JOIN dbo.Clientes c ON c.Id = p.ClienteId
WHERE p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01' GROUP BY c.Id, c.Nome ORDER BY Total DESC, c.Id;
GO
SET SHOWPLAN_TEXT OFF;
GO
