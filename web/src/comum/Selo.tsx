import estilos from './Selo.module.css';

export type TomDoSelo = 'sucesso' | 'atencao' | 'perigo' | 'neutro' | 'destaque';

/** Uma situação escrita, com a cor dela — a cor nunca sozinha. */
export function Selo({ tom, children }: { readonly tom: TomDoSelo; readonly children: string }) {
  return <span className={`${estilos.selo ?? ''} ${estilos[tom] ?? ''}`}>{children}</span>;
}
