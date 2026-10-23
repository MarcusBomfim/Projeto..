# ADR-0013 — Criação com chave de idempotência, gravada na mesma transação

**Status:** aceita

## Contexto

Na rede, um pedido pode chegar ao servidor, criar o que pediu — e a resposta se perder no caminho de volta. O cliente não sabe se deu certo, e tenta de novo. O vendedor no celular, na obra, com o sinal indo e voltando, tenta de novo três vezes.

Para alterar o que já existe, a versão resolve: a repetição leva a versão antiga e é recusada (ADR-0012). Para criar, não há versão: o id nasce no servidor, e a repetição criaria o mesmo lead três vezes — e, adiante, a busca de duplicados apontaria o lead para ele mesmo.

## Decisão

**1. Todo pedido que cria leva o cabeçalho `Idempotency-Key`**: um valor escolhido pelo cliente — um UUID —, um por operação, repetido em cada nova tentativa da mesma operação.

**2. A chave fica gravada no banco, por construtora**, com a impressão do pedido — método, caminho e corpo — e a resposta dada: o status, os cabeçalhos que fazem parte do resultado (`Location`, `ETag`) e o corpo. A tabela tem a segurança por linha das outras (ADR-0011).

**3. Reserva e criação na mesma transação.** A chave é reservada antes da operação (`insert … on conflict do nothing`), na transação que vai gravar o que se cria, e a resposta é guardada antes da confirmação. Ou ficam as duas coisas, ou nenhuma. Operação que falha desfaz a reserva, e o cliente pode tentar de novo com a mesma chave; só a resposta de sucesso fica guardada.

**4. O banco põe os repetidos em fila.** O pedido repetido enquanto o primeiro ainda roda bate na chave reservada e espera — quem segura é a chave primária do PostgreSQL — até o primeiro terminar; então recebe a resposta dele.

**5. Mesma chave, pedido diferente: recusado**, sem rodar nada e sem devolver a resposta guardada para um pedido que não é o dela.

**6. A repetição devolve a resposta original, igual**, mesmo que o criado tenha mudado depois: é a resposta *daquele* pedido.

## Consequências

- **Criar é seguro de repetir.** O cliente pode tentar até receber resposta, sem duplicar lead, conta ou proposta.
- **A chave vale por construtora**: duas podem gerar o mesmo valor sem se esbarrar.
- **As chaves se acumulam.** A rotina que apaga as antigas entra com o processamento em segundo plano (parte 8).
- **Alterar não usa chave**: a versão já impede a dupla aplicação.
