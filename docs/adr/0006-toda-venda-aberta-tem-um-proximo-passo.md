# ADR-0006 — Toda venda aberta tem um próximo passo, e a atividade feita deixa o resultado

**Status:** aceita

## Contexto

A venda de obra é feita de conversas: a ligação depois do orçamento, a visita ao terreno, a reunião com o financeiro. Quando o vendedor não marca a próxima, a venda esfria — o cliente não liga de volta, e o vendedor só percebe quando o concorrente já fechou. E quando ele faz a ligação e não anota o que ouviu, quem assume a carteira começa do zero.

O funil (ADR-0004) já diz quando a venda passou do tempo numa etapa. Mas parada é sintoma tardio: a venda pode estar na etapa há três dias e já sem ninguém cuidando dela.

## Decisão

**1. A atividade é da conta, e pode servir a uma venda.** Ligação, e-mail, reunião, visita ou tarefa, com assunto, data, responsável e, se houver, o contato com quem se vai falar — contato da conta. Só a venda aberta recebe atividade nova; depois de fechada, o que se marca é da conta.

**2. Concluir pede o resultado, e cancelar, o motivo.** A atividade encerrada não muda mais: o que aconteceu fica como foi anotado.

**3. Adiar conta.** Reagendar é para depois de agora, e cada vez que a atividade vai para mais tarde, o número de adiamentos sobe — a atividade adiada cinco vezes diz mais sobre a venda do que qualquer etapa. Antecipar não é adiar.

**4. A agenda do vendedor tem três listas:** as atrasadas — pendentes cuja hora passou, da mais antiga para a mais recente —, as do resto do dia, pela hora, e as vendas abertas dele sem próximo passo, isto é, sem nenhuma atividade pendente, de quem quer que seja. A atividade atrasada ainda é próximo passo: está marcada, e aparece nas atrasadas. A visita marcada pelo engenheiro também é: o próximo passo da venda não precisa ser do vendedor.

**5. O dia é o de quem olha a agenda**, no fuso do instante que ela recebe. A ligação marcada para 23h em Brasília é de amanhã para quem está em Lisboa.

## Consequências

- "Venda sem próximo passo" é calculada, e não um campo: não há o que manter em dia quando a atividade é concluída ou cancelada.
- A agenda recebe as atividades e as vendas que lhe passarem. Quando houver banco, quem chama busca as pendentes do vendedor e as das vendas dele, e não a base inteira.
- O sistema não impede a venda sem próximo passo — ela existe entre concluir uma atividade e marcar a seguinte. Ele mostra, e a cobrança é do gestor.
