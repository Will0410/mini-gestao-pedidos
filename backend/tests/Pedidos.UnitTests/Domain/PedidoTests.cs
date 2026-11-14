using Pedidos.Domain.Common;
using Pedidos.Domain.Entities;

namespace Pedidos.UnitTests.Domain;

public sealed class PedidoTests
{
    private static readonly DateTime Agora = new(2026, 11, 14, 13, 5, 42, 789, DateTimeKind.Utc);

    private static Produto NovoProduto(int id, decimal preco = 10m, int estoque = 100, bool ativo = true) =>
        new Produto($"SKU-{id}", $"Produto {id}", preco, estoque, ativo).ComId(id);

    [Fact]
    public void Criar_ComItensValidos_CalculaTotalComPrecoDoCadastro()
    {
        // CA-01 / RN-07 / RN-08
        var linhas = new[]
        {
            new LinhaPedido(NovoProduto(1, preco: 99.90m), 2),
            new LinhaPedido(NovoProduto(2, preco: 0.10m), 3),
        };

        var result = Pedido.Criar(clienteId: 7, linhas, Agora);

        Assert.True(result.IsSuccess);
        var pedido = result.Value;
        Assert.Equal(7, pedido.ClienteId);
        Assert.Equal(200.10m, pedido.ValorTotal);
        Assert.Collection(
            pedido.Itens,
            i => Assert.Equal((1, 2, 99.90m, 199.80m), (i.ProdutoId, i.Quantidade, i.PrecoUnitario, i.Subtotal)),
            i => Assert.Equal((2, 3, 0.10m, 0.30m), (i.ProdutoId, i.Quantidade, i.PrecoUnitario, i.Subtotal)));
    }

    [Fact]
    public void Criar_DataDoServidor_TruncadaEmSegundosEmUtc()
    {
        // RN-09: a data vem do servidor; truncada para casar com DATETIME2(0).
        var pedido = Pedido.Criar(1, [new LinhaPedido(NovoProduto(1), 1)], Agora).Value;

        Assert.Equal(new DateTime(2026, 11, 14, 13, 5, 42, DateTimeKind.Utc), pedido.DataPedido);
        Assert.Equal(DateTimeKind.Utc, pedido.DataPedido.Kind);
    }

    [Fact]
    public void Criar_SemItens_RetornaErroDeValidacao()
    {
        // CA-02 / RN-01
        var result = Pedido.Criar(1, [], Agora);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDACAO", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("itens", result.Error.Details!.Keys);
    }

    [Fact]
    public void Criar_ComMaisDe50Itens_RetornaErroDeValidacao()
    {
        // RN-01
        var linhas = Enumerable.Range(1, 51).Select(i => new LinhaPedido(NovoProduto(i), 1)).ToArray();

        var result = Pedido.Criar(1, linhas, Agora);

        Assert.Equal("VALIDACAO", result.Error!.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Criar_QuantidadeForaDoIntervalo_RetornaErroDeValidacao(int quantidade)
    {
        // CA-06 / RN-04
        var result = Pedido.Criar(1, [new LinhaPedido(NovoProduto(1), quantidade)], Agora);

        Assert.Equal("VALIDACAO", result.Error!.Code);
    }

    [Fact]
    public void Criar_ProdutoRepetido_RetornaErroDeValidacao()
    {
        // CA-06 / RN-05
        var produto = NovoProduto(1);

        var result = Pedido.Criar(1, [new LinhaPedido(produto, 1), new LinhaPedido(produto, 2)], Agora);

        Assert.Equal("VALIDACAO", result.Error!.Code);
        Assert.Contains("mais de uma vez", result.Error.Details!["itens"][0], StringComparison.Ordinal);
    }

    [Fact]
    public void Criar_EstoqueInsuficiente_RetornaConflitoComDetalhes()
    {
        // CA-05 / RN-06
        var result = Pedido.Criar(1, [new LinhaPedido(NovoProduto(9, estoque: 3), 4)], Agora);

        Assert.True(result.IsFailure);
        Assert.Equal("ESTOQUE_INSUFICIENTE", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("4", result.Error.Details!["solicitado"][0]);
        Assert.Equal("3", result.Error.Details["disponivel"][0]);
    }

    [Fact]
    public void Criar_EstoqueExatamenteIgualAQuantidade_Permite()
    {
        // Borda de RN-06: estoque == quantidade é suficiente.
        var result = Pedido.Criar(1, [new LinhaPedido(NovoProduto(1, estoque: 5), 5)], Agora);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Criar_ValoresDecimais_NaoPerdePrecisao()
    {
        // RN-08: decimal, sem erro de ponto flutuante (0.1 * 3 == 0.3 exato).
        var result = Pedido.Criar(1, [new LinhaPedido(NovoProduto(1, preco: 0.1m), 3)], Agora);

        Assert.Equal(0.3m, result.Value.ValorTotal);
    }

    [Fact]
    public void Value_EmResultDeFalha_LancaInvalidOperation()
    {
        var result = Pedido.Criar(1, [], Agora);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
