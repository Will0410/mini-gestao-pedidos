using Microsoft.EntityFrameworkCore;
using Pedidos.Application.Catalogo;
using Pedidos.Infrastructure.Persistence;

namespace Pedidos.Infrastructure.Queries;

internal sealed class CatalogoQueries(PedidosDbContext db) : ICatalogoQueries
{
    public async Task<IReadOnlyList<ClienteResponse>> ListarClientesAsync(CancellationToken ct) =>
        await db.Clientes.AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Select(c => new ClienteResponse(c.Id, c.Nome, c.Email))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProdutoResponse>> ListarProdutosAsync(CancellationToken ct) =>
        await db.Produtos.AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .Select(p => new ProdutoResponse(p.Id, p.Sku, p.Nome, p.Preco, p.Estoque))
            .ToListAsync(ct);
}
