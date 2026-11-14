import { PAGE_SIZE_OPTIONS } from '../../lib/useFiltrosUrl';

interface Props {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  onChange: (p: { page?: number; pageSize?: number }) => void;
}

export function Paginacao({ page, pageSize, totalItems, totalPages, onChange }: Props) {
  const inicio = totalItems === 0 ? 0 : (page - 1) * pageSize + 1;
  const fim = Math.min(page * pageSize, totalItems);

  return (
    <nav className="paginacao" aria-label="Paginação">
      <span data-testid="paginacao-resumo">
        {inicio}–{fim} de {totalItems.toLocaleString('pt-BR')}
      </span>
      <label className="paginacao__tamanho">
        Por página
        <select value={pageSize} onChange={(e) => onChange({ pageSize: Number(e.target.value) })}>
          {PAGE_SIZE_OPTIONS.map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
      </label>
      <div className="paginacao__botoes">
        <button type="button" className="btn btn--ghost" disabled={page <= 1} onClick={() => onChange({ page: page - 1 })}>
          ‹ Anterior
        </button>
        <span aria-current="page">
          Página {page} de {Math.max(totalPages, 1)}
        </span>
        <button
          type="button"
          className="btn btn--ghost"
          disabled={page >= totalPages}
          onClick={() => onChange({ page: page + 1 })}
        >
          Próxima ›
        </button>
      </div>
    </nav>
  );
}
