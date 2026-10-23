import { expect, test, type Page } from '@playwright/test';
import { diaDaqui } from '../apoio/api';
import { lerCarteira } from '../apoio/estado';

/*
 * O exemplo do README no navegador, contra a pilha inteira: o banco, a API e
 * o front-end de produção. Cada teste entra pela tela de entrada, como uma
 * pessoa entraria.
 *
 * Com IMAGENS=1, cada tela também é fotografada para o README, em
 * docs/imagens.
 */

test.describe.configure({ mode: 'serial' });

const Meses = [
  'jan.',
  'fev.',
  'mar.',
  'abr.',
  'maio',
  'jun.',
  'jul.',
  'ago.',
  'set.',
  'out.',
  'nov.',
  'dez.',
];

/** "2026-12-07" → "dez./2026": o mês como a previsão o escreve. */
function mesDe(dia: string): string {
  const [ano, mes] = dia.split('-');

  return `${Meses[Number(mes) - 1] ?? ''}/${ano ?? ''}`;
}

async function entrar(page: Page, endereco: string) {
  await page.goto(endereco);
  await expect(page).toHaveURL(/\/entrar/);

  await page.getByLabel('Token de acesso (JWT)').fill(lerCarteira().token);
  await page.getByRole('button', { name: 'Entrar' }).click();
}

async function fotografar(page: Page, nome: string) {
  if (process.env.IMAGENS === '1') {
    await page.screenshot({ path: `../docs/imagens/${nome}.png`, fullPage: true });
  }
}

const coluna = (page: Page, nome: string) => page.getByRole('region', { name: nome, exact: true });

test('o funil em kanban: a venda arrastada vai para a etapa, e a que não tem o que ela pede fica', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await entrar(page, '/');

  await expect(coluna(page, 'Proposta enviada').getByRole('article')).toHaveAccessibleName(
    'Galpão de 1.200 m² em Jundiaí',
  );
  await expect(page.getByText(/^5 vendas abertas/)).toContainText('R$ 3.550.000,00 em aberto');
  await fotografar(page, 'funil');

  // Arrastar de verdade, com o drag and drop do navegador.
  const escritorio = page.getByRole('article', { name: 'Escritório de Campinas' });
  await escritorio.dragTo(coluna(page, 'Proposta enviada'));
  await expect(
    coluna(page, 'Proposta enviada').getByRole('article', { name: 'Escritório de Campinas' }),
  ).toBeVisible();
  await expect(page.getByText('Escritório de Campinas foi para Proposta enviada.')).toBeAttached();

  // Pelo teclado: o seletor do cartão. O orçamento pede quem decide, e a guarita não tem.
  await page.getByLabel('Mover Guarita da entrada para').selectOption('Orçamento em elaboração');
  await expect(page.getByRole('alert')).toContainText(
    'Para a oportunidade Guarita da entrada entrar em Orçamento em elaboração, falta quem decide.',
  );
  await expect(
    coluna(page, 'Primeiro contato').getByRole('article', { name: 'Guarita da entrada' }),
  ).toBeVisible();
});

test('a ficha da filial: os contatos, as vendas pela etapa e a linha do tempo', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await entrar(page, '/');

  await coluna(page, 'Proposta enviada')
    .getByRole('article', { name: 'Galpão de 1.200 m² em Jundiaí' })
    .getByRole('link', { name: 'Rota Sul Logística — Campinas' })
    .click();

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Rota Sul Logística — Campinas');
  await expect(page.getByText('11.222.333/0002-62')).toBeVisible();

  const contatos = page.getByRole('region', { name: 'Contatos' });
  await expect(contatos.getByRole('listitem').first()).toContainText('Marina Costa');
  await expect(contatos.getByRole('listitem').first()).toContainText('Principal');
  await expect(contatos.getByRole('listitem').filter({ hasText: 'Sérgio Lima' })).toContainText('Decide');

  const vendas = page.getByRole('region', { name: 'Vendas' });
  await expect(vendas.getByRole('row', { name: /^Galpão de 1.200 m² em Jundiaí/ })).toContainText(
    'Proposta enviada',
  );
  await expect(vendas.getByRole('row', { name: /^Doca de Sumaré/ })).toContainText('Perdida');
  await expect(vendas.getByRole('row', { name: /^Reforma do refeitório/ })).toContainText('Ganha');

  await expect(page.getByRole('region', { name: 'Atividades' })).toContainText(
    'Ligação: Retorno da proposta',
  );

  const linha = page.getByRole('region', { name: 'Linha do tempo' });
  await expect(linha).toContainText('Proposta enviada: versão 1, R$ 2.094.340,00');
  await expect(linha).toContainText('Venda aberta: Galpão de 1.200 m² em Jundiaí, em Primeiro contato');
  await expect(linha).toContainText('Venda perdida: Doca de Sumaré, por preço');
  await fotografar(page, 'ficha');
});

test('a previsão: o galpão ponderado no mês em que deve fechar, e a vencida à parte', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await entrar(page, '/previsao?meses=6');

  const grafico = page.getByRole('figure', { name: /Previsão de receita por mês/ });
  await expect(grafico.locator('svg g rect')).toHaveCount(18);
  await expect(
    grafico.getByRole('row', { name: new RegExp(`^${mesDe(diaDaqui(45)).replace('.', '\\.')}`) }),
  ).toContainText(/R\$\s1\.440\.000,00/);
  await expect(
    grafico.getByRole('row', { name: new RegExp(`^${mesDe(diaDaqui(0)).replace('.', '\\.')}`) }),
  ).toContainText(/R\$\s78\.500,00/);

  const fora = page.getByRole('region', { name: 'Fora da previsão' });
  await expect(fora).toContainText('Estacionamento');
  await expect(fora).toContainText('Guarita da entrada');
  await fotografar(page, 'previsao');
});
