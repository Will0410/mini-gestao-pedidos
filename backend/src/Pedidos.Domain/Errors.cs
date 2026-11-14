using System.Globalization;
using Pedidos.Domain.Common;

namespace Pedidos.Domain;

/// <summary>Catálogo central de erros de negócio. Códigos documentados em docs/spec.md §5.</summary>
public static class Errors
{
    public const string CodigoValidacao = "VALIDACAO";

    public static Error Validacao(IReadOnlyDictionary<string, string[]> detalhes) =>
        new(CodigoValidacao, "Um ou mais campos são inválidos.", ErrorType.Validation, detalhes);

    public static Error Validacao(string campo, string mensagem) =>
        Validacao(new Dictionary<string, string[]> { [campo] = [mensagem] });

    public static class Pedido
    {
        public const int MaxItens = 50;
        public const int QuantidadeMinima = 1;
        public const int QuantidadeMaxima = 1000;

        public static Error NaoEncontrado(int id) =>
            new("NAO_ENCONTRADO", $"Pedido {id} não encontrado.", ErrorType.NotFound);

        public static Error SemItens() => Validacao("itens", "O pedido deve ter pelo menos 1 item.");

        public static Error ItensDemais() => Validacao("itens", $"O pedido pode ter no máximo {MaxItens} itens.");

        public static Error QuantidadeInvalida(int produtoId) => Validacao(
            "itens",
            $"Quantidade do produto {produtoId} deve estar entre {QuantidadeMinima} e {QuantidadeMaxima}.");

        public static Error ProdutoRepetido(int produtoId) => Validacao(
            "itens",
            $"O produto {produtoId} aparece mais de uma vez; some as quantidades em um único item.");

        public static Error EstoqueInsuficiente(int produtoId, string produtoNome, int solicitado, int disponivel) => new(
            "ESTOQUE_INSUFICIENTE",
            string.Create(
                CultureInfo.InvariantCulture,
                $"Estoque insuficiente para '{produtoNome}' (produto {produtoId}): solicitado {solicitado}, disponível {disponivel}."),
            ErrorType.Conflict,
            new Dictionary<string, string[]>
            {
                ["produtoId"] = [produtoId.ToString(CultureInfo.InvariantCulture)],
                ["solicitado"] = [solicitado.ToString(CultureInfo.InvariantCulture)],
                ["disponivel"] = [disponivel.ToString(CultureInfo.InvariantCulture)],
            });
    }

    public static class Cliente
    {
        public static Error NaoEncontrado(int id) =>
            new("CLIENTE_NAO_ENCONTRADO", $"Cliente {id} não encontrado ou inativo.", ErrorType.Unprocessable);
    }

    public static class Produto
    {
        public static Error NaoEncontrado(IEnumerable<int> ids) => new(
            "PRODUTO_NAO_ENCONTRADO",
            $"Produto(s) não encontrado(s) ou inativo(s): {string.Join(", ", ids)}.",
            ErrorType.Unprocessable);
    }
}
