import { expect, request, test, type APIRequestContext } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';
import { alterar, criar, diaDaqui } from '../apoio/api';
import { emitirToken, emitirTokenDoPlanejamento } from '../apoio/identidade';
import { Api, Painel, Planejamento } from '../apoio/pilha';

/*
 * A proposta aceita vira obra (ADR-0016, ADR-0017), contra a pilha do
 * Compose: a API, o processador e o planejamento-obras, cada um no seu
 * contêiner. O aceite ganha a venda no CRM; o cronograma da obra aparece no
 * planejamento, na mesma construtora, com o nome da obra — e um só.
 *
 * Com IMAGENS=1, o rastro do aceite é fotografado no painel do
 * OpenTelemetry para o README (ADR-0018): os três serviços num rastro só.
 */

interface Resumo {
  readonly id: string;
  readonly nome: string;
}

async function doCrm(construtora: string, nome: string, login: string, papel: string) {
  return request.newContext({
    baseURL: Api,
    extraHTTPHeaders: {
      Authorization: `Bearer ${emitirToken({ construtora, nome, login, papeis: [papel] })}`,
    },
  });
}

/** O planejamento visto por quem planeja: a construtora do CRM é a empresa dele, com o mesmo id. */
async function doPlanejamento(construtora: string): Promise<APIRequestContext> {
  return request.newContext({
    baseURL: Planejamento,
    extraHTTPHeaders: {
      Authorization: `Bearer ${emitirTokenDoPlanejamento({ empresa: construtora, nome: 'Rita Planejamento', papeis: ['planejador'] })}`,
    },
  });
}

async function cronogramas(planejamento: APIRequestContext): Promise<string[]> {
  const resposta = await planejamento.get('/api/cronogramas');
  expect(resposta.status(), await resposta.text()).toBe(200);

  return ((await resposta.json()) as Resumo[]).map((cronograma) => cronograma.nome);
}

test('a proposta aceita no CRM vira o cronograma da obra no planejamento-obras', async ({ page }) => {
  const construtora = randomUUID();
  const carlos = await doCrm(construtora, 'Carlos Souza', 'carlos.souza', 'vendedor');
  const helena = await doCrm(construtora, 'Helena Martins', 'helena.martins', 'gerente');
  const planejamento = await doPlanejamento(construtora);

  const conta = await criar(carlos, '/api/contas', {
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
  const funil = await criar(helena, '/api/funis/padrao');
  const venda = await criar(carlos, '/api/oportunidades', {
    conta: conta.id,
    funil: funil.id,
    titulo: 'Galpão de 1.200 m² em Jundiaí',
  });
  const proposta = await criar(carlos, '/api/propostas', {
    oportunidade: venda.id,
    versao: {
      itens: [
        { descricao: 'Estrutura metálica do galpão', quantidade: 1, unidade: 'vb', precoUnitario: 1_450_000 },
      ],
      desconto: 0,
      validaAte: diaDaqui(30),
      condicoesDePagamento: '30% na assinatura e o resto por medição',
    },
  });
  const daProposta = `/api/propostas/${proposta.id}`;

  expect(await cronogramas(planejamento)).toEqual([]);

  await alterar(carlos, 'POST', daProposta, '/envio');

  // O aceite chega num rastro que o teste conhece, como de um front-end instrumentado.
  const rastro = randomBytes(16).toString('hex');
  const lida = await carlos.get(daProposta);
  const aceite = await carlos.post(`${daProposta}/aceite`, {
    headers: {
      'If-Match': lida.headers().etag ?? '',
      traceparent: `00-${rastro}-${randomBytes(8).toString('hex')}-01`,
    },
  });
  expect(aceite.status(), await aceite.text()).toBe(200);
  expect(
    ((await aceite.json()) as { readonly oportunidade: { readonly situacao: string } }).oportunidade.situacao,
  ).toBe('Ganha');

  // O processador acorda com a mensagem nova e leva a venda ao planejamento:
  // em instantes, e não na próxima volta da fila.
  await expect
    .poll(() => cronogramas(planejamento), { timeout: 20_000 })
    .toEqual(['Galpão de 1.200 m² em Jundiaí']);

  // Outra construtora não vê a obra desta.
  expect(await cronogramas(await doPlanejamento(randomUUID()))).toEqual([]);

  if (process.env.IMAGENS === '1') {
    await page.setViewportSize({ width: 1400, height: 980 });

    // Cada serviço manda a telemetria em lotes, no seu tempo: o rastro se
    // completa aos poucos no painel, e a foto espera as duas pontas dele.
    await expect(async () => {
      await page.goto(`${Painel}/traces/detail/${rastro}`);
      await expect(page.getByText('POST /api/propostas/{id:guid}/aceite', { exact: true })).toBeVisible({
        timeout: 2_000,
      });
      await expect(page.getByText('POST /api/cronogramas/', { exact: true })).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 30_000 });

    await page.locator('[aria-label="Dismiss notification"]').click();
    await page.screenshot({ path: '../docs/imagens/rastro.png' });
  }
});
