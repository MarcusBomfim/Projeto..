/*
 * Os números e as datas como o comercial os escreve: real com centavos,
 * probabilidade em porcentagem, mês abreviado — "jan./2027". O que a API
 * manda sem valor (a venda sem valor estimado, sem previsão) aparece como um
 * traço.
 *
 * Data sem hora — o DateOnly da API, "2026-12-15" — é lida como data, e não
 * como meia-noite em UTC: no Brasil, isso cairia no dia anterior. Instante —
 * com fuso — é mostrado na hora de quem lê.
 */

const SemValor = '—';

const UmDia = 24 * 60 * 60 * 1000;

const Moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

const MoedaCompacta = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  notation: 'compact',
  maximumFractionDigits: 1,
});

const Data = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });

const DataEHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

const Meses = [
  'jan.',
  'fev.',
  'mar.',
  'abr.',
  'maio',
  'jun.',
  'jul.',
  'ago.',
  'set.',
  'out.',
  'nov.',
  'dez.',
] as const;

/** "2026-12-15" — a data como a API manda — numa data local, sem fuso no meio. */
export function lerData(texto: string): Date {
  const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(texto);

  if (!partes) {
    throw new Error(`Data fora do formato aaaa-mm-dd: ${texto}`);
  }

  return new Date(Number(partes[1]), Number(partes[2]) - 1, Number(partes[3]));
}

export function formatarMoeda(valor: number | null): string {
  return valor === null ? SemValor : Moeda.format(valor);
}

/** R$ 1,4 mi: para eixos de gráfico e totais de coluna, onde os centavos só atrapalham. */
export function formatarMoedaCompacta(valor: number): string {
  return MoedaCompacta.format(valor);
}

/** A probabilidade da etapa, que a API manda em porcentagem inteira: 40 → "40%". */
export function formatarProbabilidade(percentual: number): string {
  return `${percentual}%`;
}

/** "2026-12-15" → "15/12/2026". */
export function formatarData(texto: string | null): string {
  return texto === null ? SemValor : Data.format(lerData(texto));
}

/** Um instante da API — com fuso — na hora de quem lê: "27/11/2026, 10:00". */
export function formatarDataEHora(instante: string): string {
  return DataEHora.format(new Date(instante));
}

/** "2027-01" — o mês da previsão — → "jan./2027". */
export function formatarMes(texto: string): string {
  const partes = /^(\d{4})-(\d{2})$/.exec(texto);
  const nome = partes ? Meses[Number(partes[2]) - 1] : undefined;

  if (!partes || !nome) {
    throw new Error(`Mês fora do formato aaaa-mm: ${texto}`);
  }

  return `${nome}/${partes[1] ?? ''}`;
}

/** Quantos dias inteiros se passaram desde o instante. */
export function diasDesde(instante: string, agora: Date = new Date()): number {
  return Math.max(Math.floor((agora.getTime() - new Date(instante).getTime()) / UmDia), 0);
}

/** "1 dia", "21 dias" — e "hoje", no dia em que começou. */
export function formatarDias(dias: number): string {
  if (dias === 0) {
    return 'hoje';
  }

  return dias === 1 ? '1 dia' : `${dias} dias`;
}
