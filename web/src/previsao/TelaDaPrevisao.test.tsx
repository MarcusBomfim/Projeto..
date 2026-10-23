import { screen, within } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, test } from 'vitest';
import { abrir } from '../testes/abrir';
import { escritorio, previsaoDoReadme } from '../testes/exemplosDaPrevisao';
import { servidor } from '../testes/servidor';

/** A API da previsão, guardando cada consulta que a tela fez. */
function responder(resposta = previsaoDoReadme) {
  const consultas: string[] = [];

  servidor.use(
    http.get('/api/relatorios/previsao', ({ request }) => {
      consultas.push(new URL(request.url).search);

      return HttpResponse.json(resposta);
    }),
  );

  return consultas;
}

describe('o painel da previsão', () => {
  test('o previsto do período, e a tabela dos números de cada mês ao lado do gráfico', async () => {
    const consultas = responder();
    abrir('/previsao?primeiro=2027-01&meses=3');

    expect(await screen.findByText(/previstos de jan\.\/2027 a mar\.\/2027/)).toHaveTextContent(
      'R$ 375.000,00 previstos de jan./2027 a mar./2027: R$ 0,00 já ganhos e R$ 375.000,00 em aberto, ponderados pela etapa de cada venda.',
    );
    expect(consultas).toEqual(['?primeiro=2027-01&meses=3']);

    const grafico = screen.getByRole('figure', { name: /Previsão de receita por mês/ });
    expect(grafico.querySelectorAll('svg g rect')).toHaveLength(9);
    expect(
      within(grafico)
        .getAllByRole('row')
        .map((linha) => linha.textContent.replace(/\s/g, ' ')),
    ).toEqual([
      'MêsJá ganhoEm abertoPonderadoPrevisto',
      'jan./2027R$ 0,00R$ 600.000,00R$ 360.000,00R$ 360.000,00',
      'fev./2027R$ 0,00R$ 150.000,00R$ 15.000,00R$ 15.000,00',
      'mar./2027R$ 0,00R$ 0,00R$ 0,00R$ 0,00',
    ]);
  });

  test('o previsto do mês é o já ganho mais o ponderado', async () => {
    responder({
      ...previsaoDoReadme,
      meses: [
        {
          mes: '2026-12',
          ganho: 78_500,
          emAberto: 150_000,
          ponderado: 15_000,
          previsto: 93_500,
          abertas: [],
        },
      ],
      ganho: 78_500,
      ponderado: 15_000,
      previsto: 93_500,
    });
    abrir('/previsao?primeiro=2026-12');

    expect(await screen.findByRole('row', { name: /^dez\.\/2026/ })).toHaveTextContent(
      'dez./2026R$ 78.500,00R$ 150.000,00R$ 15.000,00R$ 93.500,00',
    );
  });

  test('cada mês lista as vendas que devem fechar nele, e as que a previsão não conta ficam à parte', async () => {
    responder();
    abrir('/previsao?primeiro=2027-01&meses=3');

    const doMes = await screen.findByRole('region', { name: 'As vendas de cada mês' });
    expect(
      within(doMes)
        .getAllByRole('heading', { level: 3 })
        .map((titulo) => titulo.textContent),
    ).toEqual(['jan./2027', 'fev./2027']);
    expect(within(doMes).getByText(escritorio.titulo).parentElement).toHaveTextContent(
      'carlos.souza · R$ 600.000,00 · previsão para 29/01/2027',
    );

    const fora = screen.getByRole('region', { name: 'Fora da previsão' });
    expect(within(fora).getByText('Estacionamento').parentElement).toHaveTextContent(
      'previsão para 18/12/2026',
    );
    expect(within(fora).getAllByText('Nenhuma.')).toHaveLength(2);
  });

  test('o mês e o período escolhidos vão para o endereço e para a consulta', async () => {
    const consultas = responder();
    const { roteador } = abrir('/previsao');
    expect(await screen.findByRole('figure')).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('Meses'), '6');

    expect(roteador.state.location.search).toBe('?meses=6');
    expect(consultas).toEqual(['?meses=3', '?meses=6']);
    expect(screen.getByLabelText('A partir de')).toHaveValue('2027-01');
  });

  test('o mês que não se lê é recusado pela API, com a explicação dela', async () => {
    servidor.use(
      http.get('/api/relatorios/previsao', () =>
        HttpResponse.json(
          {
            title: 'O pedido não está no formato esperado.',
            status: 400,
            detail: 'O parâmetro primeiro é um mês, como 2026-10; veio "2027-13".',
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    abrir('/previsao?primeiro=2027-13');

    expect(await screen.findByRole('alert')).toHaveTextContent('O parâmetro primeiro é um mês, como 2026-10');
  });
});
