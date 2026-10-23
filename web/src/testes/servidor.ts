import { setupServer } from 'msw/node';

/** A API dos testes: cada teste diz o que ela responde, com servidor.use(...). */
export const servidor = setupServer();
