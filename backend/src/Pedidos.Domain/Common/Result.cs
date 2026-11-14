namespace Pedidos.Domain.Common;

/// <summary>Categoria do erro — a camada Api traduz para status HTTP.</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Unprocessable,
    Conflict,
}

/// <summary>
/// Erro de negócio. <see cref="Code"/> é contrato estável com o frontend (ver docs/spec.md §5).
/// </summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? Details = null);

/// <summary>
/// Result pattern: erros de negócio são valores, não exceções (docs/plan.md A5).
/// </summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error)
        : base(error) => _value = value;

    /// <summary>Valor do sucesso. Acessar em caso de falha é bug de programação.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result em falha ({Error!.Code}) não possui valor.");

    public static Result<T> Success(T value) => new(value, null);

    public static new Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
