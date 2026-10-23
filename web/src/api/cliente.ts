import createFetchClient from 'openapi-fetch';
import createClient from 'openapi-react-query';
import { lerToken } from '../sessao/token';
import type { components, paths } from './esquema';

/** As representações da API, como o contrato as descreve. */
export type Esquemas = components['schemas'];

let aoRecusarToken: (() => void) | undefined;

/**
 * Avisa quando a API recusar o token — vencido, revogado, de outra chave —,
 * para a sessão pedir outro. Devolve como deixar de avisar.
 */
export function avisarQuandoRecusarToken(acao: () => void): () => void {
  aoRecusarToken = acao;

  return () => {
    if (aoRecusarToken === acao) {
      aoRecusarToken = undefined;
    }
  };
}

/**
 * O cliente HTTP da API, tipado pelo contrato: o caminho, os parâmetros e a
 * resposta de cada chamada vêm de docs/api/openapi.json, pelos tipos gerados
 * em esquema.d.ts (ADR-0015). Mesma origem: em desenvolvimento, o Vite
 * repassa /api para a API.
 */
export const http = createFetchClient<paths>({
  baseUrl: globalThis.location.origin,
  // O fetch da hora de cada chamada, e não o do momento em que este módulo
  // carregou: quem o troca depois — os testes, pela API de mentira — vale.
  fetch: (pedido) => globalThis.fetch(pedido),
});

http.use({
  onRequest({ request }) {
    const token = lerToken();

    if (token) {
      request.headers.set('Authorization', `Bearer ${token}`);
    }

    return request;
  },
  onResponse({ response }) {
    if (response.status === 401) {
      aoRecusarToken?.();
    }

    return response;
  },
});

/** As consultas da API no TanStack Query, com a chave de cache tirada do caminho e dos parâmetros. */
export const api = createClient(http);
