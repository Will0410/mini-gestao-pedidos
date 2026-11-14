const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

export const formatarMoeda = (valor: number): string => moeda.format(valor);

/** A API devolve UTC (ISO com "Z"); exibimos no fuso do navegador. */
export const formatarDataHora = (iso: string): string => dataHora.format(new Date(iso));

/** yyyy-MM-dd de hoje no fuso local (para inputs type="date"). */
export function hojeIso(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
