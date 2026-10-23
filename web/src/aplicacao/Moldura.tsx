import { Link, Navigate, NavLink, Outlet, useLocation } from 'react-router';
import { useSessao } from '../sessao/contexto';
import { entradasDoMenu } from './modulos';
import estilos from './Moldura.module.css';

/**
 * O que toda tela tem em volta: a marca, o menu das áreas e quem entrou. Sem
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
      <header className={estilos.topo}>
        <Link to="/" className={estilos.marca}>
          CRM de Obras
        </Link>

        <nav aria-label="Principal">
          <ul className={estilos.menu}>
            {entradasDoMenu().map((entrada) => (
              <li key={entrada.caminho}>
                <NavLink to={entrada.caminho} end={entrada.caminho === '/'}>
                  {entrada.rotulo}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className={estilos.sessao}>
          <span>
            <strong>{sessao.nome}</strong>
            {sessao.papeis.length > 0 && (
              <span className={estilos.papeis}> · {sessao.papeis.join(', ')}</span>
            )}
          </span>
          <button
            type="button"
            className="secundario"
            onClick={() => {
              sair();
            }}
          >
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
