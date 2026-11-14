using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(Pedidos.IntegrationTests.Infrastructure.SqlServerFixture))]

namespace Pedidos.IntegrationTests.Infrastructure;

/// <summary>
/// Um SQL Server real para toda a suíte (uma vez por execução, ~15 s).
/// - Padrão: sobe um container via Testcontainers.
/// - Alternativa: defina INTEGRATION_SQL_SERVER (ex.: "localhost,14333") e INTEGRATION_SQL_SA_PASSWORD
///   para usar o SQL Server do docker compose.
/// Aplica database/init/01-schema.sql e 03-logins.sql e conecta a API com o login pedidos_app
/// (menor privilégio) — se faltar permissão, o teste quebra como quebraria em produção.
/// </summary>
public sealed partial class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    // Logins são do SERVIDOR (não do banco). No modo externo, reutiliza a senha do compose para que o
    // ALTER LOGIN de 03-logins.sql não quebre a API que já está rodando no mesmo SQL Server.
    private static string AppPassword =>
        Environment.GetEnvironmentVariable("INTEGRATION_APP_DB_PASSWORD") ?? "App@Pedidos2026!";

    public string DatabaseName { get; } = $"PedidosTest_{Guid.NewGuid():N}"[..24];

    /// <summary>Conexão administrativa (sa) para preparar dados de teste.</summary>
    public string AdminConnectionString { get; private set; } = string.Empty;

    /// <summary>Conexão usada pela API (login pedidos_app, sem DDL).</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("INTEGRATION_SQL_SERVER");
        string masterConnection;
        if (string.IsNullOrWhiteSpace(server))
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            masterConnection = _container.GetConnectionString();
        }
        else
        {
            masterConnection = new SqlConnectionStringBuilder
            {
                DataSource = server,
                UserID = "sa",
                Password = Environment.GetEnvironmentVariable("INTEGRATION_SQL_SA_PASSWORD") ?? "Pedidos@Local2026!",
                TrustServerCertificate = true,
            }.ConnectionString;
        }

        await ExecuteAsync(masterConnection, $"CREATE DATABASE [{DatabaseName}]; ALTER DATABASE [{DatabaseName}] SET READ_COMMITTED_SNAPSHOT ON;");

        AdminConnectionString = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = DatabaseName }.ConnectionString;

        var variables = new Dictionary<string, string>
        {
            ["DB_NAME"] = DatabaseName,
            ["APP_DB_PASSWORD"] = AppPassword,
            ["MCP_DB_PASSWORD"] = "Mcp@IntegrationTests1!",
        };
        await RunScriptAsync(AdminConnectionString, "01-schema.sql", variables);
        await RunScriptAsync(AdminConnectionString, "03-logins.sql", variables);

        AppConnectionString = new SqlConnectionStringBuilder(AdminConnectionString)
        {
            UserID = "pedidos_app",
            Password = AppPassword,
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Executa um script .sql como o sqlcmd: divide em lotes por "GO" e substitui $(VAR).</summary>
    private static async Task RunScriptAsync(string connectionString, string fileName, IReadOnlyDictionary<string, string> variables)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "database", fileName);
        var script = await File.ReadAllTextAsync(path);
        foreach (var (key, value) in variables)
        {
            script = script.Replace($"$({key})", value, StringComparison.Ordinal);
        }

        foreach (var batch in GoSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b)))
        {
            await ExecuteAsync(connectionString, batch);
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
