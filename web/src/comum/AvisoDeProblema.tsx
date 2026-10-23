import type { Problema } from '../api/problemas';
import estilos from './AvisoDeProblema.module.css';

/** O problema como a API o explicou — título e detalhe, em português. */
export function AvisoDeProblema({ problema }: { readonly problema: Problema }) {
  return (
    <div className={estilos.aviso} role="alert">
      <p className={estilos.titulo}>{problema.titulo}</p>
      {problema.detalhe && <p className={estilos.detalhe}>{problema.detalhe}</p>}
    </div>
  );
}
