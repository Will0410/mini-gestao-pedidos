namespace Pedidos.Domain.Entities;

public sealed class ItemPedido
{
    internal ItemPedido(int produtoId, int quantidade, decimal precoUnitario)
    {
        ProdutoId = produtoId;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
    }

    // Construtor para o EF Core.
    private ItemPedido()
    {
    }

    public int Id { get; private set; }

    public int PedidoId { get; private set; }

    public int ProdutoId { get; private set; }

    public int Quantidade { get; private set; }

    /// <summary>Preço congelado no momento da compra (RN-07).</summary>
    public decimal PrecoUnitario { get; private set; }

    public decimal Subtotal => Quantidade * PrecoUnitario;
}
