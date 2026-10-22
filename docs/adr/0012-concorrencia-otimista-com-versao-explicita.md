# ADR-0012 — Concorrência otimista, com a versão conferida pelo banco num comando só

**Status:** aceita

## Contexto

Uma conta, uma venda ou uma proposta é mexida por mais de uma pessoa: o vendedor, o gerente que aprova o desconto, a assistente que atualiza o contato. Cada uma trabalha numa cópia lida minutos antes. Sem controle, quem grava por último apaga, sem saber, o que o outro gravou no meio: o desconto aprovado some, a etapa volta para trás.

Trancar o registro enquanto alguém edita não serve. A edição atravessa várias requisições HTTP (Parte 6), e a pessoa pode fechar o navegador com a tranca na mão.

## Decisão

**1. Cada linha tem uma versão inteira**, que começa em 1 e sobe a cada gravação. Atualizar e remover pedem a versão que foi lida; se o banco estiver em outra, a gravação é recusada com `ConflitoDeConcorrencia`: "A conta … mudou desde a versão 1: está na versão 2. Leia de novo e refaça a alteração." Uma coluna própria, e não a coluna de sistema `xmin` do PostgreSQL, que não diz nada a quem lê nem sobrevive a um backup restaurado. A API vai mandá-la como `ETag`.

**2. A versão fica fora do domínio.** O repositório devolve um `Versionado<T>` — o agregado e a versão lida —, e quem grava informa a versão que leu. Concorrência é problema de quem guarda e de quem edita pela rede, não regra da venda.

**3. Conferir e gravar são um comando só:** `update … where id = @id and versao = @lida`, e o mesmo para o `delete`. Se outra gravação passou no meio, nenhuma linha muda — e só então uma consulta separa "mudou" (`ConflitoDeConcorrencia`, com a versão atual) de "não existe" (`AgregadoNaoEncontrado`). Não há janela entre conferir e gravar: um teste dispara dez gravações ao mesmo tempo a partir da mesma versão, cada uma na sua conexão, e o banco deixa passar exatamente uma.

**4. O que o PostgreSQL responde vira exceção com nome — e mensagem para gente:**

| O banco responde | A persistência lança |
|---|---|
| nenhuma linha mudou, e a linha existe | `ConflitoDeConcorrencia`, com a versão atual |
| nenhuma linha mudou, e a linha não existe — ou é de outra construtora | `AgregadoNaoEncontrado` |
| chave primária repetida | `AgregadoJaExiste` |
| CNPJ ou CPF repetido na construtora | `AgregadoJaExiste`, com o documento |
| referência a agregado que não está gravado | `AgregadoNaoEncontrado`, dizendo qual |
| remoção de agregado que outro referencia | `AgregadoEmUso`, dizendo por quê |
| documento que não se lê (ADR-0011) | `DocumentoInvalido`, com o id e o motivo |

A restrição que ninguém previu não vira mensagem: é defeito, e sobe como tal.

**5. Nada fica no rastreador de mudanças do Entity Framework.** Lê-se sem rastreio, grava-se direto no banco; o contexto não guarda estado entre uma operação e outra. O relógio é injetado, e os instantes de gravação vão em UTC.

**6. Os repositórios respondem ao que o domínio pergunta**: a conta pelo documento; os leads de uma situação; as vendas de um funil, para o quadro e os relatórios; as vendas abertas de um vendedor e as atividades pendentes dele e das vendas dele, para a agenda; as vendas, atividades e propostas de uma conta, para a linha do tempo.

## Consequências

- **Nenhuma gravação apaga outra em silêncio.** O conflito chega a quem gravou por último, com a versão atual, e a decisão — ler de novo e refazer — fica com quem sabe o que mudou.
- **Edições em partes diferentes do mesmo agregado também conflitam**: o vendedor muda a previsão, o gerente aprova o desconto da proposta — se for a mesma venda, o segundo relê. É o preço de o agregado ser a unidade de consistência.
- **Um conflito custa uma consulta a mais**, só quando acontece; o caminho comum é um comando.
