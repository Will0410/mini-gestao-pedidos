using FluentValidation;
using Pedidos.Api.Http;
using Pedidos.Application.Common;
using Pedidos.Application.Relatorios;

namespace Pedidos.Api.Endpoints;

public static class RelatoriosEndpoints
{
    public static IEndpointRouteBuilder MapRelatoriosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/relatorios").WithTags("Relatórios");

        // GET /relatorios/top-clientes — docs/spec.md §5.4
        group.MapGet("/top-clientes", async (
                [AsParameters] TopClientesQuery query,
                IValidator<TopClientesQuery> validator,
                IRelatorioQueries relatorios,
                CancellationToken ct) =>
            {
                var validacao = await validator.ValidateAsync(query, ct);
                if (!validacao.IsValid)
                {
                    return validacao.ToError().ToProblem();
                }

                return TypedResults.Ok(await relatorios.TopClientesAsync(query.DataInicio!.Value, query.DataFim!.Value, ct));
            })
            .WithName("TopClientes")
            .WithSummary("Top 5 clientes com maior valor total de pedidos no período.")
            .Produces<IReadOnlyList<TopClienteResponse>>()
            .ProducesValidationProblem();

        return app;
    }
}
