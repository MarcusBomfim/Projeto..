import { render } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { Aplicacao } from '../aplicacao/Aplicacao';
import { criarConsultas } from '../aplicacao/consultas';
import { criarRotas } from '../aplicacao/rotas';
import { guardarToken } from '../sessao/token';
import { tokenDeTeste } from './tokens';

/**
 * A aplicação inteira aberta num endereço, como quem chega por um link: as
 * rotas de verdade, a sessão de verdade, só o roteador em memória. Com um
 * token válido, a menos que o teste diga outro — ou nenhum. Devolve também
 * o cache das consultas, para o teste saber o que ficou na memória da aba.
 */
export function abrir(caminho: string, { token = tokenDeTeste() }: { readonly token?: string | null } = {}) {
  if (token !== null) {
    guardarToken(token);
  }

  const roteador = createMemoryRouter(criarRotas(), { initialEntries: [caminho] });
  const consultas = criarConsultas({ tentativas: 0 });
  const tela = render(<Aplicacao roteador={roteador} consultas={consultas} />);

  return { ...tela, roteador, consultas };
}
