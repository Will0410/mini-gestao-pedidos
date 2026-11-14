// Espelho dos DTOs do backend (backend/src/Pedidos.Application/**/Contracts.cs).
// Mantenha sincronizado ao alterar a API (ver skill new-endpoint, etapa 7).

export interface Cliente {
  id: number;
  nome: string;
  email: string;
}

export interface Produto {
  id: number;
  sku: string;
  nome: string;
  preco: number;
  estoque: number;
}

export interface PedidoResumo {
  id: number;
  clienteId: number;
  clienteNome: string;
  dataPedido: string; // ISO 8601 UTC
  quantidadeItens: number;
  valorTotal: number;
}

export interface ItemPedido {
  produtoId: number;
  produtoNome: string;
  quantidade: number;
  precoUnitario: number;
  subtotal: number;
}

export interface PedidoDetalhe {
  id: number;
  clienteId: number;
  clienteNome: string;
  dataPedido: string;
  valorTotal: number;
  itens: ItemPedido[];
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface TopCliente {
  posicao: number;
  clienteId: number;
  clienteNome: string;
  quantidadePedidos: number;
  valorTotal: number;
}

export interface CriarPedidoRequest {
  clienteId: number;
  itens: { produtoId: number; quantidade: number }[];
}

export interface FiltrosPedidos {
  clienteId?: number;
  dataInicio?: string; // yyyy-MM-dd
  dataFim?: string; // yyyy-MM-dd
  page: number;
  pageSize: number;
}

/** Códigos de erro estáveis da API (docs/spec.md §5). */
export type CodigoErro =
  | 'VALIDACAO'
  | 'CLIENTE_NAO_ENCONTRADO'
  | 'PRODUTO_NAO_ENCONTRADO'
  | 'ESTOQUE_INSUFICIENTE'
  | 'NAO_ENCONTRADO'
  | 'ERRO_INTERNO';

/** Corpo RFC 9457 Problem Details retornado pela API. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: CodigoErro;
  errors?: Record<string, string[]>;
}
