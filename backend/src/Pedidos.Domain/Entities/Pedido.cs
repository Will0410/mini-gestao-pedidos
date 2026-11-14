using Pedidos.Domain.Common;

namespace Pedidos.Domain.Entities;

/// <summary>Linha de entrada para criação de pedido: produto já carregado do banco + quantidade pedida.</summary>
public readonly record struct LinhaPedido(Produto Produto, int Quantidade);

/// <summary>
/// Agregado Pedido. Imutável após a criação (edição está fora de escopo — docs/spec.md §1.1).
/// Toda regra que depende apenas dos dados do pedido vive aqui e é testada sem banco.
/// </summary>
public sealed class Pedido
{
    private readonly List<ItemPedido> _itens = [];

    private Pedido(int clienteId, DateTime dataPedidoUtc)
    {
        ClienteId = clienteId;
        DataPedido = dataPedidoUtc;
    }

    // Construtor para o EF Core.
    private Pedido()
    {
    }

    public int Id { get; private set; }

    public int ClienteId { get; private set; }

    /// <summary>Data/hora UTC atribuída pelo servidor (RN-09).</summary>
    public DateTime DataPedido { get; private set; }

    /// <summary>Σ(quantidade × preço unitário), calculado no servidor (RN-08).</summary>
    public decimal ValorTotal { get; private set; }

    public IReadOnlyCollection<ItemPedido> Itens => _itens.AsReadOnly();

    /// <summary>
    /// Cria um pedido validando RN-01, RN-04, RN-05, RN-06 (checagem prévia), RN-07, RN-08 e RN-09.
    /// A existência/atividade de cliente e produtos (RN-02, RN-03) é responsabilidade do caso de uso,
    /// que precisa consultar o banco.
    /// </summary>
    public static Result<Pedido> Criar(int clienteId, IReadOnlyCollection<LinhaPedido> linhas, DateTime agoraUtc)
    {
        if (linhas.Count == 0)
        {
            return Errors.Pedido.SemItens();
        }

        if (linhas.Count > Errors.Pedido.MaxItens)
        {
            return Errors.Pedido.ItensDemais();
        }

        var produtosVistos = new HashSet<int>();
        foreach (var (produto, quantidade) in linhas)
        {
            if (!produtosVistos.Add(produto.Id))
            {
                return Errors.Pedido.ProdutoRepetido(produto.Id);
            }

            if (quantidade is < Errors.Pedido.QuantidadeMinima or > Errors.Pedido.QuantidadeMaxima)
            {
                return Errors.Pedido.QuantidadeInvalida(produto.Id);
            }

            if (!produto.PossuiEstoque(quantidade))
            {
                return Errors.Pedido.EstoqueInsuficiente(produto.Id, produto.Nome, quantidade, produto.Estoque);
            }
        }

        // Trunca para segundos: a coluna é DATETIME2(0), que ARREDONDA frações. Sem isso, a resposta do
        // POST e um GET posterior poderiam divergir em até 1 segundo.
        var dataPedido = new DateTime(agoraUtc.Ticks - (agoraUtc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
        var pedido = new Pedido(clienteId, dataPedido);
        foreach (var (produto, quantidade) in linhas)
        {
            // RN-07: o preço vem do cadastro do produto, nunca da requisição.
            pedido._itens.Add(new ItemPedido(produto.Id, quantidade, produto.Preco));
        }

        pedido.ValorTotal = decimal.Round(pedido._itens.Sum(i => i.Subtotal), 2, MidpointRounding.AwayFromZero);
        return pedido;
    }
}
