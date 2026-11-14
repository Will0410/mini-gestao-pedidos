using Pedidos.Api.Http;

namespace Pedidos.IntegrationTests.Seguranca;

public sealed class LogForgingTests
{
    [Theory]
    [InlineData("/pedidos\r\n[INFO] Admin logado", "/pedidos[INFO] Admin logado")]
    [InlineData("/pedidos\nlinha falsa", "/pedidoslinha falsa")]
    [InlineData(null, "")]
    public void SemQuebraDeLinha_RemoveCrLf_ImpedindoLinhasFalsasNoLog(string? entrada, string esperado)
    {
        // Achado do CodeQL (cs/log-forging): dados do cliente vão para o log sem permitir quebrar linha.
        Assert.Equal(esperado, GlobalExceptionHandler.SemQuebraDeLinha(entrada));
    }
}
