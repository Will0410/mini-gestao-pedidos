namespace Pedidos.Domain.Entities;

public sealed class Produto
{
    public Produto(string sku, string nome, decimal preco, int estoque, bool ativo = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(preco);
        ArgumentOutOfRangeException.ThrowIfNegative(estoque);
        Sku = sku;
        Nome = nome;
        Preco = preco;
        Estoque = estoque;
        Ativo = ativo;
    }

    // Construtor para o EF Core.
    private Produto()
    {
        Sku = string.Empty;
        Nome = string.Empty;
    }

    public int Id { get; private set; }

    public string Sku { get; private set; }

    public string Nome { get; private set; }

    public decimal Preco { get; private set; }

    /// <summary>
    /// Estoque lido no momento da consulta. A baixa real é feita por UPDATE condicional atômico
    /// no repositório (RN-10), nunca alterando esta propriedade e chamando SaveChanges.
    /// </summary>
    public int Estoque { get; private set; }

    public bool Ativo { get; private set; }

    public bool PossuiEstoque(int quantidade) => quantidade > 0 && Estoque >= quantidade;

    /// <summary>Usado só em testes para simular o Id gerado pelo banco.</summary>
    internal Produto ComId(int id)
    {
        Id = id;
        return this;
    }
}
