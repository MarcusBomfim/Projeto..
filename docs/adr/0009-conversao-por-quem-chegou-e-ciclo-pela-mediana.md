# ADR-0009 — A conversão conta quem chegou a cada etapa, e o ciclo de venda é a mediana

**Status:** aceita

## Contexto

Duas perguntas seguem a previsão (ADR-0008): as probabilidades do funil estão certas? E quanto tempo leva para vender? As duas parecem simples, e as duas têm armadilhas.

Na conversão, a venda pode pular etapas (ADR-0004) e voltar a etapas anteriores (ADR-0005): "quantas passaram pela visita técnica" não conta a venda que foi direto para a proposta, e conta duas vezes a que voltou. E a venda aberta ainda não ganhou nem perdeu: contá-la como perdida derruba a taxa de qualquer mês recente.

No ciclo, uma obra pública que levou dois anos ao lado de dez reformas de um mês dá uma média de três meses — que não descreve nenhuma das onze.

## Decisão

**1. A conversão acompanha as vendas que nasceram no período** — uma coorte —, até onde cada uma chegou. O ciclo olha as vendas ganhas no período, pelo tempo que cada uma levou. O dia de cada venda é contado no fuso do período.

**2. Chegar a uma etapa é alcançá-la ou alcançar uma depois dela.** A venda que pulou a visita e foi direto para a proposta chegou à visita; a ganha chegou a todas; a que voltou conta pela etapa mais adiante que alcançou. A ordem é a do funil hoje, só com as etapas em uso.

**3. A taxa de ganho de uma etapa conta só as vendas que já fecharam:** das que chegaram lá e fecharam, quantas foram ganhas. É a probabilidade da etapa medida, e se compara com a configurada. Sem nenhuma fechada, não há taxa — e não zero.

**4. O tempo típico é a mediana.** Para o ciclo inteiro e para cada etapa. A média, o mais curto e o mais longo vêm junto: a distância entre a média e a mediana diz quanto os casos fora da curva pesam. O tempo numa etapa soma as passagens por ela; o tempo perdida, de uma venda que foi reaberta e depois ganha, conta no ciclo, mas não em nenhuma etapa.

## Consequências

- Os números são calculados das passagens das vendas pelas etapas, sem nenhum campo mantido à parte: o relatório de qualquer período passado pode ser refeito.
- A coorte de um mês recente ainda está em andamento: a taxa de ganho dela conta poucas vendas fechadas. O relatório mostra quantas estão em andamento, para quem lê saber o quanto confiar.
- Mudar a ordem do funil muda a conversão de períodos passados, porque "chegar" segue a ordem de hoje. É o que se quer quando a pergunta é sobre o funil de hoje.
