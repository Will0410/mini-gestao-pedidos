using System.Net;
using System.Net.Http.Json;
using Pedidos.Application.Pedidos;
using Pedidos.IntegrationTests.Infrastructure;

namespace Pedidos.IntegrationTests.Pedidos;

public sealed class ListarPedidosTests(SqlServerFixture sql) : IntegrationTestBase(sql)
{
    private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0, int s = 0) => new(y, m, d, h, min, s, DateTimeKind.Utc);

    [Fact]
    public async Task Get_FiltroPorClienteEPeriodo_DatasInclusivasEOrdemDecrescente()
    {
        // CA-09 / RN-11 / RN-13
        var cliente = await NovoClienteAsync();
        var outroCliente = await NovoClienteAsync();
        var antes = await NovoPedidoAsync(cliente, Utc(2030, 2, 28, 23, 59, 59), 1m);        // fora (antes)
        var inicio = await NovoPedidoAsync(cliente, Utc(2030, 3, 1, 0, 0, 0), 2m);          // dentro (1º instante)
        var meio = await NovoPedidoAsync(cliente, Utc(2030, 3, 15, 12, 0, 0), 3m);          // dentro
        var fim = await NovoPedidoAsync(cliente, Utc(2030, 3, 31, 23, 59, 59), 4m);         // dentro (último instante)
        var depois = await NovoPedidoAsync(cliente, Utc(2030, 4, 1, 0, 0, 0), 5m);          // fora (depois)
        await NovoPedidoAsync(outroCliente, Utc(2030, 3, 10), 6m);                          // outro cliente

        var page = await Client.GetFromJsonAsync<PagedResponse<PedidoResumoResponse>>(
            $"/pedidos?clienteId={cliente}&dataInicio=2030-03-01&dataFim=2030-03-31", Json, Ct);

        Assert.NotNull(page);
        Assert.Equal([fim, meio, inicio], page.Items.Select(p => p.Id));
        Assert.Equal(3, page.TotalItems);
        Assert.All(page.Items, p => Assert.Equal(cliente, p.ClienteId));
        Assert.DoesNotContain(page.Items, p => p.Id == antes || p.Id == depois);
    }

    [Fact]
    public async Task Get_Paginacao_25PedidosPageSize10_Pagina3Tem5()
    {
        // CA-10 / RN-12
        var cliente = await NovoClienteAsync();
        for (var i = 0; i < 25; i++)
        {
            await NovoPedidoAsync(cliente, Utc(2031, 1, 1).AddHours(i), i + 1);
        }

        var page = await Client.GetFromJsonAsync<PagedResponse<PedidoResumoResponse>>(
            $"/pedidos?clienteId={cliente}&page=3&pageSize=10", Json, Ct);

        Assert.Equal(5, page!.Items.Count);
        Assert.Equal(25, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(5m, page.Items[0].ValorTotal);   // ordem desc: página 3 começa no 5º mais antigo
    }

    [Fact]
    public async Task Get_PaginaAlemDoFim_RetornaListaVazia()
    {
        var cliente = await NovoClienteAsync();
        await NovoPedidoAsync(cliente, Utc(2031, 6, 1), 1m);

        var page = await Client.GetFromJsonAsync<PagedResponse<PedidoResumoResponse>>(
            $"/pedidos?clienteId={cliente}&page=99", Json, Ct);

        Assert.Empty(page!.Items);
        Assert.Equal(1, page.TotalItems);
    }

    [Theory]
    [InlineData("pageSize=500")]
    [InlineData("page=0")]
    [InlineData("dataInicio=2026-02-01&dataFim=2026-01-01")]
    [InlineData("dataInicio=31-12-2026")]
    [InlineData("page=2147483647&pageSize=100")] // overflow do offset (achado na revisão de segurança)
    [InlineData("dataFim=9999-12-31")]           // AddDays(1) estourava DateOnly.MaxValue
    [InlineData("dataInicio=1753-01-01")]
    public async Task Get_ParametrosInvalidos_Retorna400(string query)
    {
        // CA-11
        var response = await Client.GetAsync($"/pedidos?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_Health_RetornaHealthy()
    {
        var response = await Client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }
}
