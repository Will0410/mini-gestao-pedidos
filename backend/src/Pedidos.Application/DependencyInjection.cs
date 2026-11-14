using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pedidos.Application.Pedidos;

namespace Pedidos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CriarPedidoRequestValidator>(includeInternalTypes: true);
        services.AddScoped<CriarPedidoHandler>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
