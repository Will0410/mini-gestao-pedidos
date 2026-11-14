using FluentValidation;
using Pedidos.Api.Http;
using Pedidos.Application.Common;
using Pedidos.Application.Pedidos;
using Pedidos.Domain;

namespace Pedidos.Api.Endpoints;

public static class PedidosEndpoints
{
    public const string RateLimitEscrita = "escrita";

    public static IEndpointRouteBuilder MapPedidosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/pedidos").WithTags("Pedidos");

        // POST /pedidos — docs/spec.md §5.1
        group.MapPost("/", async (CriarPedidoRequest request, CriarPedidoHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(request, ct);
                return result.IsSuccess
                    ? TypedResults.Created($"/pedidos/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .WithName("CriarPedido")
            .WithSummary("Cria um pedido com itens, validando cliente e estoque.")
            .RequireRateLimiting(RateLimitEscrita)
            .Produces<PedidoDetalheResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // GET /pedidos — docs/spec.md §5.3
        group.MapGet("/", async (
                [AsParameters] ListarPedidosQuery query,
                IValidator<ListarPedidosQuery> validator,
                IPedidoQueries pedidos,
                CancellationToken ct) =>
            {
                var validacao = await validator.ValidateAsync(query, ct);
                return validacao.IsValid
                    ? TypedResults.Ok(await pedidos.ListarAsync(query, ct))
                    : validacao.ToError().ToProblem();
            })
            .WithName("ListarPedidos")
            .WithSummary("Lista pedidos com filtros por cliente e período, paginada.")
            .Produces<PagedResponse<PedidoResumoResponse>>()
            .ProducesValidationProblem();

        // GET /pedidos/{id} — alvo do header Location do POST
        group.MapGet("/{id:int}", async (int id, IPedidoQueries pedidos, CancellationToken ct) =>
                await pedidos.ObterAsync(id, ct) is { } pedido
                    ? TypedResults.Ok(pedido)
                    : Errors.Pedido.NaoEncontrado(id).ToProblem())
            .WithName("ObterPedido")
            .WithSummary("Detalhe de um pedido.")
            .Produces<PedidoDetalheResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
