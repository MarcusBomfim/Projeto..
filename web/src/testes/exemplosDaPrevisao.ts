import type { Esquemas } from '../api/cliente';

/*
 * A previsão do exemplo do README, na primeira segunda de janeiro de 2027,
 * como a API a devolve (conferida pelo teste da API do exemplo do README).
 * Tipada pelo contrato: se a API mudar o formato, não compila.
 */

function venda(
  id: string,
  titulo: string,
  valorEstimado: number | null,
  previsaoDeFechamento: string | null,
): Esquemas['VendaNoRelatorioResposta'] {
  return {
    id,
    titulo,
    etapa: '01a16700-0000-7000-8000-00000000e004',
    responsavel: 'carlos.souza',
    valorEstimado,
    previsaoDeFechamento,
  };
}

export const escritorio = venda(
  '01a16700-0000-7000-8000-00000000b002',
  'Escritório de Campinas',
  600_000,
  '2027-01-29',
);

export const portaria = venda(
  '01a16700-0000-7000-8000-00000000b003',
  'Portaria de Jundiaí',
  150_000,
  '2027-02-26',
);

export const estacionamento = venda(
  '01a16700-0000-7000-8000-00000000b004',
  'Estacionamento',
  400_000,
  '2026-12-18',
);

export const previsaoDoReadme: Esquemas['PrevisaoResposta'] = {
  meses: [
    {
      mes: '2027-01',
      ganho: 0,
      emAberto: 600_000,
      ponderado: 360_000,
      previsto: 360_000,
      abertas: [escritorio],
    },
    { mes: '2027-02', ganho: 0, emAberto: 150_000, ponderado: 15_000, previsto: 15_000, abertas: [portaria] },
    { mes: '2027-03', ganho: 0, emAberto: 0, ponderado: 0, previsto: 0, abertas: [] },
  ],
  ganho: 0,
  ponderado: 375_000,
  previsto: 375_000,
  vencidas: [estacionamento],
  semValor: [],
  semPrevisao: [],
};
