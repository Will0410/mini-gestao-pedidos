/* 02 - Carga de dados de exemplo (idempotente: só carrega se não houver clientes).
   Determinística (sem RAND/NEWID): toda execução gera exatamente os mesmos dados,
   o que torna reprodutíveis os números da Parte 4 e os testes manuais.

   Casos propositais para a Parte 4 (debug da query):
     - Clientes 1 e 2 são HOMÔNIMOS ("Maria Silva") -> expõe o bug do GROUP BY c.Nome.
     - Cliente 23 só tem pedidos em 2025 -> não deve aparecer no ranking de 2026.
     - Clientes 24 e 25 nunca compraram; 25 está inativo.
   Volume: 20.000 pedidos (~50.000 itens) entre 2025-01-01 e 2026-11-13. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF EXISTS (SELECT 1 FROM dbo.Clientes)
BEGIN
    PRINT 'Seed ignorado: dados já existem.';
    RETURN;
END

BEGIN TRANSACTION;

SET IDENTITY_INSERT dbo.Clientes ON;
INSERT INTO dbo.Clientes (Id, Nome, Email, Ativo, DataCadastro) VALUES
 ( 1, N'Maria Silva',        N'maria.silva@exemplo.com',      1, '2024-01-10'),
 ( 2, N'Maria Silva',        N'maria.silva2@exemplo.com',     1, '2024-02-11'),
 ( 3, N'João Pereira',       N'joao.pereira@exemplo.com',     1, '2024-03-05'),
 ( 4, N'Ana Souza',          N'ana.souza@exemplo.com',        1, '2024-03-18'),
 ( 5, N'Bruno Lima',         N'bruno.lima@exemplo.com',       1, '2024-04-02'),
 ( 6, N'Carla Mendes',       N'carla.mendes@exemplo.com',     1, '2024-04-20'),
 ( 7, N'Diego Rocha',        N'diego.rocha@exemplo.com',      1, '2024-05-07'),
 ( 8, N'Eduarda Alves',      N'eduarda.alves@exemplo.com',    1, '2024-05-25'),
 ( 9, N'Felipe Costa',       N'felipe.costa@exemplo.com',     1, '2024-06-14'),
 (10, N'Gabriela Martins',   N'gabriela.martins@exemplo.com', 1, '2024-07-01'),
 (11, N'Henrique Barbosa',   N'henrique.barbosa@exemplo.com', 1, '2024-07-19'),
 (12, N'Isabela Ribeiro',    N'isabela.ribeiro@exemplo.com',  1, '2024-08-03'),
 (13, N'Lucas Carvalho',     N'lucas.carvalho@exemplo.com',   1, '2024-08-22'),
 (14, N'Mariana Gomes',      N'mariana.gomes@exemplo.com',    1, '2024-09-09'),
 (15, N'Nicolas Teixeira',   N'nicolas.teixeira@exemplo.com', 1, '2024-09-30'),
 (16, N'Olívia Fernandes',   N'olivia.fernandes@exemplo.com', 1, '2024-10-12'),
 (17, N'Paulo Araújo',       N'paulo.araujo@exemplo.com',     1, '2024-10-28'),
 (18, N'Rafaela Cardoso',    N'rafaela.cardoso@exemplo.com',  1, '2024-11-15'),
 (19, N'Samuel Nunes',       N'samuel.nunes@exemplo.com',     1, '2024-12-01'),
 (20, N'Tatiane Moreira',    N'tatiane.moreira@exemplo.com',  1, '2024-12-19'),
 (21, N'Vinícius Freitas',   N'vinicius.freitas@exemplo.com', 1, '2025-01-06'),
 (22, N'Yasmin Castro',      N'yasmin.castro@exemplo.com',    1, '2025-01-24'),
 (23, N'Rodrigo Pinto',      N'rodrigo.pinto@exemplo.com',    1, '2025-02-10'),
 (24, N'Sem Compras Ltda',   N'sem.compras@exemplo.com',      1, '2025-03-01'),
 (25, N'Cliente Inativo',    N'inativo@exemplo.com',          0, '2025-03-15');
SET IDENTITY_INSERT dbo.Clientes OFF;

SET IDENTITY_INSERT dbo.Produtos ON;
INSERT INTO dbo.Produtos (Id, Sku, Nome, Preco, Estoque, Ativo) VALUES
 ( 1, 'NB-001',  N'Notebook 14"',             4299.90, 100000, 1),
 ( 2, 'NB-002',  N'Notebook 16" Pro',         7899.00, 100000, 1),
 ( 3, 'MN-001',  N'Monitor 24" Full HD',       899.90, 100000, 1),
 ( 4, 'MN-002',  N'Monitor 27" 4K',           2199.00, 100000, 1),
 ( 5, 'TC-001',  N'Teclado Mecânico',          349.90, 100000, 1),
 ( 6, 'MS-001',  N'Mouse sem fio',             129.90, 100000, 1),
 ( 7, 'HS-001',  N'Headset USB',               299.00, 100000, 1),
 ( 8, 'WC-001',  N'Webcam Full HD',            249.90, 100000, 1),
 ( 9, 'SS-001',  N'SSD 1TB NVMe',              549.90, 100000, 1),
 (10, 'SS-002',  N'SSD 2TB NVMe',              999.90, 100000, 1),
 (11, 'RM-001',  N'Memória 16GB DDR5',         429.90, 100000, 1),
 (12, 'RM-002',  N'Memória 32GB DDR5',         819.90, 100000, 1),
 (13, 'HB-001',  N'Hub USB-C 7 em 1',          219.90, 100000, 1),
 (14, 'CB-001',  N'Cabo HDMI 2.1 2m',           59.90, 100000, 1),
 (15, 'DK-001',  N'Dock Station Thunderbolt', 1499.00, 100000, 1),
 (16, 'CD-001',  N'Cadeira Ergonômica',       1899.00, 100000, 1),
 (17, 'MS-002',  N'Mesa Regulável',           2499.00, 100000, 1),
 (18, 'IM-001',  N'Impressora Laser',         1199.00, 100000, 1),
 (19, 'RT-001',  N'Roteador Wi-Fi 6',          699.90, 100000, 1),
 (20, 'NB-003',  N'Nobreak 1500VA',            999.00, 100000, 1),
 (21, 'TB-001',  N'Tablet 11"',               2799.00, 100000, 1),
 (22, 'SP-001',  N'Smartphone 128GB',         2499.90, 100000, 1),
 (23, 'CX-001',  N'Caixa de Som Bluetooth',    399.90, 100000, 1),
 (24, 'PD-001',  N'Pendrive 128GB',             79.90, 100000, 1),
 (25, 'HD-001',  N'HD Externo 2TB',            529.90, 100000, 1),
 (26, 'MP-001',  N'Mousepad Grande',            89.90, 100000, 1),
 (27, 'SU-001',  N'Suporte para Notebook',     149.90, 100000, 1),
 (28, 'LM-001',  N'Luminária LED de Mesa',     189.90, 100000, 1),
 -- Estoque baixo: bom para demonstrar ESTOQUE_INSUFICIENTE no frontend.
 (29, 'ED-001',  N'Edição Limitada (3 un.)',   999.99,      3, 1),
 -- Inativo: não aparece em GET /produtos e é rejeitado no POST.
 (30, 'OLD-001', N'Produto Descontinuado',      10.00,    500, 0);
SET IDENTITY_INSERT dbo.Produtos OFF;

/* Tabela de números 1..20000 gerada por cross join (set-based, sem loop). */
;WITH E1(n) AS (SELECT 1 FROM (VALUES (1),(1),(1),(1),(1),(1),(1),(1),(1),(1)) v(n)),
      E4(n) AS (SELECT 1 FROM E1 a CROSS JOIN E1 b CROSS JOIN E1 c CROSS JOIN E1 d),
      E5(n) AS (SELECT 1 FROM E4 a CROSS JOIN E1 b),
      Numeros(n) AS (SELECT TOP (20000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) FROM E5)
