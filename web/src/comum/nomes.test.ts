import { describe, expect, test } from 'vitest';
import { iniciais, matiz } from './nomes';

describe('as iniciais de quem aparece na tela', () => {
  test('do nome, do login e da empresa, sem as palavras de ligação', () => {
    expect(iniciais('Carlos Souza')).toBe('CS');
    expect(iniciais('carlos.souza')).toBe('CS');
    expect(iniciais('Rota Sul Logística — Campinas')).toBe('RS');
    expect(iniciais('Maria das Dores')).toBe('MD');
    expect(iniciais('Ana')).toBe('A');
    expect(iniciais('—')).toBe('?');
  });

  test('o mesmo nome tem sempre a mesma cor, entre 0 e 359', () => {
    expect(matiz('Carlos Souza')).toBe(matiz('Carlos Souza'));
    expect(matiz('Carlos Souza')).not.toBe(matiz('Marina Costa'));
    expect(matiz('Rota Sul Logística — Campinas')).toBeGreaterThanOrEqual(0);
    expect(matiz('Rota Sul Logística — Campinas')).toBeLessThan(360);
  });
});
