import type {
  Cliente,
  CriarPedidoRequest,
  FiltrosPedidos,
  PagedResponse,
  PedidoDetalhe,
  PedidoResumo,
  ProblemDetails,
  Produto,
  TopCliente,
} from './types';

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api';

/** Erro HTTP da API, com o Problem Details já interpretado. */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Erro HTTP ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** Mensagem pronta para o usuário: detail + erros de campo, sem jargão técnico. */
  get mensagemAmigavel(): string {
    if (this.status === 429) return 'Muitas requisições em sequência. Aguarde alguns segundos e tente novamente.';
    if (this.status >= 500) return 'Erro inesperado no servidor. Tente novamente em instantes.';
    const campos = Object.values(this.problem.errors ?? {}).flat();
    if (this.problem.code === 'VALIDACAO' && campos.length > 0) return campos.join(' ');
    return this.message;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: { Accept: 'application/json', ...(init?.body ? { 'Content-Type': 'application/json' } : {}) },
    });
  } catch {
    throw new ApiError(0, { detail: 'Não foi possível conectar à API. Verifique se o backend está rodando.' });
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails;
    throw new ApiError(response.status, problem);
  }
  return (await response.json()) as T;
}

function toQuery(params: Record<string, string | number | undefined>): string {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== '') qs.set(k, String(v));
  const s = qs.toString();
  return s ? `?${s}` : '';
}

export const api = {
  listarPedidos: (f: FiltrosPedidos) =>
    request<PagedResponse<PedidoResumo>>(`/pedidos${toQuery({ ...f })}`),

  obterPedido: (id: number) => request<PedidoDetalhe>(`/pedidos/${id}`),

  criarPedido: (body: CriarPedidoRequest) =>
    request<PedidoDetalhe>('/pedidos', { method: 'POST', body: JSON.stringify(body) }),

  topClientes: (dataInicio: string, dataFim: string) =>
    request<TopCliente[]>(`/relatorios/top-clientes${toQuery({ dataInicio, dataFim })}`),

  listarClientes: () => request<Cliente[]>('/clientes'),

  listarProdutos: () => request<Produto[]>('/produtos'),
};
