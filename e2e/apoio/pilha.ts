/*
 * Onde a pilha dos testes de ponta a ponta roda. Por padrão, a que o próprio
 * Playwright sobe, em portas próprias — 5091 e 5191 —, para não esbarrar na
 * API e no front-end de desenvolvimento.
 *
 * Contra a pilha do Compose (ADR-0019), E2E_WEB diz onde ela está, e a API
 * vem pela mesma origem, pelo nginx, como em produção. Só lá há o
 * planejamento-obras, onde a venda ganha vira obra: E2E_PLANEJAMENTO diz
 * onde está a API dele.
 */

const Externa = process.env.E2E_WEB;

export const Web = Externa ?? 'http://localhost:5191';

export const Api = Externa ?? 'http://localhost:5091';

export const Planejamento = process.env.E2E_PLANEJAMENTO ?? 'http://localhost:5281';

/** O painel do OpenTelemetry da pilha do Compose, onde o README fotografa o rastro da venda que vira obra. */
export const Painel = process.env.E2E_PAINEL ?? 'http://localhost:18888';

/** O banco dos testes: o do CI, ou um PostgreSQL local na porta 55434. */
export const Banco =
  process.env.E2E_BANCO ??
  'Host=localhost;Port=55434;Database=crm_obras_e2e;Username=postgres;Password=postgres';
