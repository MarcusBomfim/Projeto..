# ADR-0008 — A previsão de receita é ponderada pela etapa, e não chuta

**Status:** aceita

## Contexto

A pergunta que a diretoria faz toda segunda é quanto a construtora vai vender nos próximos meses. Somar o valor de todas as vendas abertas dá um número que nunca acontece: metade delas vai ser perdida. Perguntar a cada vendedor dá o otimismo de cada um.

E a previsão herda os vícios do cadastro: a venda sem valor, a sem data, a que devia ter fechado em agosto e continua aberta com a data de agosto. Somadas a algum mês, elas viram um número que ninguém sabe explicar.

## Decisão

**1. A venda aberta entra no mês da previsão de fechamento, pelo valor estimado vezes a probabilidade da etapa em que está** — a do funil (ADR-0004), que não cai conforme a venda avança. A venda numa etapa arquivada conta pela probabilidade que a etapa tinha.

**2. A ganha entra no mês em que foi ganha, pelo valor fechado.** O mês é contado no fuso de quem pede a previsão: a venda ganha às 22h de 31/10 em Brasília é de outubro para quem está em Brasília. A perdida não entra.

**3. O previsto do mês é o ganho mais o ponderado.** O mês corrente mostra o que já foi ganho e o que ainda deve fechar; o mês passado, só o ganho — nenhuma previsão aponta para trás.

**4. A previsão não chuta.** A venda aberta sem valor estimado, a com valor e sem data, e a de previsão vencida — data antes de hoje — ficam fora dos meses, cada uma numa lista, para quem cuida dela corrigir. A vencida não é empurrada para o mês corrente: quem sabe a data nova é o vendedor.

**5. O ponderado é arredondado ao centavo uma vez por mês**, sobre a soma das vendas do mês, e não venda a venda: arredondar cada parcela antes de somar acumula o erro.

## Consequências

- A previsão é tão boa quanto as probabilidades do funil. Elas são configuração, e o que diz se estão certas é olhar, etapa por etapa, quantas das vendas que chegaram lá foram ganhas.
- As listas do que ficou fora fazem do relatório também uma cobrança: a previsão melhora quando elas esvaziam.
- O relatório recebe as vendas que lhe passarem. A previsão de um vendedor é a previsão das vendas dele; a da construtora, a de todas.
