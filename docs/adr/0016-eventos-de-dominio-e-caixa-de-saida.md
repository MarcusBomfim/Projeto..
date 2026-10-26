# ADR-0016 — Eventos de domínio, publicados numa caixa de saída na mesma transação

**Status:** aceita

## Contexto

Até a Parte 7, o que acontece com uma venda só interessa ao CRM. A Parte 8 traz quem precisa saber *que* algo aconteceu fora dele: a venda ganha é uma obra vendida, e a obra se planeja no planejamento-obras — o cronograma dela tem de nascer lá.

Há dois caminhos ruins. No primeiro, o pedido que ganha a venda chama o planejamento no meio da gravação: o vendedor espera outro sistema responder, e o planejamento fora do ar impede de ganhar uma venda. No segundo, o pedido grava a venda e depois avisa o planejamento: o processo cai entre um e outro, e a obra nunca chega; ou avisa e a gravação falha, e o planejamento abre uma obra que o CRM não vendeu. São dois lugares escritos sem transação entre eles.

## Decisão

**1. O agregado registra os fatos**, numa lista, na ordem em que acontecem. O nome é no passado — é fato, não pedido —, e um fato só existe se a regra deixou: o ganho recusado não registra nada. Hoje há um: `VendaGanha`, registrado por `Oportunidade.Ganhar`, que é o caminho da venda ganha direto e o do aceite da proposta. O resto da vida da venda — andar no funil, ser perdida, reaberta — não interessa a ninguém fora do CRM, e não vira fato. Evento só entra quando alguém precisa dele.

**2. O fato leva o que era verdade no instante dele**: a venda, o cliente, a obra como a venda se chamava, o valor fechado e o instante do ganho. Renomear a venda depois não muda o fato publicado. É o que permite a quem trata receber sempre a mesma mensagem, em quantas tentativas forem precisas — e mandar ao planejamento sempre o mesmo pedido (ADR-0017).

**3. O domínio não publica nada; só registra.** Quem publica é a persistência, junto com a gravação, e depois descarta os eventos publicados. Reconstituir a venda do banco não registra fato nenhum: aqueles já tinham sido publicados quando aconteceram.

**4. Caixa de saída, na mesma transação do agregado.** Cada fato vira uma mensagem na tabela `crm.caixa_de_saida`, gravada na transação que grava a venda: ou ficam as duas, ou nenhuma — num conflito de versão, nem uma nem outra. No aceite da proposta, que já grava a proposta e a venda numa transação, a mensagem entra na mesma. As mensagens de uma gravação entram uma a uma, na ordem dos fatos, e a sequência da caixa guarda essa ordem.

**5. Um catálogo traduz evento em mensagem**: um tipo (`venda-ganha`), um número de formato e o conteúdo em JSON, lido de volta com a mesma leitura estrita dos documentos — separado do evento como o documento é separado do agregado (ADR-0011). Mudar o formato publicado é mudar o número dele. Tipo ou formato que esta versão não conhece é recusado, e não adivinhado.

**6. A mensagem é da construtora da venda**, como o resto: a coluna `construtora_id`, o filtro no código e a segurança por linha forçada (ADR-0011). Outra construtora não a vê.

**7. Três funções atravessam construtoras, e cada uma faz uma coisa só.** A reserva da próxima mensagem devolve só o id e a construtora dela, e a trava com `for update skip locked` até o fim da transação de quem chamou: dois processadores nunca pegam a mesma. As duas limpezas só apagam o que é anterior a uma data — as mensagens tratadas e as chaves de idempotência (ADR-0013). Rodam como o dono das tabelas, com o `search_path` fixo, e só o papel do processador as executa: a migração revoga o acesso de `PUBLIC`, e a API, que está de frente para a internet, não alcança nenhuma.

**8. A reserva assume a construtora da mensagem**, no código e na conexão — só até o fim da transação. Dali em diante, o conteúdo da mensagem, o que o tratamento lê e o que ele grava passam pelo filtro e pela segurança por linha como num pedido.

**9. A falha conta, espera e, na décima, desiste.** A mensagem que falha guarda a tentativa, o erro e a hora de tentar de novo: 30 segundos, o dobro a cada falha, no máximo uma hora. Na décima, a fila desiste dela, e ela fica abandonada para alguém ver por quê — somadas as esperas, são cerca de três horas e meia de tentativas.

**10. Mensagem nova avisa.** Um gatilho na caixa chama `pg_notify` no canal `crm_caixa_de_saida`, que o PostgreSQL só entrega quando a transação é confirmada: a mensagem desfeita não acorda ninguém.

**11. A publicação é um passo no rastro de quem gravou.** A mensagem guarda o contexto do rastro dela, no formato `traceparent` do W3C, para o tratamento — em outro processo, talvez minutos depois — continuar o mesmo rastro. Sem ninguém rastreando, vai sem.

## Consequências

- **Nenhum fato se perde entre a gravação e a publicação, e nenhum é publicado sem ter sido gravado.**
- **Ganhar uma venda não espera o planejamento**, nem depende de ele estar no ar.
- **Um interessado novo não mexe no domínio nem na API**: trata as mensagens da caixa.
- **Os eventos vivem na memória até a gravação.** Se ela falhar, o agregado é descartado com eles.
- **Quem implanta cria mais um papel no banco**, o do processador, e concede a ele a execução das três funções. Os testes criam os dois papéis do mesmo jeito.
- **A caixa cresce** até a limpeza das tratadas; a abandonada fica até alguém resolver a causa.
