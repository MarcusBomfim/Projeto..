import { describe, expect, test } from 'vitest';
import { criarConsultas } from './consultas';

/** O que a consulta lança quando a API recusa: o corpo do ProblemDetails, e não um Error. */
const problema = (status: number) => ({ title: 'Problema da API', status }) as unknown as Error;

describe('o cache das consultas', () => {
  test('tenta de novo o que pode ter sido passageiro, e não o que a API recusou com motivo', () => {
    const tentarDeNovo = criarConsultas({ tentativas: 2 }).getDefaultOptions().queries?.retry;

    if (typeof tentarDeNovo !== 'function') {
      throw new Error('A política de nova tentativa deveria ser uma função.');
    }

    expect(tentarDeNovo(0, new TypeError('Failed to fetch'))).toBe(true);
    expect(tentarDeNovo(0, problema(503))).toBe(true);
    expect(tentarDeNovo(2, problema(503))).toBe(false);
    expect(tentarDeNovo(0, problema(404))).toBe(false);
    expect(tentarDeNovo(0, problema(403))).toBe(false);
  });
});
