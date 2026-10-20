# ADR-0003 — O lead se qualifica por critérios escritos, e só vira conta depois de procurar os duplicados

**Status:** aceita

## Contexto

Lead é quem demonstrou interesse e ainda não é cliente: preencheu o formulário do site, foi indicado, parou no estande da feira. A maioria não vira obra — falta verba, a obra é para daqui a três anos, a pessoa só queria saber o preço do metro quadrado. O comercial que trata todos igual gasta a semana com quem não vai comprar.

E o lead que vira cliente muitas vezes já é: a filial de uma empresa atendida, o arquiteto que indicou duas obras antes. Converter sem olhar cria a conta duplicada que o CRM existe para evitar (ADR-0002).

## Decisão

**1. Qualificar é responder quatro perguntas** — o BANT do comercial, nas palavras da obra: há orçamento (verba ou financiamento aprovado)? quem conversa é quem decide? a necessidade é real e definida? há prazo para começar? Cada uma é sim, não ou não se sabe.

**2. A régua é escrita no código:** qualifica quem tem a necessidade e o orçamento confirmados, e quem decida na conversa ou um prazo definido. Sem necessidade não há obra; sem orçamento não há venda; e um dos outros dois basta, porque o comercial chega a quem decide enquanto o prazo se define, e vice-versa. Quando não qualifica, o lead diz o que falta — tudo de uma vez.

**3. Descartar exige motivo**, de uma lista — sem orçamento, sem interesse, prazo distante, fora da região, escolheu o concorrente, não responde, duplicado — ou "outro", com observação. A lista é o que permite dizer, depois, onde o comercial perde. O descartado pode ser reaberto, com a avaliação que tinha.

**4. A busca de duplicados devolve indícios, e não veredito.** Para cada conta parecida, os indícios, do mais forte para o mais fraco: o mesmo documento, a mesma raiz de CNPJ (matriz e filial), o mesmo e-mail, o mesmo telefone, o mesmo nome — sem acento, maiúscula, pontuação e natureza jurídica — e o mesmo domínio de e-mail corporativo. Domínio de provedor gratuito não conta.

**5. Converter em conta nova exige a busca feita.** A lista de duplicados é parâmetro da conversão: não há como chamá-la sem ter procurado. Se a lista tem uma conta com o mesmo documento, a conversão em conta nova é recusada — o lead vira contato dela. Os outros indícios ficam com quem converte decidir.

**6. Converter numa conta existente** confere o documento — o do lead e o da conta têm de ser o mesmo, ou a conta não ter —, completa o da conta se faltava, e reaproveita o contato que já tem o e-mail do lead. Depois de convertido, o lead não muda: a história segue na conta.

## Consequências

- A régua da qualificação é uma decisão de negócio no código, e testada. Mudá-la — exigir o prazo, por exemplo — é uma linha, e o teste diz o que mudou.
- Duas empresas homônimas, ou um arquiteto que atende duas construtoras, aparecem como possíveis duplicados: é indício, e quem converte decide. Só o documento é certeza.
- A busca roda sobre as contas que lhe passarem. Quando houver banco, quem chama passa as contas candidatas — pelo documento, pelos e-mails, pelo nome —, e não a base inteira.
