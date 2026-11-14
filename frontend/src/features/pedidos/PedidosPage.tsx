import { useEffect, useState } from 'react';
import { ApiError } from '../../api/client';
import type { PedidoDetalhe } from '../../api/types';
import { formatarMoeda, hojeIso } from '../../lib/format';
import { useFiltrosUrl } from '../../lib/useFiltrosUrl';
import { TopClientesPanel } from '../relatorios/TopClientesPanel';
import { FiltrosPedidos } from './FiltrosPedidos';
import { NovoPedidoModal } from './NovoPedidoModal';
import { Paginacao } from './Paginacao';
import { TabelaPedidos } from './TabelaPedidos';
import { usePedidos } from './queries';

export function PedidosPage() {
  const [filtros, setFiltros] = useFiltrosUrl();
  const [modalAberto, setModalAberto] = useState(false);
  const [criado, setCriado] = useState<PedidoDetalhe>();
  const periodoInvalido = Boolean(filtros.dataInicio && filtros.dataFim && filtros.dataInicio > filtros.dataFim);
  const pedidos = usePedidos(filtros);

  // O ranking acompanha o período filtrado; sem filtro, usa o ano corrente até hoje.
  const hoje = hojeIso();
  const inicioTop = filtros.dataInicio ?? `${hoje.slice(0, 4)}-01-01`;
  const fimTop = filtros.dataFim ?? hoje;

  useEffect(() => {
    if (!criado) return;
    const t = setTimeout(() => setCriado(undefined), 6000);
    return () => clearTimeout(t);
  }, [criado]);

  return (
    <div className="layout">
      <header className="topo">
        <div>
          <h1>Pedidos</h1>
          <p className="muted">Consulte, filtre e cadastre pedidos de clientes.</p>
        </div>
        <button type="button" className="btn btn--primary" data-testid="novo-pedido" onClick={() => setModalAberto(true)}>
          + Novo pedido
        </button>
      </header>

      {criado && (
        <div className="alerta alerta--sucesso" role="status" data-testid="pedido-criado">
          Pedido <strong>#{criado.id}</strong> criado para {criado.clienteNome} — {formatarMoeda(criado.valorTotal)}.
        </div>
      )}

      <div className="conteudo">
        <section className="painel painel--principal" aria-label="Lista de pedidos">
          <FiltrosPedidos filtros={filtros} onChange={setFiltros} />

          {periodoInvalido ? null : pedidos.isLoading ? (
            <p className="estado">Carregando pedidos…</p>
          ) : pedidos.isError ? (
            <div className="estado alerta alerta--erro" role="alert">
              {pedidos.error instanceof ApiError ? pedidos.error.mensagemAmigavel : 'Erro ao carregar pedidos.'}
              <button type="button" className="btn btn--ghost" onClick={() => void pedidos.refetch()}>
                Tentar novamente
              </button>
            </div>
          ) : pedidos.data && pedidos.data.items.length === 0 ? (
            <p className="estado" data-testid="lista-vazia">
              Nenhum pedido encontrado para os filtros selecionados.
            </p>
          ) : pedidos.data ? (
            <>
              <TabelaPedidos pedidos={pedidos.data.items} destacarId={criado?.id} atualizando={pedidos.isFetching} />
              <Paginacao
                page={pedidos.data.page}
                pageSize={pedidos.data.pageSize}
                totalItems={pedidos.data.totalItems}
                totalPages={pedidos.data.totalPages}
                onChange={setFiltros}
              />
            </>
          ) : null}
        </section>

        <aside>
          <TopClientesPanel inicio={inicioTop} fim={fimTop} />
        </aside>
      </div>

      <NovoPedidoModal aberto={modalAberto} onFechar={() => setModalAberto(false)} onCriado={setCriado} />
    </div>
  );
}
