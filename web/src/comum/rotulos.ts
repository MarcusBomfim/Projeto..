import type { Esquemas } from '../api/cliente';

/*
 * Como cada valor dos enums da API se escreve na tela. A API manda o nome do
 * enum — "NaoSabido", "EmQualificacao" —, que é contrato, e não texto para
 * gente. Um enum novo no contrato sem rótulo aqui é erro de compilação.
 */

export const PapeisNaCompra: Readonly<Record<Esquemas['PapelNaCompra'], string>> = {
  NaoSabido: 'Papel não sabido',
  Decisor: 'Decide',
  Influenciador: 'Influencia',
  Financeiro: 'Financeiro',
  Usuario: 'Vai usar a obra',
};

export const SituacoesDaVenda: Readonly<Record<Esquemas['SituacaoDaOportunidade'], string>> = {
  Aberta: 'Aberta',
  Ganha: 'Ganha',
  Perdida: 'Perdida',
};

export const TiposDeAtividade: Readonly<Record<Esquemas['TipoDeAtividade'], string>> = {
  Ligacao: 'Ligação',
  Email: 'E-mail',
  Reuniao: 'Reunião',
  Visita: 'Visita',
  Tarefa: 'Tarefa',
};

export const SituacoesDaAtividade: Readonly<Record<Esquemas['SituacaoDaAtividade'], string>> = {
  Pendente: 'Pendente',
  Concluida: 'Concluída',
  Cancelada: 'Cancelada',
};
