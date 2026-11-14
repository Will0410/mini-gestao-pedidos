using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pedidos.Application.Abstractions;
using Pedidos.Application.Catalogo;
using Pedidos.Application.Pedidos;
using Pedidos.Application.Relatorios;
using Pedidos.Infrastructure.Persistence;
using Pedidos.Infrastructure.Queries;

namespace Pedidos.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "PedidosDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} não configurada.");

        services.AddDbContext<PedidosDbContext>(options => options.UseSqlServer(connectionString, sql =>
        {
            // Retry para falhas transitórias (ex.: SQL Server reiniciando). Seguro porque o UnitOfWork
            // usa a execution strategy em volta da transação inteira.
            sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorNumbersToAdd: null);
            sql.CommandTimeout(15);
        }));

        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPedidoQueries, PedidoQueries>();
        services.AddScoped<IRelatorioQueries, RelatorioQueries>();
        services.AddScoped<ICatalogoQueries, CatalogoQueries>();
        return services;
    }
}
