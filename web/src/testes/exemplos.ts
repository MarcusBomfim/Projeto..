import type { Esquemas } from '../api/cliente';

/*
 * A carteira da filial da Rota Sul, do exemplo do README, como a API a
 * devolve: o funil padrão, o quadro numa quinta de novembro, a ficha da
 * filial e a história dela. Tipada pelo contrato: se a API mudar o formato,
 * estes exemplos deixam de compilar.
 */

export const Ids = {
  funil: '01a16700-0000-7000-8000-00000000f001',
  outroFunil: '01a16700-0000-7000-8000-00000000f002',
  filial: '01a16700-0000-7000-8000-00000000c001',
  matriz: '01a16700-0000-7000-8000-00000000c002',
  marina: '01a16700-0000-7000-8000-00000000a001',
  sergio: '01a16700-0000-7000-8000-00000000a002',
  galpao: '01a16700-0000-7000-8000-00000000b001',
  escritorio: '01a16700-0000-7000-8000-00000000b002',
  portaria: '01a16700-0000-7000-8000-00000000b003',
  estacionamento: '01a16700-0000-7000-8000-00000000b004',
  retorno: '01a16700-0000-7000-8000-00000000d001',
  visita: '01a16700-0000-7000-8000-00000000d002',
} as const;

export const Etapas = {
  primeiroContato: '01a16700-0000-7000-8000-00000000e001',
  visitaTecnica: '01a16700-0000-7000-8000-00000000e002',
  orcamento: '01a16700-0000-7000-8000-00000000e003',
  proposta: '01a16700-0000-7000-8000-00000000e004',
  negociacao: '01a16700-0000-7000-8000-00000000e005',
  ganha: '01a16700-0000-7000-8000-00000000e006',
  perdida: '01a16700-0000-7000-8000-00000000e007',
} as const;

export const funisDaConstrutora: Esquemas['ResumoResposta'][] = [
  { id: Ids.funil, nome: 'Vendas de obras', versao: 1, atualizadoEm: '2026-10-19T12:00:00+00:00' },
];

export const funilPadrao: Esquemas['FunilResposta'] = {
  id: Ids.funil,
  nome: 'Vendas de obras',
  etapas: [
    {
      id: Etapas.primeiroContato,
      nome: 'Primeiro contato',
      tipo: 'Aberta',
      probabilidade: 10,
      diasEsperados: 7,
      requisitos: [],
    },
    {
      id: Etapas.visitaTecnica,
      nome: 'Visita técnica',
      tipo: 'Aberta',
      probabilidade: 20,
      diasEsperados: 14,
      requisitos: [],
    },
    {
      id: Etapas.orcamento,
      nome: 'Orçamento em elaboração',
      tipo: 'Aberta',
      probabilidade: 40,
      diasEsperados: 21,
      requisitos: ['QuemDecide'],
    },
    {
      id: Etapas.proposta,
      nome: 'Proposta enviada',
      tipo: 'Aberta',
      probabilidade: 60,
      diasEsperados: 15,
      requisitos: ['ValorEstimado', 'PrevisaoDeFechamento'],
    },
    {
      id: Etapas.negociacao,
      nome: 'Negociação',
      tipo: 'Aberta',
      probabilidade: 80,
      diasEsperados: 30,
      requisitos: [],
    },
    {
      id: Etapas.ganha,
      nome: 'Ganha',
      tipo: 'Ganha',
      probabilidade: 100,
      diasEsperados: null,
      requisitos: [],
    },
    {
      id: Etapas.perdida,
      nome: 'Perdida',
      tipo: 'Perdida',
      probabilidade: 0,
      diasEsperados: null,
      requisitos: [],
    },
  ],
  arquivadas: [],
};

const Filial = 'Rota Sul Logística — Campinas';

