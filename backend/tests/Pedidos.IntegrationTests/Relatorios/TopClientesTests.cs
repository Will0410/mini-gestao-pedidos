using System.Net;
using System.Net.Http.Json;
using Pedidos.Application.Relatorios;
using Pedidos.IntegrationTests.Infrastructure;

namespace Pedidos.IntegrationTests.Relatorios;

public sealed class TopClientesTests(SqlServerFixture sql) : IntegrationTestBase(sql)
{
    // Período exclusivo deste teste (ano 2040) para não colidir com dados de outros testes no mesmo banco.
    private static DateTime Em2040(int mes, int dia) => new(2040, mes, dia, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TopClientes_RetornaNoMaximo5PorValorDescIgnorandoForaDoPeriodoESemFundirHomonimos()
    {
        // CA-12 / RN-14 — inclui as armadilhas da Parte 4: homônimos e pedidos fora do período.
        var homonimoA = await NovoClienteAsync("Homônimo");
        var homonimoB = await NovoClienteAsync("Homônimo");
        var c3 = await NovoClienteAsync("C3");
        var c4 = await NovoClienteAsync("C4");
        var c5 = await NovoClienteAsync("C5");
        var c6 = await NovoClienteAsync("C6");
        var foraDoPeriodo = await NovoClienteAsync("Fora");

        await NovoPedidoAsync(homonimoA, Em2040(1, 10), 300m);
        await NovoPedidoAsync(homonimoB, Em2040(2, 10), 250m);
        await NovoPedidoAsync(c3, Em2040(3, 10), 200m);
        await NovoPedidoAsync(c3, Em2040(3, 11), 200m);   // c3 soma 400 em 2 pedidos
        await NovoPedidoAsync(c4, Em2040(4, 10), 150m);
        await NovoPedidoAsync(c5, Em2040(5, 10), 100m);
        await NovoPedidoAsync(c6, Em2040(6, 10), 50m);    // 6º lugar: não deve aparecer
        await NovoPedidoAsync(foraDoPeriodo, new DateTime(2041, 1, 1, 0, 0, 0, DateTimeKind.Utc), 99999m);

        var ranking = await Client.GetFromJsonAsync<List<TopClienteResponse>>(
            "/relatorios/top-clientes?dataInicio=2040-01-01&dataFim=2040-12-31", Json, Ct);

        Assert.NotNull(ranking);
        Assert.Equal([c3, homonimoA, homonimoB, c4, c5], ranking.Select(r => r.ClienteId));
        Assert.Equal([1, 2, 3, 4, 5], ranking.Select(r => r.Posicao));
        Assert.Equal(400m, ranking[0].ValorTotal);
        Assert.Equal(2, ranking[0].QuantidadePedidos);
        Assert.DoesNotContain(ranking, r => r.ClienteId == foraDoPeriodo || r.ClienteId == c6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?dataInicio=2026-01-01")]
    [InlineData("?dataFim=2026-12-31")]
    [InlineData("?dataInicio=2026-12-31&dataFim=2026-01-01")]
    [InlineData("?dataInicio=9999-12-30&dataFim=9999-12-31")] // estourava DateOnly.MaxValue (500)
    public async Task TopClientes_SemDatasOuPeriodoInvertido_Retorna400(string query)
    {
        // CA-13
        var response = await Client.GetAsync($"/relatorios/top-clientes{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TopClientes_PeriodoSemPedidos_RetornaListaVazia()
    {
        var ranking = await Client.GetFromJsonAsync<List<TopClienteResponse>>(
            "/relatorios/top-clientes?dataInicio=2099-01-01&dataFim=2099-12-31", Json, Ct);

        Assert.Empty(ranking!);
    }
}
