using Microsoft.EntityFrameworkCore;
using Pedidos.Application.Abstractions;
using Pedidos.Domain.Entities;

namespace Pedidos.Infrastructure.Persistence;

internal sealed class ClienteRepository(PedidosDbContext db) : IClienteRepository
{
    public Task<Cliente?> ObterAtivoAsync(int id, CancellationToken ct) =>
        db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.Ativo, ct);
}

internal sealed class ProdutoRepository(PedidosDbContext db) : IProdutoRepository
{
    public async Task<IReadOnlyList<Produto>> ObterAtivosPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Produtos.AsNoTracking().Where(p => ids.Contains(p.Id) && p.Ativo).ToListAsync(ct);

    /// <summary>
    /// Gera exatamente:
    /// <code>UPDATE [p] SET [p].[Estoque] = [p].[Estoque] - @q FROM [Produtos] AS [p] WHERE [p].[Id] = @id AND [p].[Ativo] = 1 AND [p].[Estoque] &gt;= @q</code>
    /// A leitura do estoque e a escrita acontecem no mesmo statement, sob lock de linha (U → X):
    /// duas transações concorrentes nunca veem o mesmo saldo, logo não há venda acima do estoque (RN-10).
    /// O CHECK (Estoque &gt;= 0) no banco é a última barreira.
    /// </summary>
    public async Task<bool> TentarBaixarEstoqueAsync(int produtoId, int quantidade, CancellationToken ct)
    {
        var linhas = await db.Produtos
            .Where(p => p.Id == produtoId && p.Ativo && p.Estoque >= quantidade)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estoque, p => p.Estoque - quantidade), ct);
        return linhas == 1;
    }

    public Task<int> ObterEstoqueAsync(int produtoId, CancellationToken ct) =>
        db.Produtos.Where(p => p.Id == produtoId).Select(p => p.Estoque).FirstOrDefaultAsync(ct);
}

internal sealed class PedidoRepository(PedidosDbContext db) : IPedidoRepository
{
    public void Adicionar(Pedido pedido) => db.Pedidos.Add(pedido);
}
