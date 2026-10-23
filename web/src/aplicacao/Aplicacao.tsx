import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import type { createBrowserRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { ProvedorDaSessao } from '../sessao/ProvedorDaSessao';

export type Roteador = ReturnType<typeof createBrowserRouter>;

/** O cache das consultas, a sessão e as rotas — recebidos de fora, para os testes trocarem o roteador. */
export function Aplicacao({
  roteador,
  consultas,
}: {
  readonly roteador: Roteador;
  readonly consultas: QueryClient;
}) {
  return (
    <QueryClientProvider client={consultas}>
      <ProvedorDaSessao>
        <RouterProvider router={roteador} />
      </ProvedorDaSessao>
    </QueryClientProvider>
  );
}
