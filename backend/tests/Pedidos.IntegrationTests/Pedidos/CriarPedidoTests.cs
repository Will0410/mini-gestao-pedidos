using System.Net;
using System.Net.Http.Json;
using Pedidos.Application.Pedidos;
using Pedidos.IntegrationTests.Infrastructure;

namespace Pedidos.IntegrationTests.Pedidos;

public sealed class CriarPedidoTests(SqlServerFixture sql) : IntegrationTestBase(sql)
{
    [Fact]
    public async Task Post_PedidoValido_Retorna201ComLocationTotalCorretoEBaixaEstoque()
    {
        // CA-01
        var cliente = await NovoClienteAsync("Ana Integração");
        var teclado = await NovoProdutoAsync(preco: 99.90m, estoque: 10);
        var mouse = await NovoProdutoAsync(preco: 25.50m, estoque: 5);

        var response = await PostPedidoAsync(new
        {
            clienteId = cliente,
            itens = new[] { new { produtoId = teclado, quantidade = 2 }, new { produtoId = mouse, quantidade = 3 } },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var criado = await response.Content.ReadFromJsonAsync<PedidoDetalheResponse>(Json, Ct);
        Assert.NotNull(criado);
        Assert.Equal(276.30m, criado.ValorTotal);                  // 2×99.90 + 3×25.50
        Assert.Equal($"/pedidos/{criado.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal(8, await EstoqueAsync(teclado));
        Assert.Equal(2, await EstoqueAsync(mouse));

        // Consistência do ValorTotal desnormalizado com os itens gravados (docs/spec.md §4.2).
        var somaItens = await ScalarAsync<decimal>(
            "SELECT SUM(Quantidade * PrecoUnitario) FROM dbo.ItensPedido WHERE PedidoId = @id", ("@id", criado.Id));
        Assert.Equal(criado.ValorTotal, somaItens);

        // O Location aponta para um recurso real e idêntico ao retornado no POST.
        var detalhe = await Client.GetFromJsonAsync<PedidoDetalheResponse>(response.Headers.Location, Json, Ct);
        Assert.Equal(criado.ValorTotal, detalhe!.ValorTotal);
        Assert.Equal(criado.DataPedido, detalhe.DataPedido);
        Assert.Equal(2, detalhe.Itens.Count);
    }

    [Fact]
    public async Task Post_SemItens_Retorna400ComErroEmItens()
    {
        // CA-02
        var cliente = await NovoClienteAsync();

        var response = await PostPedidoAsync(new { clienteId = cliente, itens = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("VALIDACAO", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("itens", out _));
    }

    [Fact]
    public async Task Post_ClienteInexistente_Retorna422ENadaEGravado()
    {
        // CA-03
        var produto = await NovoProdutoAsync(estoque: 10);

        var response = await PostPedidoAsync(new { clienteId = int.MaxValue, itens = new[] { new { produtoId = produto, quantidade = 1 } } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("CLIENTE_NAO_ENCONTRADO", (await ReadJsonAsync(response)).GetProperty("code").GetString());
        Assert.Equal(10, await EstoqueAsync(produto));
    }

    [Fact]
    public async Task Post_ClienteInativo_Retorna422()
    {
        // RN-02
        var cliente = await NovoClienteAsync(ativo: false);
        var produto = await NovoProdutoAsync();

        var response = await PostPedidoAsync(new { clienteId = cliente, itens = new[] { new { produtoId = produto, quantidade = 1 } } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Post_EstoqueInsuficienteNoSegundoItem_Retorna409ENaoAlteraNenhumEstoque()
    {
        // CA-05 / RN-06 (atomicidade)
        var cliente = await NovoClienteAsync();
        var comEstoque = await NovoProdutoAsync(estoque: 10);
        var semEstoque = await NovoProdutoAsync(estoque: 1, nome: "Raro");

        var response = await PostPedidoAsync(new
        {
            clienteId = cliente,
            itens = new[] { new { produtoId = comEstoque, quantidade = 5 }, new { produtoId = semEstoque, quantidade = 2 } },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("ESTOQUE_INSUFICIENTE", body.GetProperty("code").GetString());
        Assert.Equal("1", body.GetProperty("details").GetProperty("disponivel").GetString());
        Assert.Equal(10, await EstoqueAsync(comEstoque));
        Assert.Equal(1, await EstoqueAsync(semEstoque));
        Assert.Equal(0, await QuantidadePedidosDoClienteAsync(cliente));
    }

    [Fact]
    public async Task Post_PrecoEnviadoPeloCliente_EIgnorado()
    {
        // CA-08 / RN-07: tentativa de tampering com campo extra de preço.
        var cliente = await NovoClienteAsync();
        var produto = await NovoProdutoAsync(preco: 500m);

        var response = await PostPedidoAsync(new
        {
            clienteId = cliente,
            itens = new[] { new { produtoId = produto, quantidade = 1, precoUnitario = 0.01m } },
            valorTotal = 0.01m,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var criado = await response.Content.ReadFromJsonAsync<PedidoDetalheResponse>(Json, Ct);
        Assert.Equal(500m, criado!.ValorTotal);
        Assert.Equal(500m, criado.Itens.Single().PrecoUnitario);
    }

    [Fact]
    public async Task Post_20PedidosConcorrentesParaEstoque5_Exatamente5SucessosEEstoqueZero()
    {
        // CA-07 / RN-10: prova que não há venda acima do estoque sob concorrência real no SQL Server.
        var produto = await NovoProdutoAsync(estoque: 5);
        var clientes = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => NovoClienteAsync()));

        using var largada = new SemaphoreSlim(0);
        var tarefas = clientes.Select(async c =>
        {
            await largada.WaitAsync(Ct);
            return await PostPedidoAsync(new { clienteId = c, itens = new[] { new { produtoId = produto, quantidade = 1 } } });
        }).ToArray();
        largada.Release(tarefas.Length);
        var respostas = await Task.WhenAll(tarefas);

        Assert.Equal(5, respostas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(15, respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await EstoqueAsync(produto));
        Assert.Equal(5, await ScalarAsync("SELECT COUNT(*) FROM dbo.ItensPedido WHERE ProdutoId = @p", ("@p", produto)));
    }

    [Fact]
    public async Task Post_JsonMalformado_Retorna400SemStackTrace()
    {
        using var content = new StringContent("{\"clienteId\": ", System.Text.Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/pedidos", content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var texto = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("   at ", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_PedidoInexistente_Retorna404()
    {
        var response = await Client.GetAsync("/pedidos/2147483647", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
