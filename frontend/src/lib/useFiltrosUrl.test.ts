import { describe, expect, it } from 'vitest';
import { lerFiltros } from './useFiltrosUrl';

describe('lerFiltros', () => {
  it('usa padrões quando a URL está vazia', () => {
    expect(lerFiltros('')).toEqual({ clienteId: undefined, dataInicio: undefined, dataFim: undefined, page: 1, pageSize: 20 });
  });

  it('lê filtros válidos', () => {
    expect(lerFiltros('?clienteId=4&dataInicio=2026-01-01&dataFim=2026-01-31&page=3&pageSize=50')).toEqual({
      clienteId: 4,
      dataInicio: '2026-01-01',
      dataFim: '2026-01-31',
      page: 3,
      pageSize: 50,
    });
  });

  it('descarta valores inválidos ou maliciosos da URL', () => {
    const f = lerFiltros('?clienteId=abc&dataInicio=<script>&page=-1&pageSize=999');
    expect(f).toEqual({ clienteId: undefined, dataInicio: undefined, dataFim: undefined, page: 1, pageSize: 20 });
  });
});
