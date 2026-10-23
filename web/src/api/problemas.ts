/** O que deu errado, para mostrar a quem usa. */
export interface Problema {
  readonly titulo: string;
  readonly detalhe?: string | undefined;
  readonly status?: number | undefined;
}

function texto(objeto: object, chave: string): string | undefined {
  const valor: unknown = (objeto as Record<string, unknown>)[chave];

  return typeof valor === 'string' ? valor : undefined;
}

/**
 * Traduz o erro de uma consulta. A API responde em ProblemDetails (RFC
 * 9457), em português, com título e detalhe prontos para mostrar; o que não
 * chegou a ser resposta — a API fora do ar — vira um problema dito aqui.
 */
export function comoProblema(erro: unknown): Problema {
  if (typeof erro === 'object' && erro !== null) {
    const titulo = texto(erro, 'title');

    if (titulo !== undefined) {
      const status: unknown = (erro as Record<string, unknown>).status;

      return {
        titulo,
        detalhe: texto(erro, 'detail'),
        status: typeof status === 'number' ? status : undefined,
      };
    }
  }

  if (erro instanceof TypeError) {
    return {
      titulo: 'Não foi possível falar com a API.',
      detalhe: 'Confira se ela está no ar e tente de novo.',
    };
  }

  return {
    titulo: 'Algo deu errado.',
    detalhe: erro instanceof Error ? erro.message : undefined,
  };
}
