# ADR-0001 — Linguagem ubíqua em português

**Status:** aceita

## Contexto

O domínio deste sistema é a venda de obras no Brasil: a construtora recebe um interessado, descobre se ele tem verba e prazo, manda proposta e negocia até o contrato. Quem faz isso — o comercial, o orçamentista, o diretor — fala em *lead*, *qualificação*, *conta*, *funil*, *proposta*, *CNPJ*. Alguns termos vêm do inglês e ficaram assim no mercado brasileiro (*lead*, *pipeline*); outros não têm tradução que preste: CNPJ e CPF são do Brasil, e a regra dos dígitos verificadores também.

A convenção do ecossistema .NET é nomear tudo em inglês.

## Decisão

O código do domínio usa os termos do domínio, em português — ou como o comercial os fala: `Lead`, `Conta`, `Contato`, `Qualificacao`, `Cnpj`. O que é mecanismo do framework continua com o nome do framework — `IReadOnlyList`, `DateTimeOffset`, `ArgumentNullException`.

A regra de análise **CA1710**, que exige o sufixo `Exception` em exceções, foi desligada no `.editorconfig`: ela pressupõe nomes em inglês, e `ExcecaoDeDominio` já diz o que é.

## Consequências

- Uma regra de negócio lida em voz alta numa reunião do comercial é reconhecível no código.
- Quem não lê português terá mais dificuldade com o domínio. Para um sistema cujos usuários são brasileiros, e cujas regras — CNPJ, CPF, DDD — são brasileiras, é a troca certa.
- Mistura de idiomas na mesma linha (`Contatos.Where(...)`) é inevitável e aceita: se o termo vem do negócio, como o negócio fala; se vem da biblioteca, como ela nomeou.
- Acentos ficam fora dos identificadores (`Qualificacao`, não `Qualificação`): o C# os aceita, mas eles quebram busca, ferramenta de linha de comando e teclado de quem não está no padrão ABNT.
