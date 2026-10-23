import estilos from './Carregando.module.css';

export function Carregando({ rotulo }: { readonly rotulo: string }) {
  return (
    <p className={estilos.carregando} role="status">
      {rotulo}…
    </p>
  );
}
