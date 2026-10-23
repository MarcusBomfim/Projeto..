import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, test } from 'vitest';
import type { Esquemas } from '../api/cliente';
import { abrir } from '../testes/abrir';
import { Etapas, funisDaConstrutora, galpaoEm, galpaoNoQuadro, Ids, quadroDoFunil } from '../testes/exemplos';
import { servidor } from '../testes/servidor';

// O Intl separa o R$ do número com um espaço que não quebra; a Testing
// Library o troca por espaço comum antes de comparar o texto da tela.

function responder(quadro: Esquemas['QuadroResposta'] = quadroDoFunil()) {
  servidor.use(
    http.get('/api/funis', () => HttpResponse.json(funisDaConstrutora)),
    http.get(`/api/funis/${Ids.funil}/quadro`, () => HttpResponse.json(quadro)),
  );
}

/** A API do quadro com o galpão andando: o PUT responde, e o quadro seguinte já o mostra na etapa nova. */
function responderMovendoOGalpao() {
  const pedidos: { readonly ifMatch: string | null; readonly etapa: string }[] = [];
  let etapaDoGalpao: keyof typeof Etapas = 'proposta';

  servidor.use(
    http.get('/api/funis', () => HttpResponse.json(funisDaConstrutora)),
    http.get(`/api/funis/${Ids.funil}/quadro`, () =>
      HttpResponse.json(
        quadroDoFunil({
          proposta: [],
          [etapaDoGalpao]: [
            etapaDoGalpao === 'proposta' ? galpaoNoQuadro : { ...galpaoNoQuadro, versao: 7, parada: false },
          ],
        }),
      ),
    ),
    http.put(`/api/oportunidades/${Ids.galpao}/etapa`, async ({ request }) => {
      const { etapa } = (await request.json()) as { etapa: string };
      pedidos.push({ ifMatch: request.headers.get('If-Match'), etapa });
      etapaDoGalpao = 'negociacao';

      return HttpResponse.json(galpaoEm(etapa), { headers: { ETag: '"7"' } });
    }),
  );

  return pedidos;
}

function coluna(nome: string): HTMLElement {
  return screen.getByRole('region', { name: nome });
}

