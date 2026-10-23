import { request, test as preparacao, type APIRequestContext } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { alterar, criar, diaDaqui, instanteDaqui } from '../apoio/api';
import { guardarCarteira } from '../apoio/estado';
import { emitirToken } from '../apoio/identidade';
import { Api } from '../apoio/pilha';

/*
 * A carteira da Rota Sul, do exemplo do README, criada pela API numa
 * construtora nova — cada execução tem a sua, e não enxerga as anteriores
 * (ADR-0011) —, nas datas de hoje: o lead da Marina convertido na filial, o
 * galpão na proposta, a ligação de retorno marcada, e mais seis vendas — uma
 * ganha, uma perdida, e uma que acabou de chegar, sem valor nem quem decide.
 */

interface Etapa {
  readonly id: string;
  readonly nome: string;
}

interface Contato {
  readonly id: string;
  readonly nome: string;
}

async function contexto(construtora: string, nome: string, login: string, papel: string) {
  return request.newContext({
    baseURL: Api,
    extraHTTPHeaders: {
      Authorization: `Bearer ${emitirToken({ construtora, nome, login, papeis: [papel] })}`,
    },
  });
}

const Itens = [
  {
    descricao: 'Piso industrial em concreto armado, fck 30 MPa, 15 cm',
    quantidade: 1200,
    unidade: 'm²',
    precoUnitario: 185.5,
  },
  { descricao: 'Estrutura metálica do galpão', quantidade: 1, unidade: 'vb', precoUnitario: 1_450_000 },
  { descricao: 'Cobertura em telha termoacústica', quantidade: 1350, unidade: 'm²', precoUnitario: 312.4 },
];

