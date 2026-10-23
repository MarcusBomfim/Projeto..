import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** O que a preparação criou pela API, para os testes encontrarem na tela. */
export interface CarteiraCriada {
  readonly construtora: string;
  /** O token do Carlos, vendedor. */
  readonly token: string;
  readonly funil: string;
  readonly filial: string;
  readonly galpao: string;
}

const Arquivo = join(dirname(fileURLToPath(import.meta.url)), '..', '.estado', 'carteira.json');

export function guardarCarteira(carteira: CarteiraCriada): void {
  mkdirSync(dirname(Arquivo), { recursive: true });
  writeFileSync(Arquivo, JSON.stringify(carteira, null, 2));
}

export function lerCarteira(): CarteiraCriada {
  return JSON.parse(readFileSync(Arquivo, 'utf8')) as CarteiraCriada;
}
