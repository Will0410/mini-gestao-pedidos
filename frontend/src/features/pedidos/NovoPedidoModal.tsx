import { useMemo, useState } from 'react';
import { ApiError } from '../../api/client';
import type { PedidoDetalhe } from '../../api/types';
import { Modal } from '../../components/Modal';
import { formatarMoeda } from '../../lib/format';
import {
  calcularTotal,
  MAX_ITENS,
  novaLinha,
  paraRequest,
  QTD_MAX,
  QTD_MIN,
  temErros,
  validar,
  type FormPedido,
  type LinhaForm,
} from './novoPedido';
import { useClientes, useCriarPedido, useProdutos } from './queries';

interface Props {
  aberto: boolean;
  onFechar: () => void;
  onCriado: (pedido: PedidoDetalhe) => void;
}

const formInicial = (): FormPedido => ({ clienteId: '', linhas: [novaLinha()] });

export function NovoPedidoModal({ aberto, onFechar, onCriado }: Props) {
  const clientes = useClientes();
  const produtos = useProdutos(aberto);
  const criar = useCriarPedido();
  const [form, setForm] = useState<FormPedido>(formInicial);
  const [tentouEnviar, setTentouEnviar] = useState(false);

  const listaProdutos = useMemo(() => produtos.data ?? [], [produtos.data]);
  const porId = useMemo(() => new Map(listaProdutos.map((p) => [p.id, p])), [listaProdutos]);
  const erros = validar(form, listaProdutos);
  const total = calcularTotal(form.linhas, listaProdutos);
  const selecionados = new Set(form.linhas.map((l) => l.produtoId).filter((id): id is number => id !== ''));

  const fechar = () => {
    setForm(formInicial());
    setTentouEnviar(false);
    criar.reset();
    onFechar();
  };

  const atualizarLinha = (key: string, parcial: Partial<LinhaForm>) =>
    setForm((f) => ({ ...f, linhas: f.linhas.map((l) => (l.key === key ? { ...l, ...parcial } : l)) }));

  const enviar = (e: React.FormEvent) => {
    e.preventDefault();
    setTentouEnviar(true);
    if (temErros(erros) || criar.isPending) return;
    criar.mutate(paraRequest(form), {
      onSuccess: (pedido) => {
        onCriado(pedido);
        fechar();
      },
    });
  };

  const erroApi = criar.error instanceof ApiError ? criar.error.mensagemAmigavel : criar.error?.message;

  return (
    <Modal
      aberto={aberto}
      titulo="Novo pedido"
      onFechar={fechar}
      rodape={
        <>
          <div className="total" aria-live="polite">
            Total estimado <strong data-testid="novo-pedido-total">{formatarMoeda(total)}</strong>
          </div>
          <button type="button" className="btn btn--ghost" onClick={fechar}>
            Cancelar
          </button>
          <button type="submit" form="form-novo-pedido" className="btn btn--primary" disabled={criar.isPending} data-testid="salvar-pedido">
            {criar.isPending ? 'Salvando…' : 'Salvar pedido'}
          </button>
        </>
      }
    >
      <form id="form-novo-pedido" onSubmit={enviar} noValidate>
        {erroApi && (
          <div className="alerta alerta--erro" role="alert" data-testid="erro-api">
            {erroApi}
          </div>
        )}

        <label className="campo">
          <span>Cliente</span>
          <select
            data-testid="novo-pedido-cliente"
            value={form.clienteId}
            onChange={(e) => setForm((f) => ({ ...f, clienteId: e.target.value ? Number(e.target.value) : '' }))}
            aria-invalid={tentouEnviar && Boolean(erros.cliente)}
            disabled={clientes.isLoading}
          >
            <option value="">Selecione…</option>
            {clientes.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nome} — {c.email}
              </option>
            ))}
          </select>
          {tentouEnviar && erros.cliente && <small className="campo__erro">{erros.cliente}</small>}
        </label>

        <fieldset className="itens">
          <legend>Itens</legend>
          {produtos.isLoading && <p className="muted">Carregando produtos…</p>}
          {produtos.isError && <p className="alerta alerta--erro">Não foi possível carregar os produtos.</p>}

          {form.linhas.map((linha, i) => {
            const produto = linha.produtoId === '' ? undefined : porId.get(linha.produtoId);
            const subtotal = produto && typeof linha.quantidade === 'number' ? produto.preco * linha.quantidade : 0;
            const erroLinha = tentouEnviar ? erros.linhas[linha.key] : undefined;
            return (
              <div className="item-linha" key={linha.key} data-testid={`item-${i}`}>
                <label className="campo campo--grow">
                  <span className="sr-only">Produto do item {i + 1}</span>
                  <select
                    data-testid={`item-${i}-produto`}
                    value={linha.produtoId}
                    onChange={(e) => atualizarLinha(linha.key, { produtoId: e.target.value ? Number(e.target.value) : '' })}
                    aria-invalid={Boolean(erroLinha)}
                  >
                    <option value="">Produto…</option>
                    {listaProdutos.map((p) => (
                      <option
                        key={p.id}
                        value={p.id}
                        // Produto já usado em outra linha fica desabilitado (RN-05).
                        disabled={(selecionados.has(p.id) && p.id !== linha.produtoId) || p.estoque === 0}
                      >
                        {p.nome} — {formatarMoeda(p.preco)} ({p.estoque} em estoque)
                      </option>
                    ))}
                  </select>
                </label>
                <label className="campo campo--qtd">
                  <span className="sr-only">Quantidade do item {i + 1}</span>
                  <input
                    type="number"
                    inputMode="numeric"
                    min={QTD_MIN}
                    max={Math.min(QTD_MAX, produto?.estoque ?? QTD_MAX)}
                    step={1}
                    data-testid={`item-${i}-quantidade`}
                    value={linha.quantidade}
                    onChange={(e) => atualizarLinha(linha.key, { quantidade: e.target.value === '' ? '' : Number(e.target.value) })}
                    aria-invalid={Boolean(erroLinha)}
                  />
                </label>
                <span className="item-linha__subtotal num">{formatarMoeda(subtotal)}</span>
                <button
                  type="button"
                  className="btn btn--icon"
                  aria-label={`Remover item ${i + 1}`}
                  onClick={() => setForm((f) => ({ ...f, linhas: f.linhas.filter((l) => l.key !== linha.key) }))}
                  disabled={form.linhas.length === 1}
                >
                  ×
                </button>
                {erroLinha && <small className="campo__erro item-linha__erro">{erroLinha}</small>}
              </div>
            );
          })}

          {tentouEnviar && erros.geral && <p className="campo__erro">{erros.geral}</p>}

          <button
            type="button"
            className="btn btn--ghost"
            data-testid="adicionar-item"
            onClick={() => setForm((f) => ({ ...f, linhas: [...f.linhas, novaLinha()] }))}
            disabled={form.linhas.length >= MAX_ITENS}
          >
            + Adicionar item
          </button>
        </fieldset>
        <p className="muted pequeno">O preço final é confirmado pelo servidor no momento da gravação.</p>
      </form>
    </Modal>
  );
}
