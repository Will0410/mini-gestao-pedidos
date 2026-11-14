using Pedidos.Domain.Common;
using Pedidos.Domain.Entities;

namespace Pedidos.Application.Abstractions;

public interface IClienteRepository
{
    /// <summary>Retorna o cliente se existir e estiver ativo (RN-02); caso contrário, null.</summary>
    Task<Cliente?> ObterAtivoAsync(int id, CancellationToken ct);
}

public interface IProdutoRepository
{
    /// <summary>Retorna os produtos ativos entre os ids informados (RN-03).</summary>
    Task<IReadOnlyList<Produto>> ObterAtivosPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    /// <summary>
    /// Baixa o estoque de forma ATÔMICA: <c>UPDATE ... SET Estoque = Estoque - @q WHERE Id = @id AND Estoque &gt;= @q</c>.
    /// Retorna false se não havia estoque suficiente no instante da escrita (RN-10). Deve rodar dentro de
    /// <see cref="IUnitOfWork.ExecutarEmTransacaoAsync{T}"/>.
    /// </summary>
    Task<bool> TentarBaixarEstoqueAsync(int produtoId, int quantidade, CancellationToken ct);

    /// <summary>Estoque atual (usado apenas para montar a mensagem de erro após uma corrida perdida).</summary>
    Task<int> ObterEstoqueAsync(int produtoId, CancellationToken ct);
}

public interface IPedidoRepository
{
    void Adicionar(Pedido pedido);
}

public interface IUnitOfWork
{
    /// <summary>
    /// Executa a operação numa transação. Se o resultado for falha (ou houver exceção), faz rollback;
    /// se for sucesso, persiste as mudanças rastreadas e faz commit.
    /// </summary>
    Task<Result<T>> ExecutarEmTransacaoAsync<T>(Func<CancellationToken, Task<Result<T>>> operacao, CancellationToken ct);
}
