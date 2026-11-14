import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { CriarPedidoRequest, FiltrosPedidos } from '../../api/types';

export const chaves = {
  pedidos: (f: FiltrosPedidos) => ['pedidos', f] as const,
  todosPedidos: ['pedidos'] as const,
  clientes: ['clientes'] as const,
  produtos: ['produtos'] as const,
  topClientes: (inicio: string, fim: string) => ['top-clientes', inicio, fim] as const,
};

export const usePedidos = (filtros: FiltrosPedidos) =>
  useQuery({
    queryKey: chaves.pedidos(filtros),
    queryFn: () => api.listarPedidos(filtros),
    placeholderData: keepPreviousData, // mantém a tabela durante a troca de página (sem "piscar")
  });

export const useClientes = () =>
  useQuery({ queryKey: chaves.clientes, queryFn: api.listarClientes, staleTime: 5 * 60_000 });

/** Estoque muda a cada pedido: sempre revalida ao abrir o modal. */
export const useProdutos = (habilitado = true) =>
  useQuery({ queryKey: chaves.produtos, queryFn: api.listarProdutos, enabled: habilitado, staleTime: 0 });

export const useTopClientes = (inicio: string, fim: string) =>
  useQuery({
    queryKey: chaves.topClientes(inicio, fim),
    queryFn: () => api.topClientes(inicio, fim),
    enabled: Boolean(inicio && fim && inicio <= fim),
  });

export function useCriarPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CriarPedidoRequest) => api.criarPedido(body),
    onSuccess: () => {
      // Pedido novo afeta: listagem, estoque dos produtos e ranking.
      void qc.invalidateQueries({ queryKey: chaves.todosPedidos });
      void qc.invalidateQueries({ queryKey: chaves.produtos });
      void qc.invalidateQueries({ queryKey: ['top-clientes'] });
    },
  });
}
