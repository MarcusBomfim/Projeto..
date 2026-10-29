import { Link, Navigate, NavLink, Outlet, useLocation } from 'react-router';
import { Icone } from '../comum/Icone';
import { Iniciais } from '../comum/Iniciais';
import { useSessao } from '../sessao/contexto';
import { entradasDoMenu } from './modulos';
import estilos from './Moldura.module.css';

/**
 * O que toda tela tem em volta, num menu à esquerda, como nos CRMs: a marca,
 * as áreas — cada uma com o ícone e a palavra — e, no pé, quem entrou. Sem
 * sessão, manda para a entrada — e volta para cá depois.
 */
export function Moldura() {
  const { sessao, sair } = useSessao();
  const local = useLocation();

  if (!sessao) {
    return <Navigate to={`/entrar?de=${encodeURIComponent(local.pathname + local.search)}`} replace />;
  }

  return (
    <div className={estilos.moldura}>
      <header className={estilos.lateral}>
        <Link to="/" className={estilos.marca}>
          <span className={estilos.simbolo}>
            <Icone nome="marca" />
          </span>
          CRM de Obras
        </Link>

        <nav aria-label="Principal" className={estilos.navegacao}>
          <ul className={estilos.menu}>
            {entradasDoMenu().map((entrada) => (
              <li key={entrada.caminho}>
                <NavLink to={entrada.caminho} end={entrada.caminho === '/'}>
                  {entrada.icone && <Icone nome={entrada.icone} />}
                  {entrada.rotulo}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className={estilos.sessao}>
          <Iniciais nome={sessao.nome} />
          <p className={estilos.quem}>
            <strong>{sessao.nome}</strong>
            {sessao.papeis.length > 0 && <span className={estilos.papeis}>{sessao.papeis.join(', ')}</span>}
          </p>
          <button
            type="button"
            className="secundario"
            onClick={() => {
              sair();
            }}
          >
            <Icone nome="sair" />
            Sair
          </button>
        </div>
      </header>

      <main className={estilos.conteudo}>
        <Outlet />
      </main>
    </div>
  );
}
