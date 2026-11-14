/* 01 - Schema: tabelas, chaves, constraints e índices (idempotente).
   Convenções: PK_Tabela, FK_Filha_Pai, CK_Tabela_Coluna, UX_ = índice único, IX_ = índice não único.
   Todo FK possui índice (o SQL Server NÃO cria índice para FK automaticamente). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes
    (
        Id            INT            IDENTITY(1,1) NOT NULL,
        Nome          NVARCHAR(150)  NOT NULL,
        Email         NVARCHAR(254)  NOT NULL,
        Ativo         BIT            NOT NULL CONSTRAINT DF_Clientes_Ativo DEFAULT (1),
        DataCadastro  DATETIME2(0)   NOT NULL CONSTRAINT DF_Clientes_DataCadastro DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Clientes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Clientes_Nome CHECK (LEN(Nome) > 0)
    );
    CREATE UNIQUE INDEX UX_Clientes_Email ON dbo.Clientes (Email);
END
GO

IF OBJECT_ID(N'dbo.Produtos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Produtos
    (
        Id       INT            IDENTITY(1,1) NOT NULL,
        Sku      VARCHAR(30)    NOT NULL,
        Nome     NVARCHAR(150)  NOT NULL,
        Preco    DECIMAL(18,2)  NOT NULL,
        Estoque  INT            NOT NULL,
        Ativo    BIT            NOT NULL CONSTRAINT DF_Produtos_Ativo DEFAULT (1),
        CONSTRAINT PK_Produtos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Produtos_Preco   CHECK (Preco >= 0),
        -- Última barreira contra estoque negativo, mesmo se a aplicação falhar.
        CONSTRAINT CK_Produtos_Estoque CHECK (Estoque >= 0)
    );
    CREATE UNIQUE INDEX UX_Produtos_Sku ON dbo.Produtos (Sku);
END
GO

IF OBJECT_ID(N'dbo.Pedidos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Pedidos
    (
        Id          INT            IDENTITY(1,1) NOT NULL,
        ClienteId   INT            NOT NULL,
        DataPedido  DATETIME2(0)   NOT NULL CONSTRAINT DF_Pedidos_DataPedido DEFAULT (SYSUTCDATETIME()),
        -- Desnormalizado de propósito: pedidos são imutáveis (ver docs/spec.md §4.2).
        ValorTotal  DECIMAL(18,2)  NOT NULL,
        CONSTRAINT PK_Pedidos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Pedidos_Clientes FOREIGN KEY (ClienteId) REFERENCES dbo.Clientes (Id),
        CONSTRAINT CK_Pedidos_ValorTotal CHECK (ValorTotal >= 0)
    );

    -- Listagem filtrada por cliente (+ período). Também serve de índice do FK.
    CREATE INDEX IX_Pedidos_ClienteId_DataPedido
        ON dbo.Pedidos (ClienteId, DataPedido DESC)
        INCLUDE (ValorTotal);

    -- Listagem só por período e relatório top-clientes: índice de cobertura (sem Key Lookup).
    CREATE INDEX IX_Pedidos_DataPedido
        ON dbo.Pedidos (DataPedido DESC)
        INCLUDE (ClienteId, ValorTotal);
END
GO

IF OBJECT_ID(N'dbo.ItensPedido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItensPedido
    (
        Id             INT            IDENTITY(1,1) NOT NULL,
        PedidoId       INT            NOT NULL,
        ProdutoId      INT            NOT NULL,
        Quantidade     INT            NOT NULL,
        -- Preço congelado no momento da compra (não muda se o produto mudar de preço).
        PrecoUnitario  DECIMAL(18,2)  NOT NULL,
        CONSTRAINT PK_ItensPedido PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ItensPedido_Pedidos  FOREIGN KEY (PedidoId)  REFERENCES dbo.Pedidos (Id) ON DELETE CASCADE,
        CONSTRAINT FK_ItensPedido_Produtos FOREIGN KEY (ProdutoId) REFERENCES dbo.Produtos (Id),
        CONSTRAINT CK_ItensPedido_Quantidade    CHECK (Quantidade > 0),
        CONSTRAINT CK_ItensPedido_PrecoUnitario CHECK (PrecoUnitario >= 0)
    );

    -- RN-05 (produto não se repete no pedido) garantida no banco + índice do FK PedidoId
    -- + cobertura para SUM(Quantidade * PrecoUnitario) por pedido.
    CREATE UNIQUE INDEX UX_ItensPedido_PedidoId_ProdutoId
        ON dbo.ItensPedido (PedidoId, ProdutoId)
        INCLUDE (Quantidade, PrecoUnitario);

    CREATE INDEX IX_ItensPedido_ProdutoId ON dbo.ItensPedido (ProdutoId);
END
GO
