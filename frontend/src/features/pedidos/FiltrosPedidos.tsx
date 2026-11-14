import type { FiltrosPedidos as Filtros } from '../../api/types';
import { useClientes } from './queries';

interface Props {
  filtros: Filtros;
  onChange: (parcial: Partial<Filtros>) => void;
}

export function FiltrosPedidos({ filtros, onChange }: Props) {
  const clientes = useClientes();
  const periodoInvalido = Boolean(filtros.dataInicio && filtros.dataFim && filtros.dataInicio > filtros.dataFim);
  const temFiltro = Boolean(filtros.clienteId || filtros.dataInicio || filtros.dataFim);

  return (
    <form className="filtros" role="search" aria-label="Filtros de pedidos" onSubmit={(e) => e.preventDefault()}>
      <label className="campo">
        <span>Cliente</span>
        <select
          data-testid="filtro-cliente"
          value={filtros.clienteId ?? ''}
          onChange={(e) => onChange({ clienteId: e.target.value ? Number(e.target.value) : undefined })}
          disabled={clientes.isLoading}
        >
          <option value="">Todos os clientes</option>
          {clientes.data?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.nome} — {c.email}
            </option>
          ))}
        </select>
      </label>

      <label className="campo">
        <span>Data início</span>
        <input
          type="date"
          data-testid="filtro-data-inicio"
          value={filtros.dataInicio ?? ''}
          onChange={(e) => onChange({ dataInicio: e.target.value || undefined })}
          aria-invalid={periodoInvalido}
        />
      </label>

      <label className="campo">
        <span>Data fim</span>
        <input
          type="date"
          data-testid="filtro-data-fim"
          value={filtros.dataFim ?? ''}
          min={filtros.dataInicio}
          onChange={(e) => onChange({ dataFim: e.target.value || undefined })}
          aria-invalid={periodoInvalido}
        />
      </label>

      <button
        type="button"
        className="btn btn--ghost"
        disabled={!temFiltro}
        onClick={() => onChange({ clienteId: undefined, dataInicio: undefined, dataFim: undefined })}
      >
        Limpar filtros
      </button>

      {periodoInvalido && (
        <p className="filtros__erro" role="alert">
          A data fim deve ser igual ou posterior à data início.
        </p>
      )}
    </form>
  );
}
