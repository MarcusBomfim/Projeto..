import type { CSSProperties } from 'react';
import estilos from './Iniciais.module.css';
import { iniciais, matiz } from './nomes';

/**
 * As iniciais de uma pessoa ou de uma empresa, num círculo com a cor do
 * nome — como nos CRMs, para achar alguém de relance. Enfeite: o nome está
 * escrito ao lado, e o leitor de tela lê só ele. As letras vêm do CSS, para
 * não entrarem no texto da página.
 */
export function Iniciais({
  nome,
  tamanho = 'medio',
}: {
  readonly nome: string;
  readonly tamanho?: 'pequeno' | 'medio' | 'grande';
}) {
  return (
    <span
      className={`${estilos.iniciais ?? ''} ${estilos[tamanho] ?? ''}`}
      style={{ '--matiz': matiz(nome) } as CSSProperties}
      data-iniciais={iniciais(nome)}
      aria-hidden="true"
    />
  );
}
