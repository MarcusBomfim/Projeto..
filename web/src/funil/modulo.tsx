import type { Modulo } from '../aplicacao/modulos';
import { TelaDoFunil } from './TelaDoFunil';

export const modulo: Modulo = {
  rotas: [{ index: true, element: <TelaDoFunil /> }],
  menu: { rotulo: 'Funil', caminho: '/', icone: 'funil', ordem: 1 },
};
