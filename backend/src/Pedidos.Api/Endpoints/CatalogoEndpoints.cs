using Pedidos.Application.Catalogo;

namespace Pedidos.Api.Endpoints;

/// <summary>Endpoints de apoio ao frontend (docs/spec.md §5.5).</summary>
public static class CatalogoEndpoints
{
    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/clientes", async (ICatalogoQueries catalogo, CancellationToken ct) =>
                TypedResults.Ok(await catalogo.ListarClientesAsync(ct)))
            .WithTags("Catálogo")
            .WithName("ListarClientes")
            .Produces<IReadOnlyList<ClienteResponse>>();

        app.MapGet("/produtos", async (ICatalogoQueries catalogo, CancellationToken ct) =>
                TypedResults.Ok(await catalogo.ListarProdutosAsync(ct)))
            .WithTags("Catálogo")
            .WithName("ListarProdutos")
            .Produces<IReadOnlyList<ProdutoResponse>>();

        return app;
    }
}
