using Pedidos.Application.Abstractions;
using Pedidos.Domain.Common;
using Pedidos.Domain.Entities;

namespace Pedidos.UnitTests.Fakes;

/// <summary>
/// Fakes em memória escritos à mão (sem biblioteca de mock): comportamento explícito e fácil de ler,
/// incluindo rollback do estoque quando a transação falha.
/// </summary>
internal sealed class FakeClienteRepository(params Cliente[] clientes) : IClienteRepository
{
    public Task<Cliente?> ObterAtivoAsync(int id, CancellationToken ct) =>
        Task.FromResult(clientes.FirstOrDefault(c => c.Id == id && c.Ativo));
}

internal sealed class FakeProdutoRepository(params Produto[] produtos) : IProdutoRepository
{
    public Dictionary<int, int> Estoques { get; } = produtos.ToDictionary(p => p.Id, p => p.Estoque);

    /// <summary>Simula outro pedido levando o estoque entre a leitura e a escrita (corrida).</summary>
    public int? EstoqueRoubadoNaEscrita { get; set; }

    public List<(int ProdutoId, int Quantidade)> Baixas { get; } = [];

    public Task<IReadOnlyList<Produto>> ObterAtivosPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Produto>>([.. produtos.Where(p => ids.Contains(p.Id) && p.Ativo)]);

    public Task<bool> TentarBaixarEstoqueAsync(int produtoId, int quantidade, CancellationToken ct)
    {
        if (EstoqueRoubadoNaEscrita is { } restante)
        {
            Estoques[produtoId] = restante;
        }

        if (Estoques[produtoId] < quantidade)
        {
            return Task.FromResult(false);
        }

        Estoques[produtoId] -= quantidade;
        Baixas.Add((produtoId, quantidade));
        return Task.FromResult(true);
    }

    public Task<int> ObterEstoqueAsync(int produtoId, CancellationToken ct) => Task.FromResult(Estoques[produtoId]);
}

internal sealed class FakePedidoRepository : IPedidoRepository
{
    public List<Pedido> Adicionados { get; } = [];

    public void Adicionar(Pedido pedido) => Adicionados.Add(pedido);
}

internal sealed class FakeUnitOfWork(FakeProdutoRepository produtos, FakePedidoRepository pedidos) : IUnitOfWork
{
    public int Commits { get; private set; }

    public int Rollbacks { get; private set; }

    public async Task<Result<T>> ExecutarEmTransacaoAsync<T>(Func<CancellationToken, Task<Result<T>>> operacao, CancellationToken ct)
    {
        var estoqueAntes = new Dictionary<int, int>(produtos.Estoques);
        var pedidosAntes = pedidos.Adicionados.Count;

        var resultado = await operacao(ct);
        if (resultado.IsFailure)
        {
            // Rollback: restaura o estado como o banco faria.
            foreach (var (id, qtd) in estoqueAntes)
            {
                produtos.Estoques[id] = qtd;
            }

            pedidos.Adicionados.RemoveRange(pedidosAntes, pedidos.Adicionados.Count - pedidosAntes);
            Rollbacks++;
            return resultado;
        }

        Commits++;
        return resultado;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}
