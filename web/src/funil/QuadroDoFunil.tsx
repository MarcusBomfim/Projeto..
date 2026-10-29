import { useState, type CSSProperties } from 'react';
import { api, type Esquemas } from '../api/cliente';
import { comoProblema } from '../api/problemas';
import { AvisoDeProblema } from '../comum/AvisoDeProblema';
import { Carregando } from '../comum/Carregando';
import { formatarMoeda, formatarMoedaCompacta, formatarProbabilidade } from '../comum/formatacao';
import { CartaoDaVenda, type DestinoDoCartao } from './CartaoDaVenda';
import estilos from './QuadroDoFunil.module.css';
import { useMoverVenda } from './useMoverVenda';

type Quadro = Esquemas['QuadroResposta'];
type Coluna = Esquemas['ColunaResposta'];
type Cartao = Esquemas['CartaoResposta'];

/** A venda sendo arrastada, e de que coluna ela saiu. */
interface Arrastada {
  readonly cartao: Cartao;
  readonly de: string;
}

/** As vendas abertas do funil, uma coluna por etapa (ADR-0004). */
export function QuadroDoFunil({ funil }: { readonly funil: string }) {
  const quadro = api.useQuery('get', '/api/funis/{id}/quadro', { params: { path: { id: funil } } });

  if (quadro.isPending) {
    return <Carregando rotulo="Carregando o quadro" />;
  }

  if (quadro.isError) {
    return <AvisoDeProblema problema={comoProblema(quadro.error)} />;
  }

  return <Colunas quadro={quadro.data} />;
}

