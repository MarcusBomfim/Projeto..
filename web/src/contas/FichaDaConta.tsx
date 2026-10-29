import { useQueries } from '@tanstack/react-query';
import { Link, useParams } from 'react-router';
import { api, type Esquemas } from '../api/cliente';
import { comoProblema } from '../api/problemas';
import { AvisoDeProblema } from '../comum/AvisoDeProblema';
import { Carregando } from '../comum/Carregando';
import { formatarDataEHora, formatarMoeda } from '../comum/formatacao';
import { Iniciais } from '../comum/Iniciais';
import { PapeisNaCompra, SituacoesDaAtividade, SituacoesDaVenda, TiposDeAtividade } from '../comum/rotulos';
import { Selo, type TomDoSelo } from '../comum/Selo';
import estilos from './FichaDaConta.module.css';

type Conta = Esquemas['ContaResposta'];
type Venda = Esquemas['ResumoDaOportunidadeResposta'];
type Atividade = Esquemas['ResumoDaAtividadeResposta'];

const TomDaVenda: Readonly<Record<Venda['situacao'], TomDoSelo>> = {
  Aberta: 'destaque',
  Ganha: 'sucesso',
  Perdida: 'perigo',
};

/**
 * A ficha do cliente, para quem vai atendê-lo: com quem se fala, o que está
 * sendo vendido, o que está marcado — e a história, do mais recente para o
 * mais antigo (ADR-0006).
 */
export function FichaDaConta() {
  const { id = '' } = useParams();
  const conta = api.useQuery('get', '/api/contas/{id}', { params: { path: { id } } });

  if (conta.isPending) {
    return <Carregando rotulo="Carregando o cliente" />;
  }

  if (conta.isError) {
    return (
      <section>
        <AvisoDeProblema problema={comoProblema(conta.error)} />
        <p>
          <Link to="/contas">Voltar aos clientes</Link>
        </p>
      </section>
    );
  }

  return <Ficha conta={conta.data} />;
}

function Ficha({ conta }: { readonly conta: Conta }) {
  return (
    <article aria-labelledby="nome-da-conta">
      <p className={estilos.voltar}>
        <Link to="/contas">Clientes</Link>
      </p>
      <header className={estilos.cabecalho}>
        <Iniciais nome={conta.nome} tamanho="grande" />
        <div>
          <h1 id="nome-da-conta">{conta.nome}</h1>
          <p className={estilos.documento}>{conta.documento ?? 'Sem CNPJ ou CPF informado'}</p>
        </div>
      </header>

      <div className={estilos.colunas}>
        <div className={estilos.principal}>
          <Contatos conta={conta} />
          <Vendas conta={conta.id} />
          <Atividades conta={conta.id} />
        </div>
        <LinhaDoTempo conta={conta.id} />
      </div>
    </article>
  );
}

