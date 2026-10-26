import { expect, request, test } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { criar } from '../apoio/api';
import { emitirToken } from '../apoio/identidade';
import { Web } from '../apoio/pilha';

/*
 * O que a pilha do Compose promete e as telas não mostram (ADR-0019): o
 * nginx manda os cabeçalhos de segurança, e a API passa por ele como veio —
 * a ETag forte, que o If-Match confere, sem o gzip a enfraquecer.
 */

test('o nginx manda os cabeçalhos de segurança', async () => {
  const navegador = await request.newContext({ baseURL: Web });

  const resposta = await navegador.get('/');

  expect(resposta.status()).toBe(200);
  expect(resposta.headers()['content-security-policy']).toContain("default-src 'self'");
  expect(resposta.headers()['x-content-type-options']).toBe('nosniff');
  expect(resposta.headers()['referrer-policy']).toBe('no-referrer');
});

test('a ETag da API passa forte pelo nginx, mesmo para quem aceita gzip', async () => {
  const construtora = randomUUID();
  const gerente = await request.newContext({
    baseURL: Web,
    extraHTTPHeaders: {
      Authorization: `Bearer ${emitirToken({ construtora, nome: 'Helena Martins', login: 'helena.martins', papeis: ['gerente'] })}`,
      'Accept-Encoding': 'gzip',
    },
  });
  const funil = await criar(gerente, '/api/funis/padrao');

  const lido = await gerente.get(`/api/funis/${funil.id}`);

  expect(lido.status()).toBe(200);
  expect(lido.headers()['etag']).toBe('"1"');
});
