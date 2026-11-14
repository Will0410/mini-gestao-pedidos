/* =====================================================================================
   PARTE 4 — Ranking dos clientes que mais compraram em 2026
   Diagnóstico completo no README (§ Parte 4). Rodar com:
     docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C \
       -d PedidosDb -i /dev/stdin < database/queries/parte4-ranking-2026.sql
   ===================================================================================== */
SET NOCOUNT ON;
GO

/* -------------------------------------------------------------------------------------
   1) QUERY ORIGINAL (com defeitos) — mantida para comparação
   ------------------------------------------------------------------------------------- */
-- SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
-- FROM Clientes c
-- LEFT JOIN Pedidos p ON p.ClienteId = c.Id
-- LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
-- WHERE YEAR(p.DataPedido) = 2026          -- (A) não-sargável  (B) anula o LEFT JOIN
-- GROUP BY c.Nome                           -- (C) funde clientes homônimos
-- ORDER BY Total                            -- (D) ascendente: o "ranking" sai invertido

/* -------------------------------------------------------------------------------------
   2) QUERY CORRIGIDA E OTIMIZADA
   - INNER JOIN: o objetivo é "quem comprou"; quem não comprou não entra no ranking.
     (Se o negócio quisesse listar todos com 0, o filtro de data iria para o ON e
      usaríamos COALESCE(SUM(...), 0) — ver variante 2b.)
   - Intervalo semiaberto [2026-01-01, 2027-01-01): sargável → Index Seek em
     IX_Pedidos_DataPedido e pega 31/12 23:59:59 sem gambiarra de '23:59:59.997'.
   - GROUP BY c.Id, c.Nome: Id é a identidade do cliente; Nome só para exibição.
   - ORDER BY Total DESC + desempate determinístico por Id.
   - RANK() dá a posição (empates recebem a mesma posição).
   ------------------------------------------------------------------------------------- */
SELECT
    RANK() OVER (ORDER BY SUM(i.Quantidade * i.PrecoUnitario) DESC) AS Posicao,
    c.Id                                   AS ClienteId,
    c.Nome,
    COUNT(DISTINCT p.Id)                   AS QtdPedidos,
    SUM(i.Quantidade * i.PrecoUnitario)    AS Total
FROM dbo.Pedidos p
INNER JOIN dbo.ItensPedido i ON i.PedidoId = p.Id
INNER JOIN dbo.Clientes c    ON c.Id = p.ClienteId
WHERE p.DataPedido >= '2026-01-01'
  AND p.DataPedido <  '2027-01-01'
GROUP BY c.Id, c.Nome
ORDER BY Total DESC, c.Id;
GO

/* 2b) Variante "todos os clientes, inclusive quem não comprou" (filtro no ON, não no WHERE). */
-- SELECT c.Id, c.Nome, COALESCE(SUM(i.Quantidade * i.PrecoUnitario), 0) AS Total
-- FROM dbo.Clientes c
-- LEFT JOIN dbo.Pedidos p ON p.ClienteId = c.Id
--                        AND p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'
-- LEFT JOIN dbo.ItensPedido i ON i.PedidoId = p.Id
-- GROUP BY c.Id, c.Nome
-- ORDER BY Total DESC, c.Id;

/* 2c) Variante usada pela API (GET /relatorios/top-clientes): soma Pedidos.ValorTotal
       (desnormalizado; pedidos são imutáveis) e lê SÓ o índice de cobertura
       IX_Pedidos_DataPedido (DataPedido) INCLUDE (ClienteId, ValorTotal). */
-- SELECT TOP (5) p.ClienteId, SUM(p.ValorTotal) AS Total
-- FROM dbo.Pedidos p
-- WHERE p.DataPedido >= '2026-01-01' AND p.DataPedido < '2027-01-01'
-- GROUP BY p.ClienteId
-- ORDER BY Total DESC, p.ClienteId;