preparacao('a carteira da Rota Sul entra pela API', async () => {
  const construtora = randomUUID();
  const carlos = await contexto(construtora, 'Carlos Souza', 'carlos.souza', 'vendedor');
  const helena = await contexto(construtora, 'Helena Martins', 'helena.martins', 'gerente');

  // A matriz já é cliente; a Marina chega pelo site, se qualifica e vira a conta da filial.
  await criar(carlos, '/api/contas', {
    nome: 'Logística Rota Sul',
    documento: '11.222.333/0001-81',
    contatos: [
      {
        nome: 'Otávio Reis',
        email: 'otavio.reis@rotasul.com.br',
        telefone: '(11) 3333-4444',
        cargo: 'Diretor de obras',
        papel: 'Decisor',
      },
    ],
  });
  const lead = await criar(carlos, '/api/leads', {
    nome: 'Marina Costa',
    origem: 'Site',
    empresa: 'Rota Sul Logística — Campinas',
    documento: '11.222.333/0002-62',
    email: 'Marina.Costa@RotaSul.com.br',
    telefone: '+55 19 99876-5432',
    interesse: 'Galpão de 1.200 m² em Jundiaí',
  });
  const doLead = `/api/leads/${lead.id}`;
  await alterar(carlos, 'PUT', doLead, '/avaliacao', {
    orcamento: 'Sim',
    falaComQuemDecide: 'Nao',
    necessidade: 'Sim',
    prazo: 'Sim',
  });
  await alterar(carlos, 'POST', doLead, '/qualificacao');
  const { conta } = await alterar<{ conta: { id: string } }>(carlos, 'POST', doLead, '/conversao', {});

  // O funil padrão, criado pela gerência; o galpão nasce do lead e anda até a proposta.
  const funil = await criar<{ etapas: Etapa[] }>(helena, '/api/funis/padrao');
  const etapa = (nome: string) => funil.etapas.find((candidata) => candidata.nome === nome)?.id ?? '';

  const galpao = await criar(carlos, '/api/oportunidades/do-lead', { lead: lead.id, funil: funil.id });
  const venda = `/api/oportunidades/${galpao.id}`;
  await alterar(carlos, 'PUT', venda, '/etapa', { etapa: etapa('Visita técnica') });
  const { contatos } = await alterar<{ contatos: Contato[] }>(
    carlos,
    'POST',
    `/api/contas/${conta.id}`,
    '/contatos',
    {
      nome: 'Sérgio Lima',
      email: 'sergio.lima@rotasul.com.br',
      cargo: 'Gerente da filial',
      papel: 'Decisor',
    },
  );
  const sergio = contatos.find((contato) => contato.nome === 'Sérgio Lima')?.id ?? '';
  await alterar(carlos, 'PATCH', venda, '', {
    quemDecide: sergio,
    valorEstimado: 2_400_000,
    previsaoDeFechamento: diaDaqui(45),
  });
  await alterar(carlos, 'PUT', venda, '/etapa', { etapa: etapa('Orçamento em elaboração') });
  await alterar(carlos, 'PUT', venda, '/etapa', { etapa: etapa('Proposta enviada') });

  const proposta = await criar(carlos, '/api/propostas', {
    oportunidade: galpao.id,
    versao: {
      itens: Itens,
      desconto: 0,
      validaAte: diaDaqui(60),
      condicoesDePagamento: '30% na assinatura e o resto por medição',
    },
  });
  await alterar(carlos, 'POST', `/api/propostas/${proposta.id}`, '/envio');
  await criar(carlos, '/api/atividades', {
    conta: conta.id,
    tipo: 'Ligacao',
    assunto: 'Retorno da proposta',
    previstaPara: instanteDaqui(1, 2),
    com: sergio,
    oportunidade: galpao.id,
  });

  // O resto da carteira da filial.
  await outraVenda(carlos, conta.id, funil.id, sergio, 'Escritório de Campinas', 600_000, diaDaqui(80), [
    etapa('Visita técnica'),
    etapa('Orçamento em elaboração'),
  ]);
  await outraVenda(carlos, conta.id, funil.id, sergio, 'Portaria de Jundiaí', 150_000, diaDaqui(110), []);
  await outraVenda(carlos, conta.id, funil.id, sergio, 'Estacionamento', 400_000, diaDaqui(-5), [
    etapa('Negociação'),
  ]);
  const doca = await outraVenda(carlos, conta.id, funil.id, sergio, 'Doca de Sumaré', 300_000, diaDaqui(20), [
    etapa('Visita técnica'),
  ]);
  await alterar(carlos, 'POST', `/api/oportunidades/${doca}`, '/perda', {
    motivo: 'Preco',
    concorrente: 'Construtora Beta',
  });
  const refeitorio = await outraVenda(
    carlos,
    conta.id,
    funil.id,
    sergio,
    'Reforma do refeitório',
    80_000,
    diaDaqui(3),
    [etapa('Negociação')],
  );
  await alterar(carlos, 'POST', `/api/oportunidades/${refeitorio}`, '/ganho', { valorFechado: 78_500 });

  // E a que acabou de chegar, sem valor nem quem decide: o orçamento não a recebe.
  await criar(carlos, '/api/oportunidades', {
    conta: conta.id,
    funil: funil.id,
    titulo: 'Guarita da entrada',
  });

  guardarCarteira({
    construtora,
    token: emitirToken({ construtora, nome: 'Carlos Souza', login: 'carlos.souza', papeis: ['vendedor'] }),
    funil: funil.id,
    filial: conta.id,
    galpao: galpao.id,
  });
  await carlos.dispose();
  await helena.dispose();
});

/** Uma venda com valor, previsão e quem decide, levada pelas etapas dadas. Devolve o id. */
async function outraVenda(
  api: APIRequestContext,
  conta: string,
  funil: string,
  quemDecide: string,
  titulo: string,
  valorEstimado: number,
  previsaoDeFechamento: string,
  etapas: readonly string[],
): Promise<string> {
  const venda = await criar(api, '/api/oportunidades', {
    conta,
    funil,
    titulo,
    valorEstimado,
    previsaoDeFechamento,
    quemDecide,
  });

  for (const etapa of etapas) {
    await alterar(api, 'PUT', `/api/oportunidades/${venda.id}`, '/etapa', { etapa });
  }

  return venda.id;
}
