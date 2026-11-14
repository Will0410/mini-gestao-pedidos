using Microsoft.EntityFrameworkCore;
using Pedidos.Application.Relatorios;
using Pedidos.Infrastructure.Persistence;

namespace Pedidos.Infrastructure.Queries;

internal sealed class RelatorioQueries(PedidosDbContext db) : IRelatorioQueries
{
    /// <summary>
    /// Top 5 clientes por valor no período (RN-14). Aplica as lições da Parte 4:
    /// agrupa por ClienteId (não por nome), filtro sargável, ordenação DESC com desempate determinístico,
    /// e lê só o índice de cobertura IX_Pedidos_DataPedido (DataPedido) INCLUDE (ClienteId, ValorTotal).
    /// </summary>
    public async Task<IReadOnlyList<TopClienteResponse>> TopClientesAsync(DateOnly dataInicio, DateOnly dataFim, CancellationToken ct)
    {
        var de = dataInicio.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var ate = dataFim.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var ranking = await db.Pedidos.AsNoTracking()
            .Where(p => p.DataPedido >= de && p.DataPedido < ate)
            .GroupBy(p => p.ClienteId)
            .Select(g => new { ClienteId = g.Key, Quantidade = g.Count(), Total = g.Sum(p => p.ValorTotal) })
            .OrderByDescending(x => x.Total).ThenBy(x => x.ClienteId)
            .Take(TopClientesQuery.Limite)
            .Join(db.Clientes, r => r.ClienteId, c => c.Id, (r, c) => new { r.ClienteId, c.Nome, r.Quantidade, r.Total })
            .ToListAsync(ct);

        // O JOIN após o TOP pode não preservar a ordem: reordena em memória (no máximo 5 linhas).
        return [.. ranking
            .OrderByDescending(x => x.Total).ThenBy(x => x.ClienteId)
            .Select((x, i) => new TopClienteResponse(i + 1, x.ClienteId, x.Nome, x.Quantidade, x.Total))];
    }
}
