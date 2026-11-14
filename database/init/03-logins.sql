/* 03 - Logins de menor privilégio (idempotente).
   - pedidos_app : usado pela API. Lê e escreve dados; NÃO pode alterar schema (sem DDL).
   - mcp_readonly: usado pelo MCP do Claude. Apenas leitura + SHOWPLAN (plano de execução).
   Senhas vêm de variáveis do sqlcmd (-v), alimentadas pelo .env / docker-compose. */
USE [master];
GO
IF SUSER_ID(N'pedidos_app') IS NULL
    EXEC (N'CREATE LOGIN pedidos_app WITH PASSWORD = N''$(APP_DB_PASSWORD)'', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DB_NAME)]');
ELSE
    EXEC (N'ALTER LOGIN pedidos_app WITH PASSWORD = N''$(APP_DB_PASSWORD)''');

IF SUSER_ID(N'mcp_readonly') IS NULL
    EXEC (N'CREATE LOGIN mcp_readonly WITH PASSWORD = N''$(MCP_DB_PASSWORD)'', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DB_NAME)]');
ELSE
    EXEC (N'ALTER LOGIN mcp_readonly WITH PASSWORD = N''$(MCP_DB_PASSWORD)''');
GO

USE [$(DB_NAME)];
GO
IF USER_ID(N'pedidos_app') IS NULL CREATE USER pedidos_app FOR LOGIN pedidos_app;
IF USER_ID(N'mcp_readonly') IS NULL CREATE USER mcp_readonly FOR LOGIN mcp_readonly;

ALTER ROLE db_datareader ADD MEMBER pedidos_app;
ALTER ROLE db_datawriter ADD MEMBER pedidos_app;

ALTER ROLE db_datareader ADD MEMBER mcp_readonly;
-- Defesa em profundidade: mesmo que o MCP deixe passar um comando de escrita, o banco nega.
ALTER ROLE db_denydatawriter ADD MEMBER mcp_readonly;
GRANT SHOWPLAN TO mcp_readonly;
GRANT VIEW DEFINITION TO mcp_readonly;
GO
