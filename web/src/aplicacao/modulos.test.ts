import { describe, expect, test } from 'vitest';
import { entradasDoMenu, modulos } from './modulos';

describe('as áreas da aplicação', () => {
  test('cada uma tem rota, e nenhum caminho se repete', () => {
    const caminhos = modulos.flatMap((modulo) =>
      modulo.rotas.map((rota) => (rota.index ? '(início)' : rota.path)),
    );

    expect(modulos.every((modulo) => modulo.rotas.length > 0)).toBe(true);
    expect(caminhos).toContain('(início)');
    expect(new Set(caminhos).size).toBe(caminhos.length);
  });

  test('o menu sai na ordem pedida por cada área, e quem não pede entrada fica fora dele', () => {
    const menu = entradasDoMenu([
      { rotas: [{ path: 'b' }], menu: { rotulo: 'B', caminho: '/b', ordem: 2 } },
      { rotas: [{ path: 'escondida' }] },
      { rotas: [{ index: true }], menu: { rotulo: 'A', caminho: '/', ordem: 1 } },
    ]);

    expect(menu.map((entrada) => entrada.rotulo)).toEqual(['A', 'B']);
  });
});
