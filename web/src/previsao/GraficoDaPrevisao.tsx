import type { Esquemas } from '../api/cliente';
import { formatarMes, formatarMoeda, formatarMoedaCompacta } from '../comum/formatacao';
import estilos from './GraficoDaPrevisao.module.css';
import { montarGrafico } from './geometriaDaPrevisao';

type Mes = Esquemas['MesDaPrevisaoResposta'];

const Medidas = { largura: 1080, altura: 300, esquerda: 80, abaixo: 30 };

/**
 * O previsto de cada mês, em barras, sobre o teto do que está em aberto —
 * e, ao lado, a tabela dos mesmos números, que é o que um leitor de tela lê
 * (ADR-0015).
 */
export function GraficoDaPrevisao({ meses }: { readonly meses: readonly Mes[] }) {
  const grafico = montarGrafico(meses, Medidas);

  return (
    <figure className={estilos.figura} aria-labelledby="legenda-da-previsao">
      <figcaption id="legenda-da-previsao" className={estilos.legenda}>
        Previsão de receita por mês
        <span className={estilos.chaves}>
          <span className={estilos.chaveGanho}>já ganho</span>
          <span className={estilos.chavePonderado}>em aberto, ponderado pela etapa</span>
          <span className={estilos.chaveTeto}>em aberto, se tudo fechar</span>
        </span>
      </figcaption>

      <svg
        viewBox={`0 0 ${Medidas.largura} ${Medidas.altura}`}
        className={estilos.svg}
        aria-hidden="true"
        focusable="false"
      >
        {grafico.marcas.map((marca) => (
          <g key={marca.valor}>
            <line
              x1={Medidas.esquerda}
              x2={Medidas.largura}
              y1={marca.y}
              y2={marca.y}
              className={estilos.grade}
            />
            <text x={Medidas.esquerda - 8} y={marca.y} className={estilos.valorDoEixo}>
              {formatarMoedaCompacta(marca.valor)}
            </text>
          </g>
        ))}

        {grafico.colunas.map((coluna) => (
          <g key={coluna.mes}>
            <rect
              x={coluna.x}
              y={coluna.teto.y}
              width={coluna.largura}
              height={coluna.teto.altura}
              className={estilos.teto}
            />
            <rect
              x={coluna.x}
              y={coluna.ganho.y}
              width={coluna.largura}
              height={coluna.ganho.altura}
              className={estilos.ganho}
            />
            <rect
              x={coluna.x}
              y={coluna.ponderado.y}
              width={coluna.largura}
              height={coluna.ponderado.altura}
              className={estilos.ponderado}
            />
            <text x={coluna.centro} y={Medidas.altura - 8} className={estilos.mes}>
              {formatarMes(coluna.mes)}
            </text>
          </g>
        ))}
      </svg>

      <table className={estilos.tabela}>
        <thead>
          <tr>
            <th scope="col">Mês</th>
            <th scope="col" className="numero">
              Já ganho
            </th>
            <th scope="col" className="numero">
              Em aberto
            </th>
            <th scope="col" className="numero">
              Ponderado
            </th>
            <th scope="col" className="numero">
              Previsto
            </th>
          </tr>
        </thead>
        <tbody>
          {meses.map((mes) => (
            <tr key={mes.mes}>
              <th scope="row">{formatarMes(mes.mes)}</th>
              <td className="numero">{formatarMoeda(mes.ganho)}</td>
              <td className="numero">{formatarMoeda(mes.emAberto)}</td>
              <td className="numero">{formatarMoeda(mes.ponderado)}</td>
              <td className="numero">
                <strong>{formatarMoeda(mes.previsto)}</strong>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
}
