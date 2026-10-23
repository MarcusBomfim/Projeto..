import { useState } from 'react';
import { Link } from 'react-router';
import { api } from '../api/cliente';
import { comoProblema } from '../api/problemas';
import { AvisoDeProblema } from '../comum/AvisoDeProblema';
import { Carregando } from '../comum/Carregando';
import estilos from './TelaDasContas.module.css';

/** O nome sem acento e sem diferença de maiúscula: "logistica" acha "Logística". */
function comparavel(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLocaleLowerCase('pt-BR');
}

/** Os clientes da construtora, em ordem de nome, com a busca pelo nome. */
export function TelaDasContas() {
  const contas = api.useQuery('get', '/api/contas');
  const [busca, definirBusca] = useState('');

  const achadas = contas.data?.filter((conta) => comparavel(conta.nome).includes(comparavel(busca.trim())));

  return (
    <section aria-labelledby="titulo-das-contas">
      <h1 id="titulo-das-contas">Clientes</h1>
      <p className="subtitulo">Cada conta é um cliente: a empresa ou a pessoa que compra a obra.</p>

      <label className={estilos.busca}>
        Procurar pelo nome
        <input
          type="search"
          value={busca}
          onChange={(evento) => {
            definirBusca(evento.target.value);
          }}
        />
      </label>

      {contas.isPending && <Carregando rotulo="Carregando os clientes" />}
      {contas.isError && <AvisoDeProblema problema={comoProblema(contas.error)} />}
      {achadas &&
        (achadas.length === 0 ? (
          <p className={estilos.vazio}>
            {busca.trim() === '' ? 'Nenhum cliente ainda.' : `Nenhum cliente com "${busca.trim()}" no nome.`}
          </p>
        ) : (
          <ul className={estilos.contas}>
            {achadas.map((conta) => (
              <li key={conta.id}>
                <Link to={`/contas/${conta.id}`}>{conta.nome}</Link>
              </li>
            ))}
          </ul>
        ))}
    </section>
  );
}