function Contatos({ conta }: { readonly conta: Conta }) {
  return (
    <section aria-labelledby="titulo-contatos" className={estilos.bloco}>
      <h2 id="titulo-contatos">Contatos</h2>
      {conta.contatos.length === 0 ? (
        <p className={estilos.vazio}>Ninguém cadastrado ainda.</p>
      ) : (
        <ul className={estilos.contatos}>
          {conta.contatos.map((contato) => (
            <li key={contato.id}>
              <Iniciais nome={contato.nome} />
              <p className={estilos.nome}>
                <strong>{contato.nome}</strong>{' '}
                {contato.id === conta.principal && <Selo tom="destaque">Principal</Selo>}
              </p>
              <p className={estilos.detalhe}>
                {[contato.cargo, PapeisNaCompra[contato.papel]].filter((parte) => parte !== null).join(' · ')}
              </p>
              <p className={estilos.meios}>
                {contato.email && <a href={`mailto:${contato.email}`}>{contato.email}</a>}
                {contato.telefone && (
                  <a href={`tel:${contato.telefone.replaceAll(/\D/g, '')}`}>{contato.telefone}</a>
                )}
              </p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function Vendas({ conta }: { readonly conta: string }) {
  const vendas = api.useQuery('get', '/api/oportunidades', { params: { query: { conta } } });
  const funis = [...new Set(vendas.data?.map((venda) => venda.funil))];
  const etapas = new Map(
    useQueries({
      queries: funis.map((funil) =>
        api.queryOptions('get', '/api/funis/{id}', { params: { path: { id: funil } } }),
      ),
    })
      .flatMap((funil) => [...(funil.data?.etapas ?? []), ...(funil.data?.arquivadas ?? [])])
      .map((etapa) => [etapa.id, etapa.nome]),
  );

  return (
    <section aria-labelledby="titulo-vendas" className={estilos.bloco}>
      <h2 id="titulo-vendas">Vendas</h2>
      {vendas.isPending && <Carregando rotulo="Carregando as vendas" />}
      {vendas.isError && <AvisoDeProblema problema={comoProblema(vendas.error)} />}
      {vendas.isSuccess &&
        (vendas.data.length === 0 ? (
          <p className={estilos.vazio}>Nenhuma venda com este cliente ainda.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th scope="col">Venda</th>
                <th scope="col">Etapa</th>
                <th scope="col">Situação</th>
                <th scope="col" className="numero">
                  Valor estimado
                </th>
              </tr>
            </thead>
            <tbody>
              {vendas.data.map((venda) => (
                <tr key={venda.id}>
                  <th scope="row">
                    <Link to={`/?funil=${venda.funil}`}>{venda.titulo}</Link>
                  </th>
                  <td>{etapas.get(venda.etapa) ?? '…'}</td>
                  <td>
                    <Selo tom={TomDaVenda[venda.situacao]}>{SituacoesDaVenda[venda.situacao]}</Selo>
                  </td>
                  <td className="numero">{formatarMoeda(venda.valorEstimado)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ))}
    </section>
  );
}

function Atividades({ conta }: { readonly conta: string }) {
  const atividades = api.useQuery('get', '/api/atividades', { params: { query: { conta } } });
  const agora = new Date();
  const atrasada = (atividade: Atividade) =>
    atividade.situacao === 'Pendente' && new Date(atividade.previstaPara) < agora;

  return (
    <section aria-labelledby="titulo-atividades" className={estilos.bloco}>
      <h2 id="titulo-atividades">Atividades</h2>
      {atividades.isPending && <Carregando rotulo="Carregando as atividades" />}
      {atividades.isError && <AvisoDeProblema problema={comoProblema(atividades.error)} />}
      {atividades.isSuccess &&
        (atividades.data.length === 0 ? (
          <p className={estilos.vazio}>Nada marcado com este cliente.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th scope="col">Quando</th>
                <th scope="col">O quê</th>
                <th scope="col">Quem</th>
                <th scope="col">Situação</th>
              </tr>
            </thead>
            <tbody>
              {atividades.data.map((atividade) => (
                <tr key={atividade.id}>
                  <td>{formatarDataEHora(atividade.previstaPara)}</td>
                  <th scope="row">
                    {TiposDeAtividade[atividade.tipo]}: {atividade.assunto}
                  </th>
                  <td>{atividade.responsavel}</td>
                  <td>
                    {atrasada(atividade) ? (
                      <Selo tom="perigo">Atrasada</Selo>
                    ) : (
                      <Selo tom={atividade.situacao === 'Pendente' ? 'neutro' : 'sucesso'}>
                        {SituacoesDaAtividade[atividade.situacao]}
                      </Selo>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ))}
    </section>
  );
}

function LinhaDoTempo({ conta }: { readonly conta: string }) {
  const linha = api.useQuery('get', '/api/contas/{id}/linha-do-tempo', { params: { path: { id: conta } } });

  return (
    <section aria-labelledby="titulo-linha" className={`${estilos.bloco ?? ''} ${estilos.linha ?? ''}`}>
      <h2 id="titulo-linha">Linha do tempo</h2>
      {linha.isPending && <Carregando rotulo="Carregando a linha do tempo" />}
      {linha.isError && <AvisoDeProblema problema={comoProblema(linha.error)} />}
      {linha.isSuccess &&
        (linha.data.length === 0 ? (
          <p className={estilos.vazio}>Nada aconteceu com este cliente ainda.</p>
        ) : (
          <ol className={estilos.acontecimentos}>
            {linha.data.map((acontecimento, posicao) => (
              <li key={`${acontecimento.em}-${String(posicao)}`}>
                <time dateTime={acontecimento.em}>{formatarDataEHora(acontecimento.em)}</time>
                <p>{acontecimento.descricao}</p>
              </li>
            ))}
          </ol>
        ))}
    </section>
  );
}
