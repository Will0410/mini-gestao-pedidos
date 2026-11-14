import { ApiError } from '../../api/client';
import { formatarMoeda } from '../../lib/format';
import { useTopClientes } from '../pedidos/queries';

interface Props {
  inicio: string;
  fim: string;
}

export function TopClientesPanel({ inicio, fim }: Props) {
  const top = useTopClientes(inicio, fim);
  const max = top.data?.[0]?.valorTotal ?? 0;
  const fmt = (iso: string) => iso.split('-').reverse().join('/');

  return (
    <section className="painel" aria-labelledby="top-titulo" data-testid="top-clientes">
      <header className="painel__header">
        <h2 id="top-titulo">Top 5 clientes</h2>
        <span className="muted pequeno">
          {fmt(inicio)} a {fmt(fim)}
        </span>
      </header>

      {top.isLoading && <p className="muted">Carregando…</p>}
      {top.isError && (
        <p className="alerta alerta--erro">{top.error instanceof ApiError ? top.error.mensagemAmigavel : 'Erro ao carregar.'}</p>
      )}
      {top.data?.length === 0 && <p className="muted">Nenhum pedido no período.</p>}

      <ol className="ranking">
        {top.data?.map((c) => (
          <li key={c.clienteId} className="ranking__item">
            <div className="ranking__linha">
              <span className="ranking__pos">{c.posicao}</span>
              <span className="ranking__nome" title={`Cliente #${c.clienteId}`}>
                {c.clienteNome} <span className="muted pequeno">#{c.clienteId}</span>
              </span>
              <span className="num">{formatarMoeda(c.valorTotal)}</span>
            </div>
            <div className="ranking__barra" aria-hidden="true">
              <span style={{ width: `${max ? (c.valorTotal / max) * 100 : 0}%` }} />
            </div>
            <span className="muted pequeno">{c.quantidadePedidos} pedidos</span>
          </li>
        ))}
      </ol>
    </section>
  );
}
