import { act, screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, onTestFinished, test, vi } from 'vitest';
import { abrir } from '../testes/abrir';
import { contasDaConstrutora } from '../testes/exemplos';
import { servidor } from '../testes/servidor';
import { ConstrutoraDosTestes, tokenDeTeste } from '../testes/tokens';
import { destinoSeguro } from './destino';

/** As contas como a API responde: cada construtora vê só as dela. */
const contasDeCadaConstrutora = http.get('/api/contas', ({ request }) => {
  const carga = request.headers.get('Authorization')?.split('.')[1] ?? '';
  const { construtora } = JSON.parse(Buffer.from(carga, 'base64url').toString()) as { construtora: string };

  return HttpResponse.json(construtora === ConstrutoraDosTestes ? contasDaConstrutora : []);
});

const tokenDaOutraConstrutora = tokenDeTeste({
  name: 'Bruno',
  construtora: '0199e3b4-0000-7000-8000-0000000000b2',
});

describe('a entrada', () => {
  test('sem sessão, o endereço pedido manda para a entrada, e entrar volta para ele', async () => {
    servidor.use(http.get('/api/contas', () => HttpResponse.json([])));
    const { roteador } = abrir('/contas?ordem=nome', { token: null });

    expect(await screen.findByRole('heading', { name: 'CRM de Obras' })).toBeInTheDocument();
    expect(roteador.state.location.pathname).toBe('/entrar');

    await userEvent.type(screen.getByLabelText('Token de acesso (JWT)'), tokenDeTeste());
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('heading', { name: 'Clientes' })).toBeInTheDocument();
    expect(roteador.state.location.search).toBe('?ordem=nome');
    expect(screen.getByText('Carlos Souza')).toBeInTheDocument();
    expect(screen.getByText('vendedor')).toBeInTheDocument();
  });

  test('o token que não serve não entra, e o motivo aparece', async () => {
    abrir('/entrar', { token: null });

    await userEvent.type(
      screen.getByLabelText('Token de acesso (JWT)'),
      tokenDeTeste({ construtora: undefined }),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(screen.getByRole('alert')).toHaveTextContent('falta a reivindicação construtora');
    expect(screen.getByLabelText('Token de acesso (JWT)')).toHaveAttribute('aria-invalid', 'true');
  });

  test('a API recusar o token leva de volta à entrada, dizendo por quê', async () => {
    servidor.use(
      http.get('/api/contas', () =>
        HttpResponse.json({ title: 'É preciso se autenticar.', status: 401 }, { status: 401 }),
      ),
    );
    const { roteador } = abrir('/contas');

    expect(await screen.findByText(/A API recusou o token/)).toBeInTheDocument();
    expect(roteador.state.location.pathname).toBe('/entrar');
    expect(sessionStorage.length).toBe(0);
  });

  test('sair esquece o token e o que a construtora leu, e volta à entrada', async () => {
    servidor.use(http.get('/api/contas', () => HttpResponse.json(contasDaConstrutora)));
    const { roteador, consultas } = abrir('/contas');
    expect(await screen.findByText('Logística Rota Sul')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }));

    expect(roteador.state.location.pathname).toBe('/entrar');
    expect(sessionStorage.length).toBe(0);
    expect(consultas.getQueryCache().getAll()).toHaveLength(0);
  });

  test('o token que vence com a tela aberta leva de volta à entrada, dizendo por quê', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    onTestFinished(() => {
      vi.useRealTimers();
    });
    servidor.use(http.get('/api/contas', () => HttpResponse.json([])));
    const { roteador } = abrir('/contas', {
      token: tokenDeTeste({ exp: Math.floor(Date.now() / 1000) + 60 }),
    });
    expect(await screen.findByText('Nenhum cliente ainda.')).toBeInTheDocument();

    await act(() => vi.advanceTimersByTimeAsync(60_000));

    expect(screen.getByRole('status')).toHaveTextContent('O token venceu. Entre de novo.');
    expect(roteador.state.location.pathname).toBe('/entrar');
    expect(sessionStorage.length).toBe(0);
  });

  test('trocar de sessão esvazia o cache: a outra construtora não vê o que a primeira leu', async () => {
    servidor.use(contasDeCadaConstrutora);
    abrir('/contas');
    expect(await screen.findByText('Logística Rota Sul')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }));
    await userEvent.type(screen.getByLabelText('Token de acesso (JWT)'), tokenDaOutraConstrutora);
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByText('Nenhum cliente ainda.')).toBeInTheDocument();
    expect(screen.queryByText('Logística Rota Sul')).not.toBeInTheDocument();
  });

  test('entrar com outro token sem sair antes também esvazia o cache', async () => {
    servidor.use(contasDeCadaConstrutora);
    const { roteador } = abrir('/contas');
    expect(await screen.findByText('Logística Rota Sul')).toBeInTheDocument();

    await act(() => roteador.navigate('/entrar?de=%2Fcontas'));
    await userEvent.type(screen.getByLabelText('Token de acesso (JWT)'), tokenDaOutraConstrutora);
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByText('Nenhum cliente ainda.')).toBeInTheDocument();
    expect(screen.getByText('Bruno')).toBeInTheDocument();
    expect(screen.queryByText('Logística Rota Sul')).not.toBeInTheDocument();
  });

  test('depois de entrar, só volta para dentro da aplicação', () => {
    expect(destinoSeguro('/contas/123?x=1')).toBe('/contas/123?x=1');
    expect(destinoSeguro('//site-de-fora.com')).toBe('/');
    expect(destinoSeguro('/\\site-de-fora.com')).toBe('/');
    expect(destinoSeguro('https://site-de-fora.com')).toBe('/');
    expect(destinoSeguro(null)).toBe('/');
  });
});
