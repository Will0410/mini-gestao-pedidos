using Pedidos.Application.Pedidos;
using Pedidos.Application.Relatorios;

namespace Pedidos.UnitTests.Pedidos;

public sealed class CriarPedidoRequestValidatorTests
{
    private readonly CriarPedidoRequestValidator _validator = new();

    private static CriarPedidoRequest Request(int clienteId, params (int ProdutoId, int Quantidade)[] itens) =>
        new(clienteId, [.. itens.Select(i => new ItemPedidoRequest(i.ProdutoId, i.Quantidade))]);

    [Fact]
    public void Validate_RequestValido_SemErros() =>
        Assert.True(_validator.Validate(Request(1, (1, 1), (2, 1000))).IsValid);

    [Fact]
    public void Validate_ItensNulos_ErroEmItensSemExcecao()
    {
        // Cascade(Stop) impede NullReferenceException nos Must seguintes.
        var result = _validator.Validate(new CriarPedidoRequest(1, null));

        Assert.Contains(result.Errors, e => e.PropertyName == "Itens");
    }

    [Fact]
    public void Validate_ItensVazios_ErroEmItens()
    {
        // CA-02
        var result = _validator.Validate(Request(1));

        Assert.Single(result.Errors, e => e.PropertyName == "Itens");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_ClienteIdInvalido_Erro(int clienteId) =>
        Assert.Contains(_validator.Validate(Request(clienteId, (1, 1))).Errors, e => e.PropertyName == "ClienteId");

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Validate_QuantidadeForaDoIntervalo_ErroNoItem(int quantidade)
    {
        // CA-06 / RN-04
        var result = _validator.Validate(Request(1, (1, quantidade)));

        Assert.Contains(result.Errors, e => e.PropertyName == "Itens[0].Quantidade");
    }

    [Fact]
    public void Validate_ProdutoRepetido_Erro()
    {
        // CA-06 / RN-05
        var result = _validator.Validate(Request(1, (7, 1), (7, 2)));

        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("mais de um item", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_51Itens_Erro()
    {
        // RN-01
        var itens = Enumerable.Range(1, 51).Select(i => (i, 1)).ToArray();

        Assert.False(_validator.Validate(Request(1, itens)).IsValid);
    }

    [Fact]
    public void Validate_ItemNulo_ErroSemExcecao()
    {
        var result = _validator.Validate(new CriarPedidoRequest(1, [null!]));

        Assert.False(result.IsValid);
    }
}

public sealed class ListarPedidosQueryValidatorTests
{
    private readonly ListarPedidosQueryValidator _validator = new();

    [Fact]
    public void Validate_Padrao_Valido() =>
        Assert.True(_validator.Validate(new ListarPedidosQuery(null, null, null)).IsValid);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(1, 500)]
    public void Validate_PaginacaoInvalida_Erro(int page, int pageSize)
    {
        // CA-11 / RN-12
        Assert.False(_validator.Validate(new ListarPedidosQuery(null, null, null, page, pageSize)).IsValid);
    }

    [Fact]
    public void Validate_PaginaGiganteQueEstourariaOffset_Erro()
    {
        // CA-11 / RN-12: (page - 1) * pageSize estourava int → 500.
        Assert.False(_validator.Validate(new ListarPedidosQuery(null, null, null, int.MaxValue, 100)).IsValid);
    }

    [Theory]
    [InlineData(1999, 12, 31)]
    [InlineData(2101, 1, 1)]
    [InlineData(9999, 12, 31)]
    public void Validate_DataForaDoIntervaloSuportado_Erro(int ano, int mes, int dia)
    {
        // CA-11 / RN-13
        var data = new DateOnly(ano, mes, dia);
        Assert.False(_validator.Validate(new ListarPedidosQuery(null, data, null)).IsValid);
        Assert.False(_validator.Validate(new ListarPedidosQuery(null, null, data)).IsValid);
    }

    [Fact]
    public void Validate_DataInicioMaiorQueFim_Erro()
    {
        // CA-11 / RN-13
        var query = new ListarPedidosQuery(null, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1));

        Assert.Contains(_validator.Validate(query).Errors, e => e.PropertyName == "DataFim");
    }

    [Fact]
    public void Validate_MesmoDia_Valido() =>
        Assert.True(_validator.Validate(new ListarPedidosQuery(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1))).IsValid);
}

public sealed class TopClientesQueryValidatorTests
{
    private readonly TopClientesQueryValidator _validator = new();

    [Fact]
    public void Validate_SemDatas_ErroNasDuas()
    {
        // CA-13 / RN-13
        var result = _validator.Validate(new TopClientesQuery(null, null));

        Assert.Contains(result.Errors, e => e.PropertyName == "DataInicio");
        Assert.Contains(result.Errors, e => e.PropertyName == "DataFim");
    }

    [Fact]
    public void Validate_PeriodoAcimaDoMaximo_Erro()
    {
        // RN-15
        var result = _validator.Validate(new TopClientesQuery(new DateOnly(2010, 1, 1), new DateOnly(2026, 1, 1)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_DataPertoDoMaxValue_ErroEmVezDeOverflow() =>
        Assert.False(_validator.Validate(new TopClientesQuery(new DateOnly(9999, 12, 30), new DateOnly(9999, 12, 31))).IsValid);

    [Fact]
    public void Validate_AnoInteiro_Valido() =>
        Assert.True(_validator.Validate(new TopClientesQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))).IsValid);
}