SELECT n INTO #Numeros FROM Numeros;

/* Datas espalhadas de forma determinística (multiplicação por primo módulo nº de minutos do período).
   n * 104729 <= 2.094.580.000 < 2^31, então não há overflow de INT. */
SET IDENTITY_INSERT dbo.Pedidos ON;
INSERT INTO dbo.Pedidos (Id, ClienteId, DataPedido, ValorTotal)
SELECT
    n.n,
    CASE
        WHEN n.n % 50 = 0 THEN 23                                  -- 2% dos pedidos: cliente 23
        WHEN n.n % 7  = 0 THEN 1 + (n.n % 3)                       -- clientes 1..3 compram mais
        ELSE 1 + ((n.n * 7919) % 22)                               -- clientes 1..22
    END,
    CASE
        WHEN n.n % 50 = 0
            THEN DATEADD(MINUTE, (n.n * 104729) % 524160, '2025-01-01')      -- cliente 23: só 2025
        ELSE DATEADD(MINUTE, (n.n * 104729) % 980639, '2025-01-01')          -- até 2026-11-13 (681 dias)
    END,
    0
FROM #Numeros n;
SET IDENTITY_INSERT dbo.Pedidos OFF;

/* 1 a 4 itens por pedido; (n*31 + k*17) % 28 gera produtos distintos para k = 1..4. */
INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario)
SELECT p.Id, pr.Id, ((p.Id + k.k) % 5) + 1, pr.Preco
FROM dbo.Pedidos p
CROSS JOIN (VALUES (1),(2),(3),(4)) k(k)
JOIN dbo.Produtos pr ON pr.Id = ((p.Id * 31 + k.k * 17) % 28) + 1
WHERE k.k <= (p.Id % 4) + 1;

UPDATE p
SET ValorTotal = t.Total
FROM dbo.Pedidos p
JOIN (SELECT PedidoId, SUM(Quantidade * PrecoUnitario) AS Total
      FROM dbo.ItensPedido GROUP BY PedidoId) t ON t.PedidoId = p.Id;

DROP TABLE #Numeros;

COMMIT TRANSACTION;

DECLARE @qtdPedidos INT = (SELECT COUNT(*) FROM dbo.Pedidos),
        @qtdItens   INT = (SELECT COUNT(*) FROM dbo.ItensPedido);
PRINT CONCAT('Seed concluído: ', @qtdPedidos, ' pedidos, ', @qtdItens, ' itens.');
GO
