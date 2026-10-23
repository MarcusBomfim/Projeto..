import { useId } from 'react';
import { Link } from 'react-router';
import type { Esquemas } from '../api/cliente';
import { diasDesde, formatarData, formatarDias, formatarMoeda } from '../comum/formatacao';
import { Selo } from '../comum/Selo';
import estilos from './CartaoDaVenda.module.css';

type Cartao = Esquemas['CartaoResposta'];

export interface DestinoDoCartao {
  readonly etapa: string;
  readonly nome: string;
}

/**
 * Uma venda no quadro. Arrasta-se para outra coluna — ou, para quem usa o
 * teclado ou um leitor de tela, escolhe-se a etapa no seletor do próprio
 * cartão: os dois caminhos fazem o mesmo pedido.
 */
export function CartaoDaVenda({
  cartao,
  destinos,
  movendo,
  aoMover,
  aoArrastar,
}: {
  readonly cartao: Cartao;
  /** As etapas para onde a venda pode ir daqui. */
  readonly destinos: readonly DestinoDoCartao[];
  readonly movendo: boolean;
  readonly aoMover: (etapa: string) => void;
  readonly aoArrastar: (arrastando: boolean) => void;
}) {
  const seletor = useId();

  return (
    <article
      className={`${estilos.cartao ?? ''} ${cartao.parada ? (estilos.parada ?? '') : ''}`}
      aria-labelledby={`${seletor}-titulo`}
      aria-busy={movendo}
      draggable={!movendo}
      onDragStart={(evento) => {
        evento.dataTransfer.setData('text/plain', cartao.id);
        evento.dataTransfer.effectAllowed = 'move';
        aoArrastar(true);
      }}
      onDragEnd={() => {
        aoArrastar(false);
      }}
    >
      <header className={estilos.cabecalho}>
        <h3 id={`${seletor}-titulo`}>{cartao.titulo}</h3>
        {cartao.parada && <Selo tom="atencao">Parada</Selo>}
      </header>

      <Link to={`/contas/${cartao.conta}`} className={estilos.conta}>
        {cartao.nomeDaConta}
      </Link>

      <dl className={estilos.dados}>
        <div>
          <dt>Valor</dt>
          <dd>{cartao.valorEstimado === null ? 'sem valor' : formatarMoeda(cartao.valorEstimado)}</dd>
        </div>
        <div>
          <dt>Fecha em</dt>
          <dd>{formatarData(cartao.previsaoDeFechamento)}</dd>
        </div>
        <div>
          <dt>Na etapa</dt>
          <dd>{formatarDias(diasDesde(cartao.entrouNaEtapaEm))}</dd>
        </div>
        <div>
          <dt>Com</dt>
          <dd>{cartao.responsavel}</dd>
        </div>
      </dl>

      <label htmlFor={seletor} className="somente-leitor">
        Mover {cartao.titulo} para
      </label>
      <select
        id={seletor}
        className={estilos.mover}
        value=""
        disabled={movendo || destinos.length === 0}
        onChange={(evento) => {
          if (evento.target.value) {
            aoMover(evento.target.value);
          }
        }}
      >
        <option value="">{movendo ? 'Movendo…' : 'Mover para…'}</option>
        {destinos.map((destino) => (
          <option key={destino.etapa} value={destino.etapa}>
            {destino.nome}
          </option>
        ))}
      </select>
    </article>
  );
}
