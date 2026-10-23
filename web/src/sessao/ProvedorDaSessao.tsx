import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { avisarQuandoRecusarToken } from '../api/cliente';
import { ContextoDaSessao, type EstadoDaSessao } from './contexto';
import { esquecerToken, guardarToken, lerSessao, lerToken, type Sessao } from './token';

/** O maior intervalo que o setTimeout aceita; acima dele, o navegador dispara na hora. */
const MaiorEspera = 2_147_483_647;

function sessaoGuardada(): Sessao | null {
  const token = lerToken();

  if (!token) {
    return null;
  }

  const leitura = lerSessao(token);

  if ('motivo' in leitura) {
    esquecerToken();

    return null;
  }

  return leitura.sessao;
}

/**
 * Quem entrou, e até quando. Trocar de sessão — entrar, sair, o token
 * vencer ou ser recusado pela API — esvazia o cache das consultas: o que
 * uma construtora leu não aparece para a próxima.
 */
export function ProvedorDaSessao({ children }: { readonly children: ReactNode }) {
  const consultas = useQueryClient();
  const [sessao, definirSessao] = useState<Sessao | null>(sessaoGuardada);
  const [aviso, definirAviso] = useState<string | null>(null);

  const sair = useCallback(
    (motivo?: string) => {
      esquecerToken();
      consultas.clear();
      definirSessao(null);
      definirAviso(motivo ?? null);
    },
    [consultas],
  );

  const entrar = useCallback(
    (token: string) => {
      const leitura = lerSessao(token);

      if ('motivo' in leitura) {
        return leitura.motivo;
      }

      guardarToken(leitura.sessao.token);
      consultas.clear();
      definirSessao(leitura.sessao);
      definirAviso(null);

      return null;
    },
    [consultas],
  );

  useEffect(
    () =>
      avisarQuandoRecusarToken(() => {
        sair('A API recusou o token: ele venceu ou não vale mais. Entre de novo.');
      }),
    [sair],
  );

  useEffect(() => {
    const faltam = sessao?.venceEm ? sessao.venceEm.getTime() - Date.now() : undefined;

    if (faltam === undefined || faltam > MaiorEspera) {
      return undefined;
    }

    const temporizador = setTimeout(
      () => {
        sair('O token venceu. Entre de novo.');
      },
      Math.max(faltam, 0),
    );

    return () => {
      clearTimeout(temporizador);
    };
  }, [sessao, sair]);

  const estado = useMemo<EstadoDaSessao>(
    () => ({ sessao, aviso, entrar, sair }),
    [sessao, aviso, entrar, sair],
  );

  return <ContextoDaSessao value={estado}>{children}</ContextoDaSessao>;
}
