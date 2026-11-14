namespace Pedidos.Application.Pedidos;

/// <summary>Lado de leitura (CQRS leve): projeções diretas para DTO, sem carregar agregados.</summary>
public interface IPedidoQueries
{
    Task<PagedResponse<PedidoResumoResponse>> ListarAsync(ListarPedidosQuery query, CancellationToken ct);

    Task<PedidoDetalheResponse?> ObterAsync(int id, CancellationToken ct);
}
