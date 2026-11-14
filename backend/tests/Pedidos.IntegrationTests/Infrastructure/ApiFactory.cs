using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pedidos.IntegrationTests.Infrastructure;

/// <summary>Sobe a API real em memória (TestServer) apontando para o SQL Server do fixture.</summary>
public sealed class ApiFactory(SqlServerFixture sql) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PedidosDb", sql.AppConnectionString);
        builder.UseSetting("RateLimiting:PermitLimit", "10000"); // o teste de concorrência dispara rajadas
    }
}
