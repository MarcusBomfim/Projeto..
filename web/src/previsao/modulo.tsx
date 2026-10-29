import type { Modulo } from '../aplicacao/modulos';
import { TelaDaPrevisao } from './TelaDaPrevisao';

export const modulo: Modulo = {
  rotas: [{ path: 'previsao', element: <TelaDaPrevisao /> }],
  menu: { rotulo: 'Previsão', caminho: '/previsao', icone: 'previsao', ordem: 3 },
};
