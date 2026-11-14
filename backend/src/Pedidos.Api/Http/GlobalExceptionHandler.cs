using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Pedidos.Api.Http;

/// <summary>
/// Converte exceções não tratadas em Problem Details SEM vazar detalhes internos (RNF-03).
/// JSON malformado / parâmetro inválido (BadHttpRequestException) vira 400; o resto, 500 genérico + log.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    internal static string SemQuebraDeLinha(string? valor) =>
        (valor ?? string.Empty).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            BadHttpRequestException bad => (bad.StatusCode, "Requisição inválida", "O corpo ou os parâmetros da requisição estão em formato inválido."),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "Requisição cancelada", null),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado. Use o traceId para suporte."),
        };

        // Log forging (CodeQL cs/log-forging): nada que o cliente controla vai cru para o log.
        // No erro 500 registramos o endpoint pelo TEMPLATE da rota (ex.: "HTTP: POST /pedidos/"), definido
        // no código — não o path/método enviados, que podem conter quebras de linha forjadas.
        if (status >= 500)
        {
            var endpoint = context.GetEndpoint()?.DisplayName ?? "(sem endpoint)";
            logger.LogError(exception, "Erro não tratado em {Endpoint}", endpoint);
        }
        else
        {
            logger.LogInformation("Requisição rejeitada ({Status}): {Message}", status, SemQuebraDeLinha(exception.Message));
        }

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { ["code"] = status == 400 ? "VALIDACAO" : "ERRO_INTERNO" },
            },
        });
    }
}
