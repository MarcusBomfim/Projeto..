# ADR-0017 — A venda ganha vira obra no planejamento-obras, por um processador em segundo plano

**Status:** aceita

## Contexto

A venda ganha fica registrada na caixa de saída, na mesma transação da venda (ADR-0016). Falta levá-la ao planejamento-obras, onde a obra vendida ganha cronograma, orçamento e linha de base. O planejamento é outro sistema, com API própria (a ADR-0015 dele), autenticação própria e o seu próprio tempo de manutenção: pode estar fora do ar, lento, ou recusar o pedido.

E há trabalho que não é de pedido nenhum: a ADR-0013 deixou para esta parte a limpeza das chaves de idempotência antigas.

## Decisão

**1. Um processador em host próprio.** O `CrmObras.Processador` roda fora da API: cai, reinicia e escala sem ela. Ele reserva uma mensagem por transação (ADR-0016), entrega aos tratadores do tipo dela — em nome da construtora dela — e marca a mensagem como tratada na mesma transação. Dois processadores dividem a fila sem tratar nada duas vezes.

**2. O que o tratador grava no CRM e a marca de tratada são confirmados juntos.** Se um tratador falha, o que ele gravou é desfeito até um ponto de salvamento anterior ao tratamento, e a falha fica na mensagem, com a espera crescente da ADR-0016. Só o host parando interrompe o tratamento: um tempo esgotado dentro de um tratador — o planejamento que não respondeu — é falha dele.

**3. Fora do CRM, a entrega é ao menos uma vez.** O processador pode cair depois de o planejamento abrir o cronograma e antes de confirmar a mensagem: ela volta, e o pedido vai de novo. Todo tratador tem de dar o mesmo resultado repetido — e fora de ordem, porque a mensagem que falhou volta depois da seguinte.

**4. O tratador da venda ganha abre o cronograma da obra no planejamento**, com um `POST /api/cronogramas` do contrato dele: o nome é a obra, como a venda se chamava quando foi ganha. O título da venda vai até 200 caracteres e o nome do cronograma, até 160: o que passa é cortado, com reticências. O cronograma nasce vazio — atividades e vínculos são trabalho de quem planeja, lá.

**5. A repetição não abre outra obra.** O pedido leva a chave de idempotência do contrato do planejamento, feita da venda: `crm-obras:venda-ganha:{venda}`. Repetido, o planejamento devolve o cronograma que já abriu, e diz que é repetição. A chave é a venda, e não a mensagem, porque é a regra: venda ganha não se reabre, e uma venda vira uma obra. E o corpo é sempre o mesmo, porque sai do fato (ADR-0016) — o planejamento recusa a chave usada com outro pedido. Ele guarda as chaves por 24 horas; as tentativas de uma mensagem duram umas três horas e meia.

**6. O processador se apresenta com um token de serviço que ele mesmo assina.** Um JWT HS256, com a chave, o emissor e a audiência da seção `Planejamento` da configuração: `sub` `crm-obras`, a construtora da mensagem na reivindicação `empresa` — como o planejamento a chama —, e só o papel `planejador`, que abre cronogramas e não aprova linha de base nem mede obra. Um token por pedido, válido por cinco minutos. A construtora tem o mesmo id nos dois sistemas: o do provedor de identidade dela.

**7. Configuração que não serve impede o processador de subir.** Endereço absoluto, emissor, audiência e uma chave Base64 de 256 bits ou mais — conferidos na subida, com o nome de cada configuração que falta. Melhor do que descobrir na primeira venda ganha.

**8. A recusa do planejamento fica na mensagem com o que ele disse**: o status e o `detail` do ProblemDetails — "O planejamento-obras respondeu 503 ao abrir o cronograma: o banco está em manutenção" —, ou o começo do corpo, se não for um. O tempo-limite do pedido é de 30 segundos.

**9. O processador acorda com a mensagem nova.** Ele escuta o canal do `NOTIFY` (ADR-0016) numa conexão própria, fora do pool, e a cada 30 segundos olha a fila mesmo sem aviso: é quando voltam as mensagens que falharam, e é a rede para o aviso que se perdeu. Sem a conexão de escuta, segue só com o intervalo, e a refaz na volta seguinte.

**10. Rotinas de limpeza**, de hora em hora: chaves de idempotência com mais de 24 horas, como a ADR-0013 prometia, e mensagens tratadas há mais de 7 dias. A abandonada fica. Todo processador roda todas as rotinas, então cada uma pode rodar duas vezes ao mesmo tempo — apagar o que já foi apagado não apaga mais nada.

## Consequências

- **Ganhar uma venda não depende do planejamento.** Fora do ar, ele recebe a obra quando voltar — em segundos, minutos ou, no pior caso, umas três horas e meia depois, antes de a fila desistir.
- **O CRM não guarda o cronograma que a venda virou.** A integração é de ida: o log do processador e o rastro dizem qual cronograma cada venda abriu. Trazer o cronograma de volta para a ficha da venda é uma mensagem no sentido contrário, que o planejamento ainda não publica.
- **O planejamento confia no CRM como emissor**, por configuração: a chave do token de serviço é segredo dos dois, e quem a tem abre cronograma em nome de qualquer construtora. Com um provedor de identidade de verdade, o lugar disso é um *client credentials* dele, e o processador só troca a origem do token.
- **Mensagem abandonada pede gente.** A desistência é registrada como erro no log, e a mensagem fica na caixa, com o último erro, até alguém tratar a causa.
- **Uma mensagem por transação** custa algumas idas ao banco por mensagem — de sobra para o ritmo em que uma construtora ganha vendas.
