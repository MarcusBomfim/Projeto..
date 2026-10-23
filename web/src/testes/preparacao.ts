import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { servidor } from './servidor';

// Pedido que nenhum teste previu é erro: a tela pediu o que não devia.
beforeAll(() => {
  servidor.listen({ onUnhandledFrame: 'error' });
});

afterEach(() => {
  cleanup();
  servidor.resetHandlers();
  sessionStorage.clear();
});

afterAll(() => {
  servidor.close();
});
