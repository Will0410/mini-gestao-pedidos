using FluentValidation;
using Microsoft.Extensions.Logging;
using Pedidos.Application.Abstractions;
using Pedidos.Application.Common;
using Pedidos.Domain;
using Pedidos.Domain.Common;
using Pedidos.Domain.Entities;

namespace Pedidos.Application.Pedidos;

/// <summary>
/// Caso de uso: criar pedido (POST /pedidos).
/// Fluxo: valida formato → cliente existe → produtos existem → regras do agregado → transação
/// com baixa atômica de estoque → persiste pedido.
/// </summary>
public sealed class CriarPedidoHandler(
    IValidator<CriarPedidoRequest> validator,
    IClienteRepository clientes,
    IProdutoRepository produtos,
    IPedidoRepository pedidos,
    IUnitOfWork unitOfWork,
    TimeProvider relogio,
    ILogger<CriarPedidoHandler> logger)
{
    public async Task<Result<PedidoDetalheResponse>> HandleAsync(CriarPedidoRequest request, CancellationToken ct)
    {
        // 1. Formato (400)
        var validacao = await validator.ValidateAsync(request, ct);
        if (!validacao.IsValid)
        {
            return validacao.ToError();
        }

        var itens = request.Itens!;

        // 2. Cliente existe e está ativo (RN-02 → 422)
        var cliente = await clientes.ObterAtivoAsync(request.ClienteId, ct);
        if (cliente is null)
        {
            return Errors.Cliente.NaoEncontrado(request.ClienteId);
        }

        // 3. Produtos existem e estão ativos (RN-03 → 422). Uma única query para todos os ids (sem N+1).
        var ids = itens.Select(i => i.ProdutoId).ToArray();
        var encontrados = (await produtos.ObterAtivosPorIdsAsync(ids, ct)).ToDictionary(p => p.Id);
        var faltantes = ids.Where(id => !encontrados.ContainsKey(id)).ToArray();
        if (faltantes.Length > 0)
        {
            return Errors.Produto.NaoEncontrado(faltantes);
        }

        // 4. Regras do agregado (inclui checagem prévia de estoque para mensagem amigável).
        var linhas = itens.Select(i => new LinhaPedido(encontrados[i.ProdutoId], i.Quantidade)).ToArray();
        var criacao = Pedido.Criar(cliente.Id, linhas, relogio.GetUtcNow().UtcDateTime);
        if (criacao.IsFailure)
        {
            return criacao.Error!;
        }

        var pedido = criacao.Value;

        // 5. Transação: baixa atômica de estoque (RN-06, RN-10) + insert do pedido.
        var persistido = await unitOfWork.ExecutarEmTransacaoAsync<Pedido>(
            async token =>
            {
                // Ordena por ProdutoId: todas as transações travam linhas na mesma ordem → evita deadlock
                // entre pedidos concorrentes com produtos em comum.
                foreach (var item in pedido.Itens.OrderBy(i => i.ProdutoId))
                {
                    if (!await produtos.TentarBaixarEstoqueAsync(item.ProdutoId, item.Quantidade, token))
                    {
                        // Outro pedido levou o estoque entre a leitura (passo 3) e a escrita.
                        var disponivel = await produtos.ObterEstoqueAsync(item.ProdutoId, token);
                        var produto = encontrados[item.ProdutoId];
                        logger.LogInformation(
                            "Corrida de estoque perdida: produto {ProdutoId}, solicitado {Solicitado}, disponível {Disponivel}",
                            item.ProdutoId,
                            item.Quantidade,
                            disponivel);
                        return Errors.Pedido.EstoqueInsuficiente(produto.Id, produto.Nome, item.Quantidade, disponivel);
                    }
                }

                pedidos.Adicionar(pedido);
                return pedido;
            },
            ct);

        if (persistido.IsFailure)
        {
            return persistido.Error!;
        }

        logger.LogInformation(
            "Pedido {PedidoId} criado para o cliente {ClienteId} com {QtdItens} itens, total {ValorTotal}",
            pedido.Id,
            pedido.ClienteId,
            pedido.Itens.Count,
            pedido.ValorTotal);

        return new PedidoDetalheResponse(
            pedido.Id,
            cliente.Id,
            cliente.Nome,
            pedido.DataPedido,
            pedido.ValorTotal,
            [.. pedido.Itens.Select(i => new ItemPedidoResponse(
                i.ProdutoId,
                encontrados[i.ProdutoId].Nome,
                i.Quantidade,
                i.PrecoUnitario,
                i.Subtotal))]);
    }
}
