namespace Pedidos.Application.Catalogo;

public sealed record ClienteResponse(int Id, string Nome, string Email);

public sealed record ProdutoResponse(int Id, string Sku, string Nome, decimal Preco, int Estoque);

/// <summary>Endpoints de apoio ao frontend (GET /clientes, GET /produtos).</summary>
public interface ICatalogoQueries
{
    Task<IReadOnlyList<ClienteResponse>> ListarClientesAsync(CancellationToken ct);

    Task<IReadOnlyList<ProdutoResponse>> ListarProdutosAsync(CancellationToken ct);
}
