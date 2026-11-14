using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Pedidos.IntegrationTests.Infrastructure;

/// <summary>
/// Base dos testes: HttpClient da API + helpers para preparar dados direto no banco.
/// Cada teste cria seus PRÓPRIOS clientes/produtos (e-mails/SKUs únicos), então os testes
/// rodam em paralelo no mesmo banco sem interferência.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;

    protected IntegrationTestBase(SqlServerFixture sql)
    {
        Sql = sql;
        _factory = new ApiFactory(sql);
        Client = _factory.CreateClient();
    }

    protected SqlServerFixture Sql { get; }

    protected HttpClient Client { get; }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected Task<int> NovoClienteAsync(string nome = "Cliente Teste", bool ativo = true) =>
        ScalarAsync(
            "INSERT INTO dbo.Clientes (Nome, Email, Ativo) OUTPUT INSERTED.Id VALUES (@nome, @email, @ativo)",
            ("@nome", nome),
            ("@email", $"{Guid.NewGuid():N}@teste.com"),
            ("@ativo", ativo));

    protected Task<int> NovoProdutoAsync(decimal preco = 10m, int estoque = 100, string nome = "Produto Teste") =>
        ScalarAsync(
            "INSERT INTO dbo.Produtos (Sku, Nome, Preco, Estoque) OUTPUT INSERTED.Id VALUES (@sku, @nome, @preco, @estoque)",
            ("@sku", Guid.NewGuid().ToString("N")[..30]),
            ("@nome", nome),
            ("@preco", preco),
            ("@estoque", estoque));

    /// <summary>Insere um pedido "histórico" com data controlada (o POST sempre usa a data atual).</summary>
    protected Task<int> NovoPedidoAsync(int clienteId, DateTime dataUtc, decimal valorTotal) =>
        ScalarAsync(
            "INSERT INTO dbo.Pedidos (ClienteId, DataPedido, ValorTotal) OUTPUT INSERTED.Id VALUES (@c, @d, @v)",
            ("@c", clienteId),
            ("@d", dataUtc),
            ("@v", valorTotal));

    protected Task<int> EstoqueAsync(int produtoId) =>
        ScalarAsync("SELECT Estoque FROM dbo.Produtos WHERE Id = @id", ("@id", produtoId));

    protected Task<int> QuantidadePedidosDoClienteAsync(int clienteId) =>
        ScalarAsync("SELECT COUNT(*) FROM dbo.Pedidos WHERE ClienteId = @id", ("@id", clienteId));

    protected async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(Sql.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)Convert.ChangeType((await command.ExecuteScalarAsync(Ct))!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    protected Task<int> ScalarAsync(string sql, params (string Name, object Value)[] parameters) => ScalarAsync<int>(sql, parameters);

    protected Task<HttpResponseMessage> PostPedidoAsync(object body) => Client.PostAsJsonAsync("/pedidos", body, Json, Ct);

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json, Ct);
}
