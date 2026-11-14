import { describe, expect, it } from 'vitest';
import type { Produto } from '../../api/types';
import { calcularTotal, paraRequest, temErros, validar, type FormPedido } from './novoPedido';

const produtos: Produto[] = [
  { id: 1, sku: 'A', nome: 'Teclado', preco: 0.1, estoque: 10 },
  { id: 2, sku: 'B', nome: 'Mouse', preco: 0.2, estoque: 2 },
];

const form = (parcial: Partial<FormPedido> = {}): FormPedido => ({
  clienteId: 1,
  linhas: [{ key: 'a', produtoId: 1, quantidade: 1 }],
  ...parcial,
});

describe('validar (espelha RN-01, RN-04, RN-05, RN-06 do backend)', () => {
  it('aceita formulário válido', () => {
    expect(temErros(validar(form(), produtos))).toBe(false);
  });

  it('exige cliente', () => {
    expect(validar(form({ clienteId: '' }), produtos).cliente).toBeDefined();
  });

  it('exige ao menos um item', () => {
    expect(validar(form({ linhas: [] }), produtos).geral).toMatch(/pelo menos 1/);
  });

  it('rejeita produto repetido', () => {
    const e = validar(form({ linhas: [{ key: 'a', produtoId: 1, quantidade: 1 }, { key: 'b', produtoId: 1, quantidade: 1 }] }), produtos);
    expect(e.linhas.b).toMatch(/repetido/);
  });

  it.each([0, 1001, 1.5, ''] as const)('rejeita quantidade %s', (quantidade) => {
    expect(validar(form({ linhas: [{ key: 'a', produtoId: 1, quantidade }] }), produtos).linhas.a).toBeDefined();
  });

  it('rejeita quantidade acima do estoque exibido', () => {
    const e = validar(form({ linhas: [{ key: 'a', produtoId: 2, quantidade: 3 }] }), produtos);
    expect(e.linhas.a).toMatch(/disponível: 2/);
  });
});

describe('calcularTotal', () => {
  it('soma em centavos sem erro de ponto flutuante', () => {
    const linhas = [
      { key: 'a', produtoId: 1, quantidade: 1 },
      { key: 'b', produtoId: 2, quantidade: 1 },
    ] as const;
    expect(calcularTotal([...linhas], produtos)).toBe(0.3); // 0.1 + 0.2 em float seria 0.30000000000000004
  });

  it('ignora linhas incompletas', () => {
    expect(calcularTotal([{ key: 'a', produtoId: '', quantidade: 3 }], produtos)).toBe(0);
  });
});

describe('paraRequest', () => {
  it('não envia preço (RN-07)', () => {
    const body = paraRequest(form());
    expect(body).toEqual({ clienteId: 1, itens: [{ produtoId: 1, quantidade: 1 }] });
    expect(JSON.stringify(body)).not.toMatch(/preco/i);
  });
});
