import { useQueryClient } from '@tanstack/react-query';
import { api, type Esquemas } from '../api/cliente';
import { comoProblema, type Problema } from '../api/problemas';

type Cartao = Esquemas['CartaoResposta'];

export interface MovimentoDaVenda {
  /** Pede à API para levar a venda à etapa, a partir da versão que o quadro mostrou. */
  readonly mover: (cartao: Cartao, etapa: string) => void;
  /** A venda que está indo, enquanto a API não responde. */
  readonly movendo: string | undefined;
  /** A venda que acabou de ir, e para onde. */
  readonly movida: Esquemas['OportunidadeResposta'] | undefined;
  /** Por que a última não foi — com a mensagem do domínio. */
  readonly problema: Problema | null;
}

/**
 * Mover o cartão é pedir à API, com a versão lida no If-Match (ADR-0014): a
 * tela não decide se a venda tem o que a etapa pede — quem decide é o
 * domínio, e o que ele recusar volta com a explicação dele. Dando certo ou
 * não, o quadro é lido de novo: a versão mudou, ou outra pessoa mexeu antes.
 */
export function useMoverVenda(): MovimentoDaVenda {
  const consultas = useQueryClient();
  const movimento = api.useMutation('put', '/api/oportunidades/{id}/etapa', {
    onSettled: () => consultas.invalidateQueries({ queryKey: ['get', '/api/funis/{id}/quadro'] }),
  });

  return {
    mover: (cartao, etapa) => {
      movimento.mutate({
        params: { path: { id: cartao.id }, header: { 'If-Match': `"${cartao.versao}"` } },
        body: { etapa },
      });
    },
    movendo: movimento.isPending ? movimento.variables.params.path.id : undefined,
    movida: movimento.data,
    problema: movimento.isError ? comoProblema(movimento.error) : null,
  };
}