function Colunas({ quadro }: { readonly quadro: Quadro }) {
  const { mover, movendo, movida, problema } = useMoverVenda();
  const [arrastada, definirArrastada] = useState<Arrastada | null>(null);
  const [alvo, definirAlvo] = useState<string | null>(null);

  // Venda vai para etapa em uso; a arquivada só aparece enquanto tem venda.
  const abertas = quadro.colunas.filter((coluna) => !coluna.arquivada);
  const vendas = quadro.colunas.reduce((soma, coluna) => soma + coluna.oportunidades.length, 0);
  const paradas = quadro.colunas.flatMap((coluna) => coluna.oportunidades).filter((cartao) => cartao.parada);
  const destinoDaMovida = quadro.colunas.find((coluna) => coluna.etapa === movida?.etapa);
  const comValor = vendas - quadro.colunas.reduce((soma, coluna) => soma + coluna.semValor, 0);

  const destinos = (de: string): DestinoDoCartao[] =>
    abertas
      .filter((coluna) => coluna.etapa !== de)
      .map((coluna) => ({ etapa: coluna.etapa, nome: coluna.nome }));

  const recebe = (coluna: Coluna) => arrastada !== null && !coluna.arquivada && arrastada.de !== coluna.etapa;

  return (
    <>
      {/* Os números grandes, para bater o olho; o leitor de tela lê a frase logo abaixo, que diz o mesmo. */}
      <div className={estilos.indicadores} aria-hidden="true">
        <Indicador
          nome="Em aberto"
          valor={formatarMoeda(quadro.total)}
          nota={vendas === 1 ? '1 venda no funil' : `${vendas} vendas no funil`}
          classe={estilos.principal}
        />
        <Indicador
          nome="Ponderado"
          valor={formatarMoeda(quadro.ponderado)}
          nota="pela probabilidade de cada etapa"
        />
        <Indicador
          nome="Valor médio"
          valor={comValor === 0 ? '—' : formatarMoeda(quadro.total / comValor)}
          nota="das vendas com valor estimado"
        />
        <Indicador
          nome="Paradas"
          valor={String(paradas.length)}
          nota="há mais tempo do que a etapa espera"
          classe={paradas.length > 0 ? estilos.alerta : undefined}
        />
      </div>

      <p className="somente-leitor">
        {vendas === 1 ? '1 venda aberta' : `${vendas} vendas abertas`}, {formatarMoeda(quadro.total)} em
        aberto, {formatarMoeda(quadro.ponderado)} ponderado pela probabilidade de cada etapa
        {paradas.length > 0 && (
          <>
            {' '}
            — <strong>{paradas.length === 1 ? '1 parada' : `${paradas.length} paradas`}</strong> há mais tempo
            do que a etapa espera
          </>
        )}
        .
      </p>

      {problema && (
        <AvisoDeProblema
          problema={{ ...problema, titulo: `A venda não mudou de etapa. ${problema.titulo}` }}
        />
      )}
      <p className="somente-leitor" aria-live="polite">
        {movida && destinoDaMovida ? `${movida.titulo} foi para ${destinoDaMovida.nome}.` : ''}
      </p>

      <div className={estilos.quadro}>
        {quadro.colunas.map((coluna) => (
          <section
            key={coluna.etapa}
            aria-labelledby={`coluna-${coluna.etapa}`}
            className={`${estilos.coluna ?? ''} ${alvo === coluna.etapa ? (estilos.alvo ?? '') : ''}`}
            onDragOver={(evento) => {
              if (recebe(coluna)) {
                evento.preventDefault();
                evento.dataTransfer.dropEffect = 'move';
                definirAlvo(coluna.etapa);
              }
            }}
            onDragLeave={() => {
              definirAlvo(null);
            }}
            onDrop={(evento) => {
              evento.preventDefault();
              definirAlvo(null);

              if (arrastada && recebe(coluna)) {
                mover(arrastada.cartao, coluna.etapa);
              }

              definirArrastada(null);
            }}
          >
            <header className={estilos.cabecalho}>
              <div className={estilos.titulo}>
                <h2 id={`coluna-${coluna.etapa}`}>
                  {coluna.arquivada ? `${coluna.nome} (arquivada)` : coluna.nome}
                </h2>
                <span className={estilos.contagem}>
                  {coluna.oportunidades.length}
                  <span className="somente-leitor">
                    {coluna.oportunidades.length === 1 ? ' venda' : ' vendas'}
                  </span>
                </span>
              </div>
              <p className={estilos.numeros}>
                <span className={estilos.probabilidade}>{formatarProbabilidade(coluna.probabilidade)}</span>
                <span>{formatarMoedaCompacta(coluna.total)}</span>
              </p>
              <span
                className={estilos.barra}
                style={{ '--probabilidade': `${String(coluna.probabilidade)}%` } as CSSProperties}
                aria-hidden="true"
              />
              <p className={estilos.ponderado}>
                Ponderado: {formatarMoeda(coluna.ponderado)}
                {coluna.semValor > 0 && ` · ${coluna.semValor} sem valor`}
              </p>
            </header>

            {coluna.oportunidades.length === 0 ? (
              <p className={estilos.vazia}>Nenhuma venda aqui.</p>
            ) : (
              <ul className={estilos.cartoes}>
                {coluna.oportunidades.map((cartao) => (
                  <li key={cartao.id}>
                    <CartaoDaVenda
                      cartao={cartao}
                      destinos={destinos(coluna.etapa)}
                      movendo={movendo === cartao.id}
                      aoMover={(etapa) => {
                        mover(cartao, etapa);
                      }}
                      aoArrastar={(arrastando) => {
                        definirArrastada(arrastando ? { cartao, de: coluna.etapa } : null);
                      }}
                    />
                  </li>
                ))}
              </ul>
            )}
          </section>
        ))}
      </div>
    </>
  );
}

function Indicador({
  nome,
  valor,
  nota,
  classe,
}: {
  readonly nome: string;
  readonly valor: string;
  readonly nota: string;
  readonly classe?: string | undefined;
}) {
  return (
    <div className={`${estilos.indicador ?? ''} ${classe ?? ''}`}>
      <span className={estilos.nome}>{nome}</span>
      <strong className={estilos.valor}>{valor}</strong>
      <span className={estilos.nota}>{nota}</span>
    </div>
  );
}
