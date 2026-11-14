namespace Pedidos.Domain.Entities;

public sealed class Cliente
{
    public Cliente(string nome, string email, bool ativo = true)
    {
        Nome = nome;
        Email = email;
        Ativo = ativo;
    }

    // Construtor para o EF Core.
    private Cliente()
    {
        Nome = string.Empty;
        Email = string.Empty;
    }

    public int Id { get; private set; }

    public string Nome { get; private set; }

    public string Email { get; private set; }

    public bool Ativo { get; private set; }

    public DateTime DataCadastro { get; private set; }

    /// <summary>Usado só em testes para simular o Id gerado pelo banco.</summary>
    internal Cliente ComId(int id)
    {
        Id = id;
        return this;
    }
}
