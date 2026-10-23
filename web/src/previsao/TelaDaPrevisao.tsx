import { useSearchParams } from 'react-router';
import { api, type Esquemas } from '../api/cliente';
import { comoProblema } from '../api/problemas';
import { AvisoDeProblema } from '../comum/AvisoDeProblema';
import { Carregando } from '../comum/Carregando';
import { formatarData, formatarMes, formatarMoeda } from '../comum/formatacao';
import { GraficoDaPrevisao } from './GraficoDaPrevisao';
import estilos from './TelaDaPrevisao.module.css';

type Previsao = Esquemas['PrevisaoResposta'];
type Venda = Esquemas['VendaNoRelatorioResposta'];

/** Quantos meses se pode pedir de uma vez. A API aceita até 24; a tela oferece os de costume. */
const OpcoesDeMeses = [3, 6, 12] as const;

/**
 * A receita prevista mês a mês, ponderada pela etapa de cada venda — e, à
 * parte, as vendas que a previsão não conta, para quem cuida delas corrigir
 * (ADR-0008). O mês e o período ficam no endereço: o link leva à mesma
 * previsão.
 */
export function TelaDaPrevisao() {
  const [parametros, definirParametros] = useSearchParams();
  const primeiro = parametros.get('primeiro');
  const meses = Number(parametros.get('meses') ?? OpcoesDeMeses[0]);
  const previsao = api.useQuery('get', '/api/relatorios/previsao', {
    params: { query: { ...(primeiro ? { primeiro } : {}), meses } },
  });

  function escolher(mudanca: { readonly primeiro?: string; readonly meses?: number }) {
    const proximos = new URLSearchParams(parametros);

    if (mudanca.primeiro !== undefined) {
      proximos.set('primeiro', mudanca.primeiro);
    }

    if (mudanca.meses !== undefined) {
      proximos.set('meses', String(mudanca.meses));
    }

    definirParametros(proximos);
  }

  return (
    <section aria-labelledby="titulo-da-previsao">
      <h1 id="titulo-da-previsao">Previsão de receita</h1>
      <p className="subtitulo">
        A venda aberta conta no mês em que deve fechar, pelo valor estimado vezes a probabilidade da etapa; a
        ganha, no mês em que foi ganha. A que não tem valor, nem data, ou já devia ter fechado fica à parte.
      </p>

      <div className={estilos.escolhas}>
        <label>
          A partir de
          <input
            type="month"
            value={primeiro ?? previsao.data?.meses[0]?.mes ?? ''}
            onChange={(evento) => {
              if (evento.target.value) {
                escolher({ primeiro: evento.target.value });
              }
            }}
          />
        </label>
        <label>
          Meses
          <select
            value={meses}
            onChange={(evento) => {
              escolher({ meses: Number(evento.target.value) });
            }}
          >
            {OpcoesDeMeses.map((opcao) => (
              <option key={opcao} value={opcao}>
                {opcao}
              </option>
            ))}
          </select>
        </label>
      </div>

      {previsao.isPending && <Carregando rotulo="Calculando a previsão" />}
      {previsao.isError && <AvisoDeProblema problema={comoProblema(previsao.error)} />}
      {previsao.isSuccess && <Resultado previsao={previsao.data} />}
    </section>
  );
}

function Resultado({ previsao }: { readonly previsao: Previsao }) {
  const comAbertas = previsao.meses.filter((mes) => mes.abertas.length > 0);
  const [primeiro] = previsao.meses;
  const ultimo = previsao.meses.at(-1);

  return (
    <>
      <p className={estilos.resumo}>
        <strong>{formatarMoeda(previsao.previsto)}</strong> previstos
        {primeiro && ultimo && ` de ${formatarMes(primeiro.mes)} a ${formatarMes(ultimo.mes)}`}:{' '}
        {formatarMoeda(previsao.ganho)} já ganhos e {formatarMoeda(previsao.ponderado)} em aberto, ponderados
        pela etapa de cada venda.
      </p>

      <GraficoDaPrevisao meses={previsao.meses} />

      <div className={estilos.blocos}>
        <section aria-labelledby="titulo-abertas" className={estilos.bloco}>
          <h2 id="titulo-abertas">As vendas de cada mês</h2>
          {comAbertas.length === 0 ? (
            <p className={estilos.vazio}>Nenhuma venda aberta deve fechar nestes meses.</p>
          ) : (
            comAbertas.map((mes) => (
              <div key={mes.mes}>
                <h3>{formatarMes(mes.mes)}</h3>
                <Vendas vendas={mes.abertas} />
              </div>
            ))
          )}
        </section>

        <section aria-labelledby="titulo-fora" className={estilos.bloco}>
          <h2 id="titulo-fora">Fora da previsão</h2>
          <h3>A previsão de fechamento já passou</h3>
          <Vendas vendas={previsao.vencidas} />
          <h3>Sem valor estimado</h3>
          <Vendas vendas={previsao.semValor} />
          <h3>Com valor, sem previsão de fechamento</h3>
          <Vendas vendas={previsao.semPrevisao} />
        </section>
      </div>
    </>
  );
}

function Vendas({ vendas }: { readonly vendas: readonly Venda[] }) {
  if (vendas.length === 0) {
    return <p className={estilos.vazio}>Nenhuma.</p>;
  }

  return (
    <ul className={estilos.vendas}>
      {vendas.map((venda) => (
        <li key={venda.id}>
          <strong>{venda.titulo}</strong>
          <span>
            {[
              venda.responsavel,
              venda.valorEstimado === null ? 'sem valor' : formatarMoeda(venda.valorEstimado),
              venda.previsaoDeFechamento && `previsão para ${formatarData(venda.previsaoDeFechamento)}`,
            ]
              .filter(Boolean)
              .join(' · ')}
          </span>
        </li>
      ))}
    </ul>
  );
}
