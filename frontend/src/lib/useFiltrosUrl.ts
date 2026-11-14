import { useCallback, useSyncExternalStore } from 'react';
import type { FiltrosPedidos } from '../api/types';

const PAGE_SIZES = [10, 20, 50, 100] as const;
export const PAGE_SIZE_OPTIONS = PAGE_SIZES;

const DATA_ISO = /^\d{4}-\d{2}-\d{2}$/;

/** Lê e sanitiza os filtros da URL (link compartilhável; valores inválidos voltam ao padrão). */
export function lerFiltros(search: string): FiltrosPedidos {
  const p = new URLSearchParams(search);
  const inteiro = (k: string) => {
    const n = Number(p.get(k));
    return Number.isInteger(n) && n > 0 ? n : undefined;
  };
  const data = (k: string) => {
    const v = p.get(k) ?? '';
    return DATA_ISO.test(v) ? v : undefined;
  };
  const pageSize = inteiro('pageSize');
  return {
    clienteId: inteiro('clienteId'),
    dataInicio: data('dataInicio'),
    dataFim: data('dataFim'),
    page: inteiro('page') ?? 1,
    pageSize: pageSize && (PAGE_SIZES as readonly number[]).includes(pageSize) ? pageSize : 20,
  };
}

function escrever(f: FiltrosPedidos) {
  const p = new URLSearchParams();
  if (f.clienteId) p.set('clienteId', String(f.clienteId));
  if (f.dataInicio) p.set('dataInicio', f.dataInicio);
  if (f.dataFim) p.set('dataFim', f.dataFim);
  if (f.page > 1) p.set('page', String(f.page));
  if (f.pageSize !== 20) p.set('pageSize', String(f.pageSize));
  const qs = p.toString();
  window.history.replaceState(null, '', qs ? `?${qs}` : window.location.pathname);
  window.dispatchEvent(new Event('filtros-url'));
}

const subscribe = (cb: () => void) => {
  window.addEventListener('popstate', cb);
  window.addEventListener('filtros-url', cb);
  return () => {
    window.removeEventListener('popstate', cb);
    window.removeEventListener('filtros-url', cb);
  };
};

/** Estado dos filtros sincronizado com a query string, sem dependência de router. */
export function useFiltrosUrl() {
  const search = useSyncExternalStore(subscribe, () => window.location.search);
  const filtros = lerFiltros(search);

  const atualizar = useCallback((parcial: Partial<FiltrosPedidos>) => {
    const atual = lerFiltros(window.location.search);
    // Qualquer mudança de filtro volta para a página 1 (a menos que a própria página esteja mudando).
    const page = 'page' in parcial ? (parcial.page ?? 1) : 1;
    escrever({ ...atual, ...parcial, page });
  }, []);

  return [filtros, atualizar] as const;
}
