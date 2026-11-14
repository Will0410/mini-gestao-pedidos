using FluentValidation.Results;
using Pedidos.Domain;
using Pedidos.Domain.Common;

namespace Pedidos.Application.Common;

public static class ValidationExtensions
{
    /// <summary>Converte falhas do FluentValidation no erro VALIDACAO (campos em camelCase).</summary>
    public static Error ToError(this ValidationResult result)
    {
        var detalhes = result.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        return Errors.Validacao(detalhes);
    }

    // "Itens[0].Quantidade" -> "itens[0].quantidade"
    private static string ToCamelCase(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
}
