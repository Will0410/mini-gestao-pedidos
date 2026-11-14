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
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            BadHttpRequestException bad => (bad.StatusCode, "Requisição inválida", "O corpo ou os parâmetros da requisição estão em formato inválido."),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "Requisição cancelada", null),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado. Use o traceId para suporte."),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogInformation("Requisição rejeitada ({Status}): {Message}", status, exception.Message);
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
