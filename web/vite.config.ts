import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

/*
 * Em desenvolvimento, o Vite repassa /api para a API: o navegador fala com
 * uma origem só, e a API não precisa de CORS. A porta da API vem do perfil
 * http dela (launchSettings.json); outra, pela variável API_CRM.
 */
const api = process.env.API_CRM ?? 'http://localhost:5090';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5190,
    strictPort: true,
    proxy: { '/api': api },
  },
  preview: {
    port: 5190,
    strictPort: true,
    proxy: { '/api': api },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/testes/preparacao.ts'],
    restoreMocks: true,
    // No fuso do Brasil em qualquer máquina, inclusive no CI, que roda em
    // UTC: é nele que uma data lida como meia-noite de UTC vira a véspera.
    env: { TZ: 'America/Sao_Paulo' },
    // O cache de módulos em disco guarda a expansão do import.meta.glob pelo
    // conteúdo do arquivo que o usa: uma área nova da aplicação não entraria
    // nos testes até alguém apagar o cache. Mais lento, e sempre certo.
    fsModuleCache: false,
  },
});
