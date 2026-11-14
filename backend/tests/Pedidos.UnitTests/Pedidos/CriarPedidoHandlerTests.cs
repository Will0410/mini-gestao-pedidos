using Microsoft.Extensions.Logging.Abstractions;
using Pedidos.Application.Pedidos;
using Pedidos.Domain.Entities;
using Pedidos.UnitTests.Fakes;

namespace Pedidos.UnitTests.Pedidos;

public sealed class CriarPedidoHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeClienteRepository _clientes = new(
        new Cliente("Ana Souza", "ana@exemplo.com").ComId(1),
        new Cliente("Inativo", "inativo@exemplo.com", ativo: false).ComId(2));

    private readonly FakeProdutoRepository _produtos = new(
        new Produto("TC-001", "Teclado", 100m, estoque: 10).ComId(10),
        new Produto("MS-001", "Mouse", 50m, estoque: 2).ComId(20),
        new Produto("OLD-001", "Descontinuado", 1m, estoque: 99, ativo: false).ComId(30));

    private readonly FakePedidoRepository _pedidos = new();

    private readonly FakeUnitOfWork _uow;

    private readonly CriarPedidoHandler _handler;

    public CriarPedidoHandlerTests()
    {
        _uow = new FakeUnitOfWork(_produtos, _pedidos);
        _handler = new CriarPedidoHandler(
            new CriarPedidoRequestValidator(),
            _clientes,
            _produtos,
            _pedidos,
            _uow,
            new FixedTimeProvider(Agora),
            NullLogger<CriarPedidoHandler>.Instance);
    }

    private static CriarPedidoRequest Request(int clienteId, params (int ProdutoId, int Quantidade)[] itens) =>
        new(clienteId, [.. itens.Select(i => new ItemPedidoRequest(i.ProdutoId, i.Quantidade))]);

    [Fact]
    public async Task Handle_PedidoValido_PersisteBaixaEstoqueERetornaDetalhe()
    {
        // CA-01
        var result = await _handler.HandleAsync(Request(1, (10, 3), (20, 2)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var resposta = result.Value;
        Assert.Equal("Ana Souza", resposta.ClienteNome);
        Assert.Equal(400m, resposta.ValorTotal);                       // 3×100 + 2×50
        Assert.Equal(Agora.UtcDateTime, resposta.DataPedido);
        Assert.Equal(["Teclado", "Mouse"], resposta.Itens.Select(i => i.ProdutoNome));
        Assert.Single(_pedidos.Adicionados);
        Assert.Equal(7, _produtos.Estoques[10]);
        Assert.Equal(0, _produtos.Estoques[20]);
        Assert.Equal(1, _uow.Commits);
    }

    [Fact]
    public async Task Handle_BaixaDeEstoque_OcorreEmOrdemDeProdutoId()
    {
        // Ordem determinística de locks evita deadlock entre pedidos concorrentes.
        await _handler.HandleAsync(Request(1, (20, 1), (10, 1)), CancellationToken.None);

        Assert.Equal([10, 20], _produtos.Baixas.Select(b => b.ProdutoId));
    }

    [Fact]
    public async Task Handle_SemItens_RetornaValidacaoSemTocarNoBanco()
    {
        // CA-02
        var result = await _handler.HandleAsync(new CriarPedidoRequest(1, []), CancellationToken.None);

        Assert.Equal("VALIDACAO", result.Error!.Code);
        Assert.Empty(_pedidos.Adicionados);
        Assert.Equal(0, _uow.Commits + _uow.Rollbacks);
    }

    [Fact]
    public async Task Handle_ItensNulos_RetornaValidacao()
    {
        // CA-02: JSON sem a propriedade "itens".
        var result = await _handler.HandleAsync(new CriarPedidoRequest(1, null), CancellationToken.None);

        Assert.Equal("VALIDACAO", result.Error!.Code);
    }

    [Theory]
    [InlineData(999)] // inexistente
    [InlineData(2)]   // inativo
    public async Task Handle_ClienteInexistenteOuInativo_RetornaClienteNaoEncontrado(int clienteId)
    {
        // CA-03 / RN-02
        var result = await _handler.HandleAsync(Request(clienteId, (10, 1)), CancellationToken.None);

        Assert.Equal("CLIENTE_NAO_ENCONTRADO", result.Error!.Code);
        Assert.Empty(_pedidos.Adicionados);
        Assert.Equal(10, _produtos.Estoques[10]);
    }

    [Theory]
    [InlineData(404)] // inexistente
    [InlineData(30)]  // inativo
    public async Task Handle_ProdutoInexistenteOuInativo_RetornaProdutoNaoEncontrado(int produtoId)
    {
        // CA-04 / RN-03
        var result = await _handler.HandleAsync(Request(1, (10, 1), (produtoId, 1)), CancellationToken.None);

        Assert.Equal("PRODUTO_NAO_ENCONTRADO", result.Error!.Code);
        Assert.Contains(produtoId.ToString(System.Globalization.CultureInfo.InvariantCulture), result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_pedidos.Adicionados);
    }

    [Fact]
    public async Task Handle_EstoqueInsuficienteEmUmItem_NaoBaixaNenhumEstoque()
    {
        // CA-05 / RN-06: atomicidade — falha no 2º item não pode baixar o 1º.
        var result = await _handler.HandleAsync(Request(1, (10, 1), (20, 3)), CancellationToken.None);

        Assert.Equal("ESTOQUE_INSUFICIENTE", result.Error!.Code);
        Assert.Equal(10, _produtos.Estoques[10]);
        Assert.Equal(2, _produtos.Estoques[20]);
        Assert.Empty(_pedidos.Adicionados);
    }

    [Fact]
    public async Task Handle_CorridaDeEstoquePerdidaNaEscrita_FazRollbackERetornaConflito()
    {
        // RN-10: a leitura viu estoque 10, mas outro pedido levou tudo antes do UPDATE.
        _produtos.EstoqueRoubadoNaEscrita = 0;

        var result = await _handler.HandleAsync(Request(1, (10, 5)), CancellationToken.None);

        Assert.Equal("ESTOQUE_INSUFICIENTE", result.Error!.Code);
        Assert.Equal("0", result.Error.Details!["disponivel"][0]);
        Assert.Equal(1, _uow.Rollbacks);
        Assert.Empty(_pedidos.Adicionados);
    }

    [Fact]
    public async Task Handle_PrecoVemDoCadastro_NuncaDoRequest()
    {
        // CA-08 / RN-07: o contrato nem possui campo de preço; o preço do item é o do produto.
        var result = await _handler.HandleAsync(Request(1, (10, 1)), CancellationToken.None);

        Assert.Equal(100m, result.Value.Itens.Single().PrecoUnitario);
        Assert.DoesNotContain(
            typeof(ItemPedidoRequest).GetProperties(),
            p => p.Name.Contains("Preco", StringComparison.OrdinalIgnoreCase));
    }
}
