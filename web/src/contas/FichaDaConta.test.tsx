import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, onTestFinished, test, vi } from 'vitest';
import { abrir } from '../testes/abrir';
import {
  atividadesDaFilial,
  filial,
  funilPadrao,
  Ids,
  linhaDoTempoDaFilial,
  vendasDaFilial,
} from '../testes/exemplos';
import { servidor } from '../testes/servidor';

/** A API da ficha da filial: a conta, as vendas, as atividades, a história e o funil das vendas. */
function responderAFicha() {
  servidor.use(
    http.get(`/api/contas/${Ids.filial}`, () => HttpResponse.json(filial, { headers: { ETag: '"3"' } })),
    http.get('/api/oportunidades', ({ request }) =>
      HttpResponse.json(new URL(request.url).searchParams.get('conta') === Ids.filial ? vendasDaFilial : []),
    ),
    http.get('/api/atividades', ({ request }) =>
      HttpResponse.json(
        new URL(request.url).searchParams.get('conta') === Ids.filial ? atividadesDaFilial : [],
      ),
    ),
    http.get(`/api/contas/${Ids.filial}/linha-do-tempo`, () => HttpResponse.json(linhaDoTempoDaFilial)),
    http.get(`/api/funis/${Ids.funil}`, () => HttpResponse.json(funilPadrao)),
  );
}

describe('a ficha do cliente', () => {
  test('mostra o documento e os contatos, com o principal marcado e como falar com cada um', async () => {
    responderAFicha();
    abrir(`/contas/${Ids.filial}`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Rota Sul Logística — Campinas' }),
    ).toBeInTheDocument();
    expect(screen.getByText('11.222.333/0002-62')).toBeInTheDocument();

    const contatos = within(screen.getByRole('region', { name: 'Contatos' })).getAllByRole('listitem');
    expect(contatos.map((contato) => contato.textContent)).toEqual([
      expect.stringContaining('Marina Costa Principal'),
      expect.stringContaining('Sérgio Lima Gerente da filial · Decide'),
    ]);
    expect(
      within(contatos[0] ?? document.body).getByRole('link', { name: '(19) 99876-5432' }),
    ).toHaveAttribute('href', 'tel:19998765432');
    expect(
      within(contatos[1] ?? document.body).getByRole('link', { name: 'sergio.lima@rotasul.com.br' }),
    ).toHaveAttribute('href', 'mailto:sergio.lima@rotasul.com.br');
  });

  test('as vendas dizem em que etapa estão, pelo nome que o funil dá a ela', async () => {
    responderAFicha();
    abrir(`/contas/${Ids.filial}`);

    const vendas = await screen.findByRole('region', { name: 'Vendas' });
    const galpao = await within(vendas).findByRole('row', { name: /^Galpão de 1.200 m² em Jundiaí/ });

    expect(galpao).toHaveTextContent('Ganha');
    expect(await within(vendas).findByText('Primeiro contato')).toBeInTheDocument();
    expect(within(vendas).getByRole('row', { name: /^Portaria de Jundiaí/ })).toHaveTextContent(
      'R$ 150.000,00',
    );
    expect(within(galpao).getByRole('link')).toHaveAttribute('href', `/?funil=${Ids.funil}`);
  });

  test('a atividade pendente que passou da hora aparece atrasada', async () => {
    vi.useFakeTimers({ now: new Date('2026-12-03T12:00:00Z'), shouldAdvanceTime: true });
    onTestFinished(() => {
      vi.useRealTimers();
    });
    responderAFicha();
    abrir(`/contas/${Ids.filial}`);

    const atividades = await screen.findByRole('region', { name: 'Atividades' });
    const visita = await within(atividades).findByRole('row', { name: /Visita ao terreno da portaria/ });

    expect(visita).toHaveTextContent('02/12/2026, 14:00');
    expect(visita).toHaveTextContent('joao.engenharia');
    expect(within(visita).getByText('Atrasada')).toBeInTheDocument();
    expect(within(atividades).getByRole('row', { name: /Retorno da proposta/ })).toHaveTextContent(
      'Concluída',
    );
  });

  test('a linha do tempo conta a história do mais recente para o mais antigo', async () => {
    responderAFicha();
    abrir(`/contas/${Ids.filial}`);

    const linha = await screen.findByRole('region', { name: 'Linha do tempo' });
    const acontecimentos = await within(linha).findAllByRole('listitem');

    expect(acontecimentos.map((acontecimento) => acontecimento.textContent)).toEqual([
      '07/12/2026, 11:00Venda ganha: Galpão de 1.200 m² em Jundiaí, por R$ 1.843.019,20',
      '07/12/2026, 11:00Proposta aceita: versão 2, R$ 1.843.019,20',
      '30/11/2026, 11:00Proposta enviada: versão 2, R$ 1.843.019,20, com 12% de desconto',
      '27/11/2026, 14:30Ligação: Retorno da proposta — Achou caro: pede 12% de desconto para fechar este ano',
      '21/10/2026, 09:00Venda aberta: Galpão de 1.200 m² em Jundiaí, em Primeiro contato',
    ]);
  });

  test('a conta que não existe — ou é de outra construtora — diz isso, e leva de volta aos clientes', async () => {
    servidor.use(
      http.get(`/api/contas/${Ids.matriz}`, () =>
        HttpResponse.json(
          { title: 'Não encontrado.', status: 404, detail: `Não existe a conta ${Ids.matriz}.` },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    abrir(`/contas/${Ids.matriz}`);

    expect(await screen.findByRole('alert')).toHaveTextContent(`Não existe a conta ${Ids.matriz}.`);
    expect(screen.getByRole('link', { name: 'Voltar aos clientes' })).toHaveAttribute('href', '/contas');
  });
});
