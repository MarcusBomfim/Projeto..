import { Link } from 'react-router';

export function NaoEncontrada() {
  return (
    <section>
      <h1>Página não encontrada</h1>
      <p>
        O endereço não leva a nenhuma tela. <Link to="/">Voltar ao funil</Link>.
      </p>
    </section>
  );
}
