import type { Modulo } from '../aplicacao/modulos';
import { FichaDaConta } from './FichaDaConta';
import { TelaDasContas } from './TelaDasContas';

export const modulo: Modulo = {
  rotas: [
    { path: 'contas', element: <TelaDasContas /> },
    { path: 'contas/:id', element: <FichaDaConta /> },
  ],
  menu: { rotulo: 'Clientes', caminho: '/contas', icone: 'clientes', ordem: 2 },
};