describe('o funil em kanban', () => {
  test('mostra cada etapa com as vendas e os totais, e a venda parada', async () => {
    responder();
    abrir('/');

    expect(await screen.findByRole('region', { name: 'Proposta enviada' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 2 }).map((titulo) => titulo.textContent)).toEqual([
      'Primeiro contato',
      'Visita técnica',
      'Orçamento em elaboração',
      'Proposta enviada',
      'Negociação',
    ]);
    expect(screen.getByText(/^4 vendas abertas/)).toHaveTextContent(
      '4 vendas abertas, R$ 3.550.000,00 em aberto, R$ 2.015.000,00 ponderado pela probabilidade de cada etapa — 1 parada há mais tempo do que a etapa espera.',
    );

    const proposta = coluna('Proposta enviada');
    expect(proposta).toHaveTextContent('60%');
    expect(proposta).toHaveTextContent('Ponderado: R$ 1.440.000,00');

    const galpao = within(proposta).getByRole('article', { name: 'Galpão de 1.200 m² em Jundiaí' });
    expect(within(galpao).getByText('Parada')).toBeInTheDocument();
    expect(within(galpao).getByText('R$ 2.400.000,00')).toBeInTheDocument();
    expect(within(galpao).getByText('15/12/2026')).toBeInTheDocument();
    expect(within(galpao).getByRole('link', { name: 'Rota Sul Logística — Campinas' })).toHaveAttribute(
      'href',
      `/contas/${Ids.filial}`,
    );
    expect(within(coluna('Visita técnica')).getByText('Nenhuma venda aqui.')).toBeInTheDocument();
  });

  test('o seletor do cartão leva a venda a outra etapa, com a versão que o quadro mostrou', async () => {
    const pedidos = responderMovendoOGalpao();
    abrir('/');

    await userEvent.selectOptions(
      await screen.findByLabelText('Mover Galpão de 1.200 m² em Jundiaí para'),
      'Negociação',
    );

    expect(
      await within(coluna('Negociação')).findByRole('article', { name: 'Galpão de 1.200 m² em Jundiaí' }),
    ).toBeInTheDocument();
    expect(within(coluna('Proposta enviada')).queryByRole('article')).not.toBeInTheDocument();
    expect(pedidos).toEqual([{ ifMatch: '"6"', etapa: Etapas.negociacao }]);
    expect(screen.getByText('Galpão de 1.200 m² em Jundiaí foi para Negociação.')).toBeInTheDocument();
  });

  test('o seletor oferece as outras etapas abertas, e não a da própria venda', async () => {
    responder();
    abrir('/');

    const seletor = await screen.findByLabelText('Mover Galpão de 1.200 m² em Jundiaí para');

    expect(
      within(seletor)
        .getAllByRole('option')
        .map((opcao) => opcao.textContent),
    ).toEqual(['Mover para…', 'Primeiro contato', 'Visita técnica', 'Orçamento em elaboração', 'Negociação']);
  });

  test('arrastar o cartão para outra coluna faz o mesmo pedido', async () => {
    const pedidos = responderMovendoOGalpao();
    abrir('/');
    const galpao = await screen.findByRole('article', { name: 'Galpão de 1.200 m² em Jundiaí' });
    const dataTransfer = { setData: () => undefined, effectAllowed: '', dropEffect: '' };

    fireEvent.dragStart(galpao, { dataTransfer });
    fireEvent.dragOver(coluna('Negociação'), { dataTransfer });
    fireEvent.drop(coluna('Negociação'), { dataTransfer });

    expect(
      await within(coluna('Negociação')).findByRole('article', { name: 'Galpão de 1.200 m² em Jundiaí' }),
    ).toBeInTheDocument();
    expect(pedidos).toEqual([{ ifMatch: '"6"', etapa: Etapas.negociacao }]);
  });

  test('soltar na própria coluna não pede nada', async () => {
    const pedidos: string[] = [];
    responder();
    servidor.use(
      http.put(`/api/oportunidades/${Ids.galpao}/etapa`, async ({ request }) => {
        pedidos.push(((await request.json()) as { etapa: string }).etapa);

        return HttpResponse.json(galpaoEm(Etapas.negociacao));
      }),
    );
    abrir('/');
    const galpao = await screen.findByRole('article', { name: 'Galpão de 1.200 m² em Jundiaí' });
    const dataTransfer = { setData: () => undefined, effectAllowed: '', dropEffect: '' };

    fireEvent.dragStart(galpao, { dataTransfer });
    fireEvent.drop(coluna('Proposta enviada'), { dataTransfer });

    // O pedido que vem depois, pelo seletor, é o único que chega: o de antes não houve.
    await userEvent.selectOptions(
      screen.getByLabelText('Mover Galpão de 1.200 m² em Jundiaí para'),
      'Negociação',
    );
    await waitFor(() => {
      expect(pedidos).toEqual([Etapas.negociacao]);
    });
  });

  test('o que o domínio recusa aparece com a mensagem dele, e o cartão fica onde estava', async () => {
    let leituras = 0;
    servidor.use(
      http.get('/api/funis', () => HttpResponse.json(funisDaConstrutora)),
      http.get(`/api/funis/${Ids.funil}/quadro`, () => {
        leituras += 1;

        return HttpResponse.json(quadroDoFunil());
      }),
      http.put(`/api/oportunidades/${Ids.escritorio}/etapa`, () =>
        HttpResponse.json(
          {
            title: 'O pedido fere uma regra do negócio.',
            status: 422,
            detail:
              'Para a oportunidade Escritório de Campinas entrar em Proposta enviada, falta a previsão de fechamento.',
          },
          { status: 422, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    abrir('/');

    await userEvent.selectOptions(
      await screen.findByLabelText('Mover Escritório de Campinas para'),
      'Proposta enviada',
    );

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('A venda não mudou de etapa. O pedido fere uma regra do negócio.');
    expect(aviso).toHaveTextContent('falta a previsão de fechamento');
    await waitFor(() => {
      expect(leituras).toBe(2);
    });
    expect(
      within(coluna('Orçamento em elaboração')).getByRole('article', { name: 'Escritório de Campinas' }),
    ).toBeInTheDocument();
  });

  test('com mais de um funil, escolhe-se qual — e a escolha fica no endereço', async () => {
    servidor.use(
      http.get('/api/funis', () =>
        HttpResponse.json([
          ...funisDaConstrutora,
          {
            id: Ids.outroFunil,
            nome: 'Obras públicas',
            versao: 1,
            atualizadoEm: '2026-10-19T12:00:00+00:00',
          },
        ]),
      ),
      http.get(`/api/funis/${Ids.funil}/quadro`, () => HttpResponse.json(quadroDoFunil())),
      http.get(`/api/funis/${Ids.outroFunil}/quadro`, () =>
        HttpResponse.json({
          ...quadroDoFunil({ primeiroContato: [], orcamento: [], proposta: [], negociacao: [] }),
          nome: 'Obras públicas',
          funil: Ids.outroFunil,
        }),
      ),
    );
    const { roteador } = abrir('/');
    expect(await screen.findByText(/^4 vendas abertas/)).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('Funil'), 'Obras públicas');

    expect(await screen.findByText(/^0 vendas abertas/)).toBeInTheDocument();
    expect(roteador.state.location.search).toBe(`?funil=${Ids.outroFunil}`);
  });

  test('sem funil, diz quem o cria', async () => {
    servidor.use(http.get('/api/funis', () => HttpResponse.json([])));
    abrir('/');

    expect(
      await screen.findByText(/Nenhum funil ainda. A gerência cria o funil da construtora/),
    ).toBeInTheDocument();
  });

  test('a etapa arquivada que ainda tem venda aparece, mas não recebe venda nova', async () => {
    const quadro = quadroDoFunil();
    const sondagem: Esquemas['ColunaResposta'] = {
      etapa: '01a16700-0000-7000-8000-00000000e0a1',
      nome: 'Sondagem',
      probabilidade: 15,
      diasEsperados: null,
      arquivada: true,
      total: 0,
      ponderado: 0,
      semValor: 1,
      oportunidades: [
        {
          ...galpaoNoQuadro,
          id: '01a16700-0000-7000-8000-00000000b0a1',
          titulo: 'Reforma da sede',
          valorEstimado: null,
        },
      ],
    };
    responder({ ...quadro, colunas: [...quadro.colunas, sondagem] });
    abrir('/');

    expect(await screen.findByText(/^5 vendas abertas/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Sondagem (arquivada)' })).toBeInTheDocument();
    const seletor = screen.getByLabelText('Mover Galpão de 1.200 m² em Jundiaí para');
    expect(within(seletor).queryByRole('option', { name: 'Sondagem' })).not.toBeInTheDocument();
  });
});
