import { screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, test } from 'vitest';
import { abrir } from '../testes/abrir';
import { contasDaConstrutora, Ids } from '../testes/exemplos';
import { servidor } from '../testes/servidor';

describe('os clientes', () => {
  test('a busca acha pelo nome sem acento e sem diferença de maiúscula, e cada um leva à ficha', async () => {
    servidor.use(http.get('/api/contas', () => HttpResponse.json(contasDaConstrutora)));
    abrir('/contas');
    expect(await screen.findAllByRole('link', { name: /Rota Sul|Alfa/ })).toHaveLength(3);

    await userEvent.type(screen.getByLabelText('Procurar pelo nome'), 'LOGISTICA');

    expect(screen.getAllByRole('link', { name: /Rota Sul|Alfa/ }).map((link) => link.textContent)).toEqual([
      'Logística Rota Sul',
      'Rota Sul Logística — Campinas',
    ]);
    expect(screen.getByRole('link', { name: 'Rota Sul Logística — Campinas' })).toHaveAttribute(
      'href',
      `/contas/${Ids.filial}`,
    );
  });

  test('a busca que não acha ninguém diz isso', async () => {
    servidor.use(http.get('/api/contas', () => HttpResponse.json(contasDaConstrutora)));
    abrir('/contas');
    await screen.findByText('Construtora Alfa');

    await userEvent.type(screen.getByLabelText('Procurar pelo nome'), 'Beta');

    expect(screen.getByText('Nenhum cliente com "Beta" no nome.')).toBeInTheDocument();
  });
});
