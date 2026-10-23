import { QueryClient } from '@tanstack/react-query';

function status(erro: unknown): number | undefined {
  if (typeof erro === 'object' && erro !== null && 'status' in erro && typeof erro.status === 'number') {
    return erro.status;
  }

  return undefined;
}

/**
 * O cache das leituras da API. O que vem da API é estado do servidor, e
 * mora aqui — não num estado global da aplicação (ADR-0015).
 *
 * Tenta de novo o que pode ter sido passageiro — rede, erro do servidor —,
 * e não o que a API recusou com motivo: um 404 ou um 403 não mudam com
 * insistência.
 */
export function criarConsultas({ tentativas = 2 }: { readonly tentativas?: number } = {}): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 15_000,
        retry: (falhas, erro) => {
          const codigo = status(erro);

          return falhas < tentativas && (codigo === undefined || codigo >= 500);
        },
      },
    },
  });
}
