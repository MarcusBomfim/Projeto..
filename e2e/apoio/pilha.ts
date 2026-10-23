/*
 * Onde a pilha dos testes de ponta a ponta roda. Portas próprias — 5091 e
 * 5191 —, para não esbarrar na API e no front-end de desenvolvimento.
 */

export const Api = 'http://localhost:5091';

export const Web = 'http://localhost:5191';

/** O banco dos testes: o do CI, ou um PostgreSQL local na porta 55434. */
export const Banco =
  process.env.E2E_BANCO ??
  'Host=localhost;Port=55434;Database=crm_obras_e2e;Username=postgres;Password=postgres';
