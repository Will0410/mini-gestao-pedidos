using Microsoft.EntityFrameworkCore;
using Pedidos.Application.Pedidos;
using Pedidos.Infrastructure.Persistence;

namespace Pedidos.Infrastructure.Queries;

internal sealed class PedidoQueries(PedidosDbContext db) : IPedidoQueries
{
    public async Task<PagedResponse<PedidoResumoResponse>> ListarAsync(ListarPedidosQuery query, CancellationToken ct)
    {
        var pedidos = db.Pedidos.AsNoTracking();

        if (query.ClienteId is { } clienteId)
        {
            pedidos = pedidos.Where(p => p.ClienteId == clienteId);
        }

        // Intervalo semiaberto [inicio 00:00, fim+1 00:00): datas inclusivas (RN-13) e sargável
        // → usa IX_Pedidos_DataPedido / IX_Pedidos_ClienteId_DataPedido com Index Seek.
        if (query.DataInicio is { } inicio)
        {
            var de = inicio.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            pedidos = pedidos.Where(p => p.DataPedido >= de);
        }

        if (query.DataFim is { } fim)
        {
            var ate = fim.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            pedidos = pedidos.Where(p => p.DataPedido < ate);
        }

        var total = await pedidos.CountAsync(ct);

        var itens = await pedidos
            .OrderByDescending(p => p.DataPedido).ThenByDescending(p => p.Id) // RN-11
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Join(db.Clientes, p => p.ClienteId, c => c.Id, (p, c) => new PedidoResumoResponse(
                p.Id,
                p.ClienteId,
                c.Nome,
                p.DataPedido,
                p.Itens.Count,
                p.ValorTotal))
            .ToListAsync(ct);

        return new PagedResponse<PedidoResumoResponse>(itens, query.Page, query.PageSize, total);
    }

    public async Task<PedidoDetalheResponse?> ObterAsync(int id, CancellationToken ct)
    {
        var cabecalho = await db.Pedidos.AsNoTracking()
            .Where(p => p.Id == id)
            .Join(db.Clientes, p => p.ClienteId, c => c.Id, (p, c) => new { p.Id, p.ClienteId, c.Nome, p.DataPedido, p.ValorTotal })
            .FirstOrDefaultAsync(ct);

        if (cabecalho is null)
        {
            return null;
        }

        var itens = await db.ItensPedido.AsNoTracking()
            .Where(i => i.PedidoId == id)
            .Join(db.Produtos, i => i.ProdutoId, p => p.Id, (i, p) => new ItemPedidoResponse(
                i.ProdutoId,
                p.Nome,
                i.Quantidade,
                i.PrecoUnitario,
                i.Quantidade * i.PrecoUnitario))
            .OrderBy(i => i.ProdutoId)
            .ToListAsync(ct);

        return new PedidoDetalheResponse(cabecalho.Id, cabecalho.ClienteId, cabecalho.Nome, cabecalho.DataPedido, cabecalho.ValorTotal, itens);
    }
}