function cartao(
  id: string,
  titulo: string,
  valorEstimado: number | null,
  previsaoDeFechamento: string | null,
  entrouNaEtapaEm: string,
  versao: number,
  parada = false,
): Esquemas['CartaoResposta'] {
  return {
    id,
    titulo,
    conta: Ids.filial,
    nomeDaConta: Filial,
    responsavel: 'carlos.souza',
    valorEstimado,
    previsaoDeFechamento,
    entrouNaEtapaEm,
    parada,
    versao,
  };
}

export const galpaoNoQuadro = cartao(
  Ids.galpao,
  'Galpão de 1.200 m² em Jundiaí',
  2_400_000,
  '2026-12-15',
  '2026-11-05T12:00:00+00:00',
  6,
  true,
);

function coluna(
  etapa: string,
  nome: string,
  probabilidade: number,
  diasEsperados: number,
  oportunidades: Esquemas['CartaoResposta'][],
): Esquemas['ColunaResposta'] {
  const total = oportunidades.reduce((soma, venda) => soma + (venda.valorEstimado ?? 0), 0);

  return {
    etapa,
    nome,
    probabilidade,
    diasEsperados,
    arquivada: false,
    total,
    ponderado: (total * probabilidade) / 100,
    semValor: oportunidades.filter((venda) => venda.valorEstimado === null).length,
    oportunidades,
  };
}

/** O quadro do funil na quinta, 26/11: o galpão parado na proposta, e mais três vendas. */
export function quadroDoFunil(
  colunas: Partial<Record<keyof typeof Etapas, Esquemas['CartaoResposta'][]>> = {},
): Esquemas['QuadroResposta'] {
  const vendas = {
    primeiroContato: [
      cartao(Ids.portaria, 'Portaria de Jundiaí', 150_000, '2027-02-26', '2026-11-26T12:00:00+00:00', 1),
    ],
    visitaTecnica: [],
    orcamento: [
      cartao(Ids.escritorio, 'Escritório de Campinas', 600_000, '2027-01-29', '2026-11-17T12:00:00+00:00', 3),
    ],
    proposta: [galpaoNoQuadro],
    negociacao: [
      cartao(Ids.estacionamento, 'Estacionamento', 400_000, '2026-12-18', '2026-11-05T12:00:00+00:00', 2),
    ],
    ...colunas,
  };
  const lista = [
    coluna(Etapas.primeiroContato, 'Primeiro contato', 10, 7, vendas.primeiroContato),
    coluna(Etapas.visitaTecnica, 'Visita técnica', 20, 14, vendas.visitaTecnica),
    coluna(Etapas.orcamento, 'Orçamento em elaboração', 40, 21, vendas.orcamento),
    coluna(Etapas.proposta, 'Proposta enviada', 60, 15, vendas.proposta),
    coluna(Etapas.negociacao, 'Negociação', 80, 30, vendas.negociacao),
  ];

  return {
    funil: Ids.funil,
    nome: 'Vendas de obras',
    total: lista.reduce((soma, item) => soma + item.total, 0),
    ponderado: lista.reduce((soma, item) => soma + item.ponderado, 0),
    colunas: lista,
  };
}

/** O galpão como a API o devolve depois de ir para a etapa pedida. */
export function galpaoEm(etapa: string): Esquemas['OportunidadeResposta'] {
  return {
    id: Ids.galpao,
    conta: Ids.filial,
    funil: Ids.funil,
    titulo: 'Galpão de 1.200 m² em Jundiaí',
    responsavel: 'carlos.souza',
    criadaEm: '2026-10-21T12:00:00+00:00',
    lead: '01a16700-0000-7000-8000-0000000000f1',
    situacao: 'Aberta',
    etapa,
    entrouNaEtapaEm: '2026-11-26T12:00:00+00:00',
    valorEstimado: 2_400_000,
    previsaoDeFechamento: '2026-12-15',
    quemDecide: Ids.sergio,
    valorFechado: null,
    perda: null,
    fechadaEm: null,
    passagens: [],
  };
}

