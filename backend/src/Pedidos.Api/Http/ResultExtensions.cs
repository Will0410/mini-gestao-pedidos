using Pedidos.Domain.Common;

namespace Pedidos.Api.Http;

/// <summary>
/// Tradução única Error → HTTP (RFC 9457 Problem Details), com o campo "code" estável para o frontend.
/// Validation→400 · NotFound→404 · Unprocessable→422 · Conflict→409.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToProblem(this Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error.Type == ErrorType.Validation)
        {
            return TypedResults.ValidationProblem(
                errors: error.Details ?? new Dictionary<string, string[]>(),
                detail: error.Message,
                title: "Requisição inválida",
                extensions: extensions);
        }

        if (error.Details is not null)
        {
            extensions["details"] = error.Details.ToDictionary(kv => kv.Key, kv => kv.Value.Length == 1 ? (object)kv.Value[0] : kv.Value);
        }

        var (status, title) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            ErrorType.Unprocessable => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflito com o estado atual"),
            _ => (StatusCodes.Status400BadRequest, "Requisição inválida"),
        };

        return TypedResults.Problem(detail: error.Message, statusCode: status, title: title, extensions: extensions);
    }

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
}
