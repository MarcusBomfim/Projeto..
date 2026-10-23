import { expect, type APIRequestContext } from '@playwright/test';
import { randomUUID } from 'node:crypto';

/*
 * A API usada como um cliente usaria: criar com chave de idempotência,
 * alterar com a versão lida (ADR-0013, ADR-0014). É assim que a carteira da
 * Rota Sul chega ao banco antes de o navegador abrir.
 */

/** Cria e devolve o corpo da resposta — e o id, tirado do Location. */
export async function criar<T = Record<string, unknown>>(
  api: APIRequestContext,
  caminho: string,
  corpo?: unknown,
): Promise<T & { readonly id: string }> {
  const resposta = await api.post(caminho, {
    ...(corpo === undefined ? {} : { data: corpo }),
    headers: { 'Idempotency-Key': randomUUID() },
  });

  expect(resposta.status(), `POST ${caminho}: ${await resposta.text()}`).toBe(201);

  return (await resposta.json()) as T & { readonly id: string };
}

/** Altera sobre a versão lida agora — lê, e manda o pedido com o If-Match dela. */
export async function alterar<T = Record<string, unknown>>(
  api: APIRequestContext,
  metodo: 'PUT' | 'PATCH' | 'POST',
  recurso: string,
  caminho: string,
  corpo?: unknown,
): Promise<T> {
  const lido = await api.get(recurso);
  expect(lido.status(), `GET ${recurso}: ${await lido.text()}`).toBe(200);

  const resposta = await api.fetch(`${recurso}${caminho}`, {
    method: metodo,
    ...(corpo === undefined ? {} : { data: corpo }),
    headers: { 'If-Match': lido.headers().etag ?? '' },
  });

  expect(resposta.status(), `${metodo} ${recurso}${caminho}: ${await resposta.text()}`).toBe(200);

  return (await resposta.json()) as T;
}

/** "2026-12-15": o dia, tantos dias a partir de hoje. */
export function diaDaqui(dias: number): string {
  const dia = new Date();
  dia.setDate(dia.getDate() + dias);

  return [dia.getFullYear(), dia.getMonth() + 1, dia.getDate()]
    .map((parte) => String(parte).padStart(2, '0'))
    .join('-');
}

/** O instante, tantos dias e horas a partir de agora, como a API o lê. */
export function instanteDaqui(dias: number, horas = 0): string {
  return new Date(Date.now() + (dias * 24 + horas) * 60 * 60 * 1000).toISOString();
}
