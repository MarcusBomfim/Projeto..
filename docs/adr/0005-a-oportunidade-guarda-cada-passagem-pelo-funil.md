# ADR-0005 — A oportunidade guarda cada passagem pelo funil, e a venda ganha não se reabre

**Status:** aceita

## Contexto

A oportunidade é a venda em andamento: uma obra que um cliente pode fechar. Ela anda pelo funil (ADR-0004) até ser ganha ou perdida. Duas perguntas que o comercial faz toda semana dependem de como esse andar fica registrado: onde as vendas emperram — quanto tempo passam no orçamento, na negociação — e quais estão paradas agora.

Guardar só a etapa atual e a data da última mudança responde à segunda pergunta, e não à primeira. E a venda que volta — da proposta para o orçamento, porque o cliente mudou o escopo — apagaria o tempo que já passou lá.

## Decisão

**1. Cada passagem por uma etapa fica guardada**, com a entrada e a saída. A última é a atual, ainda sem saída. Voltar a uma etapa é uma passagem nova, e o tempo nela se soma ao da anterior. Os instantes seguem a ordem do tempo: a venda não sai de uma etapa antes de ter entrado nela.

**2. Parada é a venda aberta que está na etapa há mais dias do que a etapa espera.** O prazo é o da etapa hoje: se a construtora muda o prazo, as vendas que estão lá passam a ser medidas pelo novo.

**3. A oportunidade anda para a frente e para trás**, e entrar em qualquer etapa confere os requisitos acumulados dela, de uma vez: o que falta, todo junto. Etapa arquivada não recebe venda — mas a venda que está nela sai normalmente.

**4. Ganhar e perder têm caminho próprio.** Ganhar pede o valor fechado, positivo e em centavos. Perder pede o motivo, de uma lista — preço, prazo, concorrente, projeto cancelado, sem crédito, sem resposta —, com o nome do concorrente quando o motivo é ele, e a observação quando é "outro". Nenhuma das duas passa pelos requisitos das etapas abertas: a venda fechada não precisa de previsão.

**5. A venda ganha não muda mais, nem se reabre:** virou obra, e desfazer é cancelar o contrato — outra história, que não cabe na oportunidade. A perdida pode ser reaberta numa etapa aberta, conferida como qualquer entrada; a perda é esquecida, mas a passagem pela Perdida fica no histórico. Fechada a venda, o relógio para: o tempo na Ganha ou na Perdida só conta se ela for reaberta.

**6. Mudar o vendedor e o título vale sempre**, até na venda fechada: quem sai da empresa passa a carteira inteira, e o cliente da venda ganha continua tendo com quem falar.

## Consequências

- O tempo em cada etapa sai do histórico, sem campo calculado para manter em dia. A previsão e os relatórios (parte 4) leem as passagens.
- A venda que veio de um lead guarda o id dele: é o que vai dizer, nos relatórios, que canal traz venda — e não só contato.
- O histórico cresce com cada mudança de etapa. Uma venda que vai e volta cem vezes tem cem passagens — o que, numa venda de obra, é sinal de outro problema.
- A oportunidade guarda o id do contato que decide, e não o contato: remover o contato da conta não mexe na oportunidade. Quando houver banco, quem remove confere se o contato decide alguma venda aberta.
