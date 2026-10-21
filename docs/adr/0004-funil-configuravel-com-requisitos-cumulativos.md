# ADR-0004 — O funil é configurável, a probabilidade não cai e os requisitos se acumulam

**Status:** aceita

## Contexto

O funil é o caminho da venda: do primeiro contato à obra vendida, passando pela visita técnica, pelo orçamento e pela proposta. Cada construtora tem o seu — e a mesma construtora vende obra pública por edital, com habilitação e recurso, e reforma de escritório por indicação, com duas conversas e um aperto de mão. Um funil fixo no código serve a uma delas e atrapalha as outras.

Mas funil configurável sem regra vira bagunça: etapa de 80% antes de uma de 20%, a venda que pula da primeira conversa para a negociação sem valor nem data, a etapa apagada com cinquenta vendas que passaram por ela. E é do funil que sai a previsão de vendas (parte 4): se a configuração não tem regra, a previsão não tem sentido.

## Decisão

**1. As etapas abertas são configuráveis**, por funil: o nome, a probabilidade de fechar, os dias que a venda costuma ficar nela — passado isso, está parada — e os requisitos para entrar. Há um funil padrão de construtora, para começar: primeiro contato (10%), visita técnica (20%), orçamento em elaboração (40%), proposta enviada (60%) e negociação (80%).

**2. A probabilidade vai de 1% a 99% e não cai conforme a venda avança.** Etapas seguidas podem ter a mesma probabilidade; a seguinte com menos é recusada — ao criar, alterar ou mover. Trocar duas etapas de lugar é igualar a probabilidade, mover, e ajustar: cada passo deixa o funil válido. A etapa nova entra no lugar que a probabilidade dela indica.

**3. A Ganha (100%) e a Perdida (0%) existem sempre**, no fim. Podem mudar de nome — "contrato assinado", "não fechou" —, e só: não se movem, não se arquivam, não têm prazo nem requisito. Ganhar pede o valor fechado; perder, o motivo.

**4. Os requisitos se acumulam.** São três: o valor estimado, a previsão de fechamento e quem decide, entre os contatos do cliente. Para entrar numa etapa, a oportunidade precisa ter os requisitos dela e os de todas as abertas antes. Pular etapa não pula a exigência: a venda que vai do primeiro contato direto para a proposta precisa de tudo o que a proposta e o orçamento pedem. A primeira etapa não tem requisito — é nela que a oportunidade nasce, sem nada —, e a regra vale ao criar o funil, e ao adicionar, alterar, mover, arquivar e reativar etapas: nenhuma dessas mudanças põe em primeiro uma etapa com requisito.

**5. Etapa que deixa de ser usada é arquivada, e não apagada.** Sai do funil — nenhuma oportunidade entra nela, e os requisitos dela deixam de contar —, mas a história das vendas que passaram por ela continua apontando para ela. O nome dela continua ocupado. Reativada, volta no lugar que a probabilidade indica. A última etapa aberta não se arquiva: é nela que a venda começa.

## Consequências

- A previsão de vendas pode multiplicar o valor pela probabilidade da etapa sem conferir a configuração: a regra está no funil.
- Os requisitos são uma lista fechada, no código. Requisito novo — "visita feita", "projeto recebido" — é mudança de código, e não de configuração. Vale enquanto forem poucos e comuns a toda construtora; se cada uma quiser os seus, os requisitos viram campos configuráveis, e a conferência, uma regra por campo.
- O nome é único sem contar maiúsculas, as arquivadas inclusive: "Visita técnica" e "visita técnica" seriam duas colunas iguais no quadro.
