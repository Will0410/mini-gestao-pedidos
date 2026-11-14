using FluentValidation;

namespace Pedidos.Application.Common;

/// <summary>
/// Intervalo de datas aceito nos filtros (RN-13). Além de fazer sentido para o negócio, evita
/// overflow em <c>dataFim.AddDays(1)</c> (DateOnly.MaxValue) e datas fora do suportado pelo banco.
/// </summary>
public static class Periodo
{
    public static readonly DateOnly Minimo = new(2000, 1, 1);
    public static readonly DateOnly Maximo = new(2100, 12, 31);

    public static IRuleBuilderOptions<T, DateOnly?> DentroDoPeriodoSuportado<T>(this IRuleBuilder<T, DateOnly?> rule, string campo) =>
        rule.Must(d => d is null || (d.Value >= Minimo && d.Value <= Maximo))
            .WithMessage($"{campo} deve estar entre {Minimo:yyyy-MM-dd} e {Maximo:yyyy-MM-dd}.");
}
