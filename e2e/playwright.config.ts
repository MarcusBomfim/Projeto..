import { defineConfig, devices } from '@playwright/test';
import { ConfiguracaoDoJwt } from './apoio/identidade';
import { Api, Banco, Web } from './apoio/pilha';

/*
 * A pilha inteira, de verdade: o PostgreSQL (E2E_BANCO), a API compilada e
 * o front-end de produção servido pelo vite preview. Só o provedor de
 * identidade é dos testes — uma chave que a API aceita por configuração.
 *
 * O navegador é o Google Chrome instalado na máquina: nada a baixar, aqui ou
 * no CI, que já o traz.
 *
 * O planejamento-obras não está nesta pilha: a venda que vira obra só é
 * testada contra a do Compose (playwright.imagens.config.ts).
 */

/** O ambiente de quem roda os testes, e mais o que cada processo precisa. */
function ambiente(extras: Record<string, string>): Record<string, string> {
  const atual = Object.entries(process.env).filter(
    (entrada): entrada is [string, string] => entrada[1] !== undefined,
  );

  return { ...Object.fromEntries(atual), ...extras };
}

export default defineConfig({
  testDir: './testes',
  testIgnore: ['**/*.compose.spec.ts'],
  timeout: 90_000,
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: Web,
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'preparacao', testMatch: /preparacao\.setup\.ts/ },
    {
      name: 'chrome',
      use: { ...devices['Desktop Chrome'], channel: 'chrome' },
      dependencies: ['preparacao'],
    },
  ],
  webServer: [
    {
      name: 'API',
      // As migrações antes da API: o banco dos testes nasce vazio.
      command: `dotnet ef database update --no-build --project ../src/CrmObras.Infraestrutura --connection "${Banco}" && dotnet run --no-build --no-launch-profile --project ../src/CrmObras.Api`,
      url: `${Api}/openapi/v1.json`,
      env: ambiente({ ASPNETCORE_URLS: Api, ConnectionStrings__Crm: Banco, ...ConfiguracaoDoJwt }),
      timeout: 180_000,
    },
    {
      name: 'Front-end',
      command: 'npm --prefix ../web run preview -- --port 5191',
      url: Web,
      env: ambiente({ API_CRM: Api }),
      timeout: 60_000,
    },
  ],
});
