import { describe, expect, test } from 'vitest';
import { marcasDoEixo, montarGrafico, passoRedondo } from './geometriaDaPrevisao';

const Medidas = { largura: 640, altura: 300, esquerda: 40, abaixo: 20 };

describe('a geometria do gráfico da previsão', () => {
  test('o passo do eixo é redondo: 1, 2, 2,5 ou 5 vezes uma potência de dez', () => {
    expect([150_000, 180_000, 230_000, 240_000, 400_000, 900_000].map(passoRedondo)).toEqual([
      200_000, 200_000, 250_000, 250_000, 500_000, 1_000_000,
    ]);
    expect(passoRedondo(0)).toBe(1);
  });

  test('as marcas vão de zero até cobrir o maior valor', () => {
    expect(marcasDoEixo(600_000)).toEqual([0, 200_000, 400_000, 600_000]);
    expect(marcasDoEixo(650_000)).toEqual([0, 200_000, 400_000, 600_000, 800_000]);
    expect(marcasDoEixo(0)).toEqual([0, 1]);
  });

  test('os meses do README: o teto é o em aberto, e o previsto é o ponderado sobre o ganho', () => {
    const grafico = montarGrafico(
      [
        { mes: '2027-01', ganho: 0, emAberto: 600_000, ponderado: 360_000 },
        { mes: '2027-02', ganho: 0, emAberto: 150_000, ponderado: 15_000 },
        { mes: '2027-03', ganho: 0, emAberto: 0, ponderado: 0 },
      ],
      Medidas,
    );

    // Base em 280; topo do eixo em 600 mil: cada 200 mil, 280/3 de altura.
    expect(grafico.base).toBe(280);
    expect(grafico.marcas.map((marca) => [marca.valor, Math.round(marca.y)])).toEqual([
      [0, 280],
      [200_000, 187],
      [400_000, 93],
      [600_000, 0],
    ]);

    const [janeiro, fevereiro, marco] = grafico.colunas;
    expect([janeiro?.centro, fevereiro?.centro, marco?.centro]).toEqual([140, 340, 540]);
    expect(janeiro?.largura).toBeCloseTo(110);
    expect(janeiro?.teto).toEqual({ y: 0, altura: 280 });
    expect(janeiro?.ganho).toEqual({ y: 280, altura: 0 });
    expect(janeiro?.ponderado.altura).toBeCloseTo(168);
    expect(janeiro?.ponderado.y).toBeCloseTo(112);
    expect(fevereiro?.teto.altura).toBeCloseTo(70);
    expect(marco?.teto).toEqual({ y: 280, altura: 0 });
  });

  test('o ponderado fica em cima do já ganho, e o teto soma os dois lados', () => {
    const [coluna] = montarGrafico(
      [{ mes: '2026-12', ganho: 200_000, emAberto: 200_000, ponderado: 100_000 }],
      {
        ...Medidas,
        altura: 420,
      },
    ).colunas;

    // Topo do eixo em 400 mil, base em 400: um real é um milésimo de pixel.
    expect(coluna?.teto).toEqual({ y: 0, altura: 400 });
    expect(coluna?.ganho).toEqual({ y: 200, altura: 200 });
    expect(coluna?.ponderado).toEqual({ y: 100, altura: 100 });
  });
});