export const filial: Esquemas['ContaResposta'] = {
  id: Ids.filial,
  nome: Filial,
  documento: '11.222.333/0002-62',
  contatos: [
    {
      id: Ids.marina,
      nome: 'Marina Costa',
      cargo: null,
      email: 'marina.costa@rotasul.com.br',
      telefone: '(19) 99876-5432',
      papel: 'NaoSabido',
    },
    {
      id: Ids.sergio,
      nome: 'Sérgio Lima',
      cargo: 'Gerente da filial',
      email: 'sergio.lima@rotasul.com.br',
      telefone: null,
      papel: 'Decisor',
    },
  ],
  principal: Ids.marina,
};

export const contasDaConstrutora: Esquemas['ResumoResposta'][] = [
  { id: Ids.matriz, nome: 'Logística Rota Sul', versao: 1, atualizadoEm: '2026-10-19T12:00:00+00:00' },
  { id: Ids.filial, nome: Filial, versao: 3, atualizadoEm: '2026-10-26T12:00:00+00:00' },
  {
    id: '01a16700-0000-7000-8000-00000000c003',
    nome: 'Construtora Alfa',
    versao: 1,
    atualizadoEm: '2026-10-20T12:00:00+00:00',
  },
];

export const vendasDaFilial: Esquemas['ResumoDaOportunidadeResposta'][] = [
  {
    id: Ids.galpao,
    titulo: 'Galpão de 1.200 m² em Jundiaí',
    conta: Ids.filial,
    funil: Ids.funil,
    etapa: Etapas.ganha,
    situacao: 'Ganha',
    responsavel: 'carlos.souza',
    valorEstimado: 2_400_000,
    versao: 8,
  },
  {
    id: Ids.portaria,
    titulo: 'Portaria de Jundiaí',
    conta: Ids.filial,
    funil: Ids.funil,
    etapa: Etapas.primeiroContato,
    situacao: 'Aberta',
    responsavel: 'carlos.souza',
    valorEstimado: 150_000,
    versao: 1,
  },
];

export const atividadesDaFilial: Esquemas['ResumoDaAtividadeResposta'][] = [
  {
    id: Ids.retorno,
    tipo: 'Ligacao',
    assunto: 'Retorno da proposta',
    previstaPara: '2026-11-27T13:00:00+00:00',
    responsavel: 'carlos.souza',
    situacao: 'Concluida',
    oportunidade: Ids.galpao,
    versao: 2,
  },
  {
    id: Ids.visita,
    tipo: 'Visita',
    assunto: 'Visita ao terreno da portaria',
    previstaPara: '2026-12-02T17:00:00+00:00',
    responsavel: 'joao.engenharia',
    situacao: 'Pendente',
    oportunidade: Ids.portaria,
    versao: 1,
  },
];

/** O começo e o fim da história da filial no README, do mais recente para o mais antigo. */
export const linhaDoTempoDaFilial: Esquemas['AcontecimentoResposta'][] = [
  {
    em: '2026-12-07T14:00:00+00:00',
    descricao: 'Venda ganha: Galpão de 1.200 m² em Jundiaí, por R$ 1.843.019,20',
    oportunidade: Ids.galpao,
  },
  {
    em: '2026-12-07T14:00:00+00:00',
    descricao: 'Proposta aceita: versão 2, R$ 1.843.019,20',
    oportunidade: Ids.galpao,
  },
  {
    em: '2026-11-30T14:00:00+00:00',
    descricao: 'Proposta enviada: versão 2, R$ 1.843.019,20, com 12% de desconto',
    oportunidade: Ids.galpao,
  },
  {
    em: '2026-11-27T17:30:00+00:00',
    descricao: 'Ligação: Retorno da proposta — Achou caro: pede 12% de desconto para fechar este ano',
    oportunidade: Ids.galpao,
  },
  {
    em: '2026-10-21T12:00:00+00:00',
    descricao: 'Venda aberta: Galpão de 1.200 m² em Jundiaí, em Primeiro contato',
    oportunidade: Ids.galpao,
  },
];
