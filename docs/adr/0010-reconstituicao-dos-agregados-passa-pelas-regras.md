# ADR-0010 — A reconstituição dos agregados é interna, e passa pelas regras

**Status:** aceita

## Contexto

Até a Parte 4, os agregados viviam só em memória. Para gravar e ler de um banco (Parte 5), falta um caminho de volta: ler uma conta gravada é criar um objeto com uma identidade e um estado que já existiam. Os construtores públicos não servem — dão identidade nova —, e os métodos de negócio nem sempre podem ser repetidos: a venda entrou numa etapa que, desde então, passou a pedir um requisito; a atividade foi marcada numa venda que, depois, foi perdida.

E fica a pergunta: o que fazer com um dado gravado que viola uma regra — editado à mão no banco, gravado por uma versão com defeito?

## Decisão

**1. A reconstituição é interna**, visível só para a infraestrutura, que grava e lê, e para os testes do domínio. A API pública continua sendo a das regras de negócio: quem não é a persistência não tem como criar uma conta com o id de outra. Um teste de arquitetura confere a lista de quem enxerga os internos.

**2. Nada volta sem passar pelas regras** — as que valem sempre, e não as que valiam no momento da escrita:

| Agregado | Como volta |
|---|---|
| Conta | o nome e os contatos pela conferência de quem os adiciona — e-mail repetido é recusado —, com o principal entre eles |
| Lead | refazendo os passos — avaliar, qualificar, descartar —, pelas regras de sempre; o estado a que eles chegam tem de ser o gravado |
| Funil | as etapas abertas pelas regras de quem cria o funil; as arquivadas, pelas de cada etapa, sem repetir nome; as finais com nome, e nomes diferentes |
| Oportunidade | o título e o responsável pelas regras de sempre; as passagens encadeadas no tempo, os valores em centavos, a perda com o que o motivo pede, e a situação coerente com o que ela carrega |
| Atividade | os textos pelas regras de sempre; a situação coerente com o resultado, o motivo e o encerramento |
| Proposta | o conteúdo de cada versão pelas regras de quem redige; as versões numeradas em sequência, rascunho só na última, uma só com o cliente, a aceita no fim, e o envio e a resposta que a situação de cada uma pede |

O que já valia não é conferido de novo contra o mundo de hoje: a venda volta na etapa em que está, ainda que a etapa peça hoje o que ela não tem; a atividade volta ligada à venda fechada e ao contato que saiu da conta. Mudar a configuração não invalida o histórico.

**3. Dado gravado que fere uma regra é recusado na leitura**, com uma mensagem que diz o quê — e não descoberto depois, num relatório errado. A oportunidade diz todos os problemas de uma vez.

**4. O estado gravado tem forma de domínio.** Lead, oportunidade, atividade e versão da proposta expõem, só por dentro, o seu estado num record — o que se grava e de onde se refaz —, e o teste de ida e volta compara o estado antes e depois.

## Consequências

- **O domínio continua sem saber que existe banco.** Não há atributo de mapeamento, construtor sem parâmetros nem setter público. A persistência conversa com ele pela reconstituição e pelas propriedades de leitura.
- **`InternalsVisibleTo` é um acoplamento consciente.** A infraestrutura enxerga todos os internos, não só a reconstituição; a lista fechada, conferida por teste, limita a quem.
- **A identidade nasce no domínio**, e não no banco: um agregado tem id antes de ser gravado.
