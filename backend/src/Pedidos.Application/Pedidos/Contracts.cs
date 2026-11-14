namespace Pedidos.Application.Pedidos;

// ----- POST /pedidos -----

/// <summary>Request de criação. Não há campo de preço: o preço vem do cadastro (RN-07).</summary>
public sealed record CriarPedidoRequest(int ClienteId, IReadOnlyList<ItemPedidoRequest>? Itens);

public sealed record ItemPedidoRequest(int ProdutoId, int Quantidade);

// ----- Respostas -----

public sealed record PedidoDetalheResponse(
    int Id,
    int ClienteId,
    string ClienteNome,
    DateTime DataPedido,
    decimal ValorTotal,
    IReadOnlyList<ItemPedidoResponse> Itens);

public sealed record ItemPedidoResponse(
    int ProdutoId,
    string ProdutoNome,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);

public sealed record PedidoResumoResponse(
    int Id,
    int ClienteId,
    string ClienteNome,
    DateTime DataPedido,
    int QuantidadeItens,
    decimal ValorTotal);

// ----- GET /pedidos -----

public sealed record ListarPedidosQuery(
    int? ClienteId,
    DateOnly? DataInicio,
    DateOnly? DataFim,
    int Page = ListarPedidosQuery.PaginaPadrao,
    int PageSize = ListarPedidosQuery.TamanhoPadrao)
{
    public const int PaginaPadrao = 1;
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    /// <summary>Com pageSize ≤ 100, o offset máximo (10 milhões) cabe folgado em int.</summary>
    public const int PaginaMaxima = 100_000;
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}
