/*
 * A geometria do gráfico da previsão, separada do desenho: dados os meses,
 * onde cada barra começa e até onde vai. Funções puras, testadas com números
 * (ADR-0015).
 *
 * Cada mês tem uma coluna com duas barras sobrepostas. A de trás, vazada, é
 * o teto: o já ganho mais tudo o que está em aberto para o mês, como se
 * todas as vendas fechassem. A da frente, cheia, é o previsto — o ganho
 * embaixo, e em cima o em aberto ponderado pela probabilidade da etapa.
 */

export interface MesDoGrafico {
  readonly mes: string;
  readonly ganho: number;
  readonly emAberto: number;
  readonly ponderado: number;
}

export interface Medidas {
  readonly largura: number;
  readonly altura: number;
  /** O espaço à esquerda, para os valores do eixo, e embaixo, para os meses. */
  readonly esquerda: number;
  readonly abaixo: number;
}

/** Um trecho vertical de barra, em coordenadas do SVG: o topo e a altura. */
export interface Trecho {
  readonly y: number;
  readonly altura: number;
}

export interface ColunaDoGrafico {
  readonly mes: string;
  /** O centro da coluna, onde fica o rótulo do mês. */
  readonly centro: number;
  readonly x: number;
  readonly largura: number;
  readonly teto: Trecho;
  readonly ganho: Trecho;
  readonly ponderado: Trecho;
}

export interface Grafico {
  /** As marcas do eixo dos valores, de zero ao topo. */
  readonly marcas: readonly { readonly valor: number; readonly y: number }[];
  readonly colunas: readonly ColunaDoGrafico[];
  /** Onde fica o zero: a linha de base das barras. */
  readonly base: number;
}

/** O passo "redondo" mais próximo, para cima: 1, 2, 2,5 ou 5 vezes uma potência de dez. */
export function passoRedondo(bruto: number): number {
  if (bruto <= 0) {
    return 1;
  }

  const potencia = 10 ** Math.floor(Math.log10(bruto));
  const fracao = bruto / potencia;
  const redondo = [1, 2, 2.5, 5, 10].find((candidato) => fracao <= candidato) ?? 10;

  return redondo * potencia;
}

/** As marcas do eixo: de zero até cobrir o maior valor, em quatro passos redondos, mais ou menos. */
export function marcasDoEixo(maior: number): number[] {
  const passo = passoRedondo(maior / 4);
  const quantas = Math.max(Math.ceil(maior / passo), 1);

  return Array.from({ length: quantas + 1 }, (_, indice) => indice * passo);
}

export function montarGrafico(meses: readonly MesDoGrafico[], medidas: Medidas): Grafico {
  const base = medidas.altura - medidas.abaixo;
  const marcas = marcasDoEixo(Math.max(...meses.map((mes) => mes.ganho + mes.emAberto), 0));
  const topo = marcas.at(-1) ?? 1;
  const escala = (valor: number) => (valor / topo) * base;
  const trecho = (de: number, ate: number): Trecho => ({
    y: base - escala(ate),
    altura: escala(ate) - escala(de),
  });

  const espaco = (medidas.largura - medidas.esquerda) / Math.max(meses.length, 1);
  const largura = espaco * 0.55;

  return {
    base,
    marcas: marcas.map((valor) => ({ valor, y: base - escala(valor) })),
    colunas: meses.map((mes, indice) => {
      const centro = medidas.esquerda + espaco * (indice + 0.5);

      return {
        mes: mes.mes,
        centro,
        x: centro - largura / 2,
        largura,
        teto: trecho(0, mes.ganho + mes.emAberto),
        ganho: trecho(0, mes.ganho),
        ponderado: trecho(mes.ganho, mes.ganho + mes.ponderado),
      };
    }),
  };
}
