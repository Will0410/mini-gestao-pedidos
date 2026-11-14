using FluentValidation;
using Pedidos.Domain;

namespace Pedidos.Application.Pedidos;

/// <summary>
/// Validação de FORMATO do request (400). Regras que dependem do banco ficam no handler (422/409).
/// As mesmas regras de forma existem em Pedido.Criar — defesa em profundidade: o domínio
/// nunca confia que a borda validou.
/// </summary>
public sealed class CriarPedidoRequestValidator : AbstractValidator<CriarPedidoRequest>
{
    public CriarPedidoRequestValidator()
    {
        RuleFor(x => x.ClienteId)
            .GreaterThan(0).WithMessage("clienteId deve ser maior que zero.");

        // Cascade(Stop): se Itens for nulo/vazio, não avalia os Must seguintes (evita NullReferenceException).
        RuleFor(x => x.Itens)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O pedido deve ter pelo menos 1 item.")                       // RN-01
            .Must(i => i!.Count <= Errors.Pedido.MaxItens)
                .WithMessage($"O pedido pode ter no máximo {Errors.Pedido.MaxItens} itens.")       // RN-01
            .Must(i => i!.Select(x => x.ProdutoId).Distinct().Count() == i!.Count)
                .WithMessage("Um produto não pode aparecer em mais de um item; some as quantidades."); // RN-05

        RuleForEach(x => x.Itens)
            .NotNull().WithMessage("Item do pedido não pode ser nulo.")
            .ChildRules(item =>
        {
            item.RuleFor(i => i.ProdutoId)
                .GreaterThan(0).WithMessage("produtoId deve ser maior que zero.");
            item.RuleFor(i => i.Quantidade)                                                         // RN-04
                .InclusiveBetween(Errors.Pedido.QuantidadeMinima, Errors.Pedido.QuantidadeMaxima)
                .WithMessage($"Quantidade deve estar entre {Errors.Pedido.QuantidadeMinima} e {Errors.Pedido.QuantidadeMaxima}.");
        });
    }
}

public sealed class ListarPedidosQueryValidator : AbstractValidator<ListarPedidosQuery>
{
    public ListarPedidosQueryValidator()
    {
        RuleFor(x => x.ClienteId).GreaterThan(0).When(x => x.ClienteId.HasValue);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page deve ser >= 1.");             // RN-12
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, ListarPedidosQuery.TamanhoMaximo)
            .WithMessage($"pageSize deve estar entre 1 e {ListarPedidosQuery.TamanhoMaximo}.");      // RN-12
        RuleFor(x => x.DataFim)
            .GreaterThanOrEqualTo(x => x.DataInicio!.Value)
            .When(x => x.DataInicio.HasValue && x.DataFim.HasValue)
            .WithMessage("dataFim deve ser maior ou igual a dataInicio.");                           // RN-13
    }
}
