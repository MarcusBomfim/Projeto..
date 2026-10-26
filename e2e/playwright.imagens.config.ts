import { defineConfig } from '@playwright/test';
import padrao from './playwright.config';

/*
 * Os mesmos testes contra a pilha do Compose: as imagens que vão para
 * produção, com os papéis de produção no banco, o processador e o
 * planejamento-obras (ADR-0019). Quem sobe a pilha é o Compose, e E2E_WEB diz
 * onde ela está; aqui, só o navegador. É só aqui que a venda ganha vira obra.
 */
export default defineConfig({ ...padrao, webServer: [], testIgnore: [] });
