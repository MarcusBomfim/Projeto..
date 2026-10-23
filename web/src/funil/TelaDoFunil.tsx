import { useSearchParams } from 'react-router';
import { api } from '../api/cliente';
import { comoProblema } from '../api/problemas';
import { AvisoDeProblema } from '../comum/AvisoDeProblema';
import { Carregando } from '../comum/Carregando';
import { QuadroDoFunil } from './QuadroDoFunil';
import estilos from './TelaDoFunil.module.css';

/**
 * O funil como o comercial olha para ele: as vendas abertas em cada etapa.
 * Com mais de um funil, escolhe-se qual — e a escolha fica no endereço, para
 * o link levar ao mesmo quadro.
 */
export function TelaDoFunil() {
  const [parametros, definirParametros] = useSearchParams();
  const funis = api.useQuery('get', '/api/funis');

  return (
    <section aria-labelledby="titulo-do-funil" data-largura="inteira">
      <div className={estilos.topo}>
        <div>
          <h1 id="titulo-do-funil">Funil de vendas</h1>
          <p className="subtitulo">
            Arraste a venda para outra etapa, ou escolha a etapa no cartão. Ela só entra se tiver o que a
            etapa pede.
          </p>
        </div>

        {funis.isSuccess && funis.data.length > 1 && (
          <label className={estilos.escolha}>
            Funil
            <select
              value={parametros.get('funil') ?? funis.data[0]?.id}
              onChange={(evento) => {
                definirParametros({ funil: evento.target.value });
              }}
            >
              {funis.data.map((funil) => (
                <option key={funil.id} value={funil.id}>
                  {funil.nome}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      {funis.isPending && <Carregando rotulo="Carregando os funis" />}
      {funis.isError && <AvisoDeProblema problema={comoProblema(funis.error)} />}
      {funis.isSuccess && <Escolhido funis={funis.data} pedido={parametros.get('funil')} />}
    </section>
  );
}

function Escolhido({
  funis,
  pedido,
}: {
  readonly funis: readonly { readonly id: string; readonly nome: string }[];
  readonly pedido: string | null;
}) {
  const funil = funis.find((candidato) => candidato.id === pedido) ?? funis[0];

  if (!funil) {
    return (
      <p className={estilos.vazio}>
        Nenhum funil ainda. A gerência cria o funil da construtora — o padrão, do primeiro contato à
        negociação, ou um montado etapa por etapa.
      </p>
    );
  }

  return <QuadroDoFunil key={funil.id} funil={funil.id} />;
}
