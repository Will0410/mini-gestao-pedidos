import type { PedidoResumo } from '../../api/types';
import { formatarDataHora, formatarMoeda } from '../../lib/format';

interface Props {
  pedidos: PedidoResumo[];
  destacarId?: number;
  atualizando: boolean;
}

export function TabelaPedidos({ pedidos, destacarId, atualizando }: Props) {
  return (
    <div className="tabela-wrapper" aria-busy={atualizando}>
      <table className="tabela" data-testid="tabela-pedidos">
        <caption className="sr-only">Pedidos</caption>
        <thead>
          <tr>
            <th scope="col">Pedido</th>
            <th scope="col">Cliente</th>
            <th scope="col">Data</th>
            <th scope="col" className="num">
              Itens
            </th>
            <th scope="col" className="num">
              Total
            </th>
          </tr>
        </thead>
        <tbody>
          {pedidos.map((p) => (
            <tr key={p.id} data-testid={`pedido-${p.id}`} className={p.id === destacarId ? 'tabela__linha--nova' : undefined}>
              <td className="mono">#{p.id}</td>
              <td>{p.clienteNome}</td>
              <td>{formatarDataHora(p.dataPedido)}</td>
              <td className="num">{p.quantidadeItens}</td>
              <td className="num">{formatarMoeda(p.valorTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
