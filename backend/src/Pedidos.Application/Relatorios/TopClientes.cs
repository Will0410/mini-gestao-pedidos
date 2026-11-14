using FluentValidation;
using Pedidos.Application.Common;

namespace Pedidos.Application.Relatorios;

/// <summary>GET /relatorios/top-clientes — datas obrigatórias (RN-13), período máximo (RN-15).</summary>
public sealed record TopClientesQuery(DateOnly? DataInicio, DateOnly? DataFim)
{
    public const int Limite = 5;
    public const int PeriodoMaximoDias = 366 * 5;
}

public sealed record TopClienteResponse(
    int Posicao,
    int ClienteId,
    string ClienteNome,
    int QuantidadePedidos,
    decimal ValorTotal);

public interface IRelatorioQueries
{
    Task<IReadOnlyList<TopClienteResponse>> TopClientesAsync(DateOnly dataInicio, DateOnly dataFim, CancellationToken ct);
}

public sealed class TopClientesQueryValidator : AbstractValidator<TopClientesQuery>
{
    public TopClientesQueryValidator()
    {
        RuleFor(x => x.DataInicio).NotNull().WithMessage("dataInicio é obrigatória.");
        RuleFor(x => x.DataFim).NotNull().WithMessage("dataFim é obrigatória.");
        RuleFor(x => x.DataInicio).DentroDoPeriodoSuportado("dataInicio");
        RuleFor(x => x.DataFim).DentroDoPeriodoSuportado("dataFim");
        RuleFor(x => x.DataFim)
            .Must((q, fim) => fim!.Value >= q.DataInicio!.Value)
            .WithMessage("dataFim deve ser maior ou igual a dataInicio.")
            .Must((q, fim) => fim!.Value.DayNumber - q.DataInicio!.Value.DayNumber <= TopClientesQuery.PeriodoMaximoDias)
            .WithMessage($"O período não pode exceder {TopClientesQuery.PeriodoMaximoDias} dias.")
            .When(x => x.DataInicio.HasValue && x.DataFim.HasValue);
    }
}
