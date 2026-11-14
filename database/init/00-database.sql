/* 00 - Criação do banco (idempotente). Executado conectado ao master. */
IF DB_ID(N'$(DB_NAME)') IS NULL
BEGIN
    PRINT 'Criando banco $(DB_NAME)...';
    EXEC (N'CREATE DATABASE [$(DB_NAME)]');
END
GO

/* READ_COMMITTED_SNAPSHOT: leituras (listagem/relatório) não bloqueiam nem são bloqueadas
   pelas escritas de pedidos. A baixa de estoque continua protegida pelo UPDATE condicional. */
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'$(DB_NAME)' AND is_read_committed_snapshot_on = 0)
    EXEC (N'ALTER DATABASE [$(DB_NAME)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE');
GO
