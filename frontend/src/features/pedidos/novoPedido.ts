import type { CriarPedidoRequest, Produto } from '../../api/types';

// Mesmos limites do backend (docs/spec.md RN-01 e RN-04). A validação no cliente é só UX:
// a fonte da verdade continua sendo a API.
export const MAX_ITENS = 50;
export const QTD_MIN = 1;
export const QTD_MAX = 1000;

export interface LinhaForm {
  key: string;
  produtoId: number | '';
  quantidade: number | '';
}

export interface FormPedido {
  clienteId: number | '';
  linhas: LinhaForm[];
}

export interface ErrosForm {
  cliente?: string;
  geral?: string;
  linhas: Record<string, string>;
}

let seq = 0;
export const novaLinha = (): LinhaForm => ({ key: `l${++seq}`, produtoId: '', quantidade: 1 });

export function validar(form: FormPedido, produtos: Produto[]): ErrosForm {
  const erros: ErrosForm = { linhas: {} };
  const porId = new Map(produtos.map((p) => [p.id, p]));

  if (form.clienteId === '') erros.cliente = 'Selecione o cliente.';
  if (form.linhas.length === 0) erros.geral = 'Adicione pelo menos 1 item.';
  if (form.linhas.length > MAX_ITENS) erros.geral = `No máximo ${MAX_ITENS} itens por pedido.`;

  const vistos = new Set<number>();
  for (const linha of form.linhas) {
    if (linha.produtoId === '') {
      erros.linhas[linha.key] = 'Selecione o produto.';
      continue;
    }
    if (vistos.has(linha.produtoId)) {
      erros.linhas[linha.key] = 'Produto repetido: ajuste a quantidade na linha existente.';
      continue;
    }
    vistos.add(linha.produtoId);

    const q = linha.quantidade;
    if (q === '' || !Number.isInteger(q) || q < QTD_MIN || q > QTD_MAX) {
      erros.linhas[linha.key] = `Quantidade entre ${QTD_MIN} e ${QTD_MAX}.`;
      continue;
    }
    const produto = porId.get(linha.produtoId);
    if (produto && q > produto.estoque) {
      erros.linhas[linha.key] = `Estoque insuficiente (disponível: ${produto.estoque}).`;
    }
  }
  return erros;
}

export const temErros = (e: ErrosForm) => Boolean(e.cliente || e.geral || Object.keys(e.linhas).length > 0);

/** Prévia do total. O valor oficial é calculado pelo backend com o preço vigente (RN-07/RN-08). */
export function calcularTotal(linhas: LinhaForm[], produtos: Produto[]): number {
  const porId = new Map(produtos.map((p) => [p.id, p]));
  const centavos = linhas.reduce((acc, l) => {
    const p = l.produtoId === '' ? undefined : porId.get(l.produtoId);
    return p && typeof l.quantidade === 'number' ? acc + Math.round(p.preco * 100) * l.quantidade : acc;
  }, 0);
  return centavos / 100; // soma em centavos inteiros: evita 0.1 + 0.2 = 0.30000000000000004
}

export function paraRequest(form: FormPedido): CriarPedidoRequest {
  return {
    clienteId: Number(form.clienteId),
    itens: form.linhas.map((l) => ({ produtoId: Number(l.produtoId), quantidade: Number(l.quantidade) })),
  };
}
