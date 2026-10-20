# CRM de Obras

[![CI](https://github.com/MarcusBomfim/Projeto../actions/workflows/ci.yml/badge.svg)](https://github.com/MarcusBomfim/Projeto../actions/workflows/ci.yml)

O relacionamento de uma construtora com os clientes, antes da obra existir: o interessado que chega pelo site ou por indicação, as perguntas que dizem se ele vai mesmo comprar, o cliente que já está na base com outro nome — e, nas próximas partes, o funil de vendas, as propostas e o contrato que vira obra no [planejamento-obras](https://github.com/MarcusBomfim/Projeto-C--obras).

C# 14 · .NET 10 · xUnit v3 · GitHub Actions

## O problema

Construtora vende pouco e caro: um galpão, um prédio, uma casa. Cada venda leva meses e passa por muita gente — quem pediu o orçamento, o arquiteto, o diretor que assina, o financeiro que paga. Sem um lugar para essa história, ela se espalha em e-mails, conversas de WhatsApp e na memória do vendedor, e vai embora com ele.

Três coisas atrapalham já na porta:

- **Quem não vai comprar consome o tempo de quem vai.** A maioria dos interessados não tem verba, ou a obra é para daqui a anos. É preciso perguntar cedo, e as mesmas perguntas para todos.
- **O mesmo cliente vira vários cadastros.** A filial liga, o diretor manda e-mail, a indicação chega pelo arquiteto — e cada um vira uma "empresa nova".
- **Documento errado vira dor de cabeça no contrato.** CNPJ e CPF têm dígitos verificadores; desde julho de 2026, o CNPJ também pode ter letras.

## Exemplo

O exemplo inteiro está em [`ExemploDoReadmeTests`](tests/CrmObras.Dominio.Testes/ExemploDoReadmeTests.cs). É um teste: se o código mudar e alguma frase desta página deixar de ser verdade, o build quebra.

A construtora já atende a matriz da Rota Sul, e tem o diretor de obras de contato:

```csharp
var matriz = new Conta("Logística Rota Sul", Documento.De("11.222.333/0001-81"));
matriz.AdicionarContato(new DadosDoContato("Otávio Reis", Email.De("otavio.reis@rotasul.com.br"),
    Telefone.De("(11) 3333-4444"), "Diretor de obras", PapelNaCompra.Decisor));
```

Pelo site chega a Marina, da filial de Campinas, pedindo um galpão. O que ela digitou é lido como as pessoas escrevem — `Marina.Costa@RotaSul.com.br` vira `marina.costa@rotasul.com.br`; `+55 19 99876-5432`, `(19) 99876-5432`:

```csharp
var lead = new Lead(
    new DadosDoLead("Marina Costa", "Rota Sul Logística — Campinas", Documento.De("11.222.333/0002-62"),
        Email.De("Marina.Costa@RotaSul.com.br"), Telefone.De("+55 19 99876-5432"), "Galpão de 1.200 m² em Jundiaí"),
    OrigemDoLead.Site, null, segunda);
```

Na primeira ligação, há verba e a obra é real, mas a Marina não decide, e ainda não há data. O lead não se qualifica, e diz por quê:

```csharp
lead.Avaliar(new Avaliacao(Orcamento: Sim, FalaComQuemDecide: Nao, Necessidade: Sim, Prazo: NaoSabido));
lead.Qualificar(segunda);
// O lead Marina Costa ainda não se qualifica: falta falar com quem decide ou ter o prazo da obra.
```

Na segunda, a obra tem data — março —, e o lead se qualifica. Antes de virar cliente, a busca de duplicados:

```csharp
var duplicados = BuscaDeDuplicados.Do(lead, [matriz]);
// Logística Rota Sul
//   a mesma empresa: 11.222.333/0001-81 tem a raiz do CNPJ do lead
//   Otávio Reis também tem e-mail em rotasul.com.br
```

A filial é da mesma empresa, mas tem CNPJ próprio: é outra conta. A matriz não a recebe por engano, e a conta da filial nasce com a Marina de contato principal:

```csharp
lead.ConverterNaConta(matriz, quarta);
// O lead é do cliente com o documento 11.222.333/0002-62, e a conta Logística Rota Sul tem o documento
// 11.222.333/0001-81: não são o mesmo cliente.

var filial = lead.ConverterEmContaNova(duplicados, quarta);
// Rota Sul Logística — Campinas, 11.222.333/0002-62, contato principal: Marina Costa
```

Se a busca tivesse achado uma conta com o mesmo CNPJ, a conta nova seria recusada — o lead viraria contato dela. E o CNPJ alfanumérico é conferido como o numérico: `12.ABC.345/01DE-35` passa; `12.ABC.345/01DE-53` não confere.

## Roteiro

O projeto é construído em partes, cada uma entregue funcionando, testada e documentada.

| Parte | Conteúdo | |
|--:|---|---|
| 1 | Clientes e leads: contas, contatos, CNPJ e CPF conferidos, qualificação, conversão e busca de duplicados | ✅ |
| 2 | Funil de vendas: etapas configuráveis, oportunidades, regras de passagem e o tempo em cada etapa | |
| 3 | Atividades e propostas: agenda com atrasos, propostas com versões e desconto com alçada de aprovação | |
| 4 | Previsão e relatórios: receita prevista ponderada, conversão por etapa, ciclo de venda e motivos de perda | |
| 5 | Persistência com EF Core e PostgreSQL, multiempresa com segurança por linha, testes com Testcontainers | |
| 6 | API REST: JWT, autorização por papel, ProblemDetails, concorrência otimista e idempotência | |
| 7 | Front-end em React: funil em kanban, ficha do cliente com a linha do tempo e painel da previsão | |
| 8 | Integração com o planejamento-obras por eventos — a proposta aceita vira obra —, observabilidade, Docker e entrega contínua | |

## O que já está pronto

**Parte 1 — clientes e leads**

- **CNPJ e CPF conferidos pelos dígitos verificadores**, com ou sem máscara. O CNPJ alfanumérico, que a Receita emite desde julho de 2026, é aceito com a mesma conta: cada letra vale o seu código ASCII menos 48. A raiz do CNPJ — a mesma na matriz e nas filiais — fica exposta.
- **E-mail e telefone como as pessoas escrevem.** O e-mail é guardado em minúsculas; o telefone, como DDD e número, aceito com `+55`, com o zero da operadora, com parênteses e traço — e o DDD tem de existir no plano da Anatel, o celular começa com 9 e o fixo de 2 a 5.
- **Conta e contatos.** A conta é o cliente, empresa ou pessoa; o documento pode chegar depois, mas, informado, não muda. Os contatos têm o papel na compra — decisor, influenciador, financeiro, usuário —, um deles é o principal, e o e-mail não se repete dentro da conta.
- **Lead com origem e qualificação.** Quatro perguntas — orçamento, quem decide, necessidade, prazo — e uma régua escrita: necessidade e orçamento confirmados, e quem decida ou prazo. O que não qualifica diz o que falta; o descartado tem motivo, e pode ser reaberto.
- **Busca de duplicados com indícios**, do mais forte para o mais fraco: mesmo documento, mesma raiz de CNPJ, mesmo e-mail, mesmo telefone, mesmo nome — sem acento, pontuação e "Ltda" — e mesmo domínio de e-mail de empresa.
- **Conversão que não duplica.** Converter em conta nova exige a busca feita, e é recusado se já existe conta com o mesmo documento. Na conta existente, o documento é conferido e o contato com o mesmo e-mail, reaproveitado.

## Decisões

As decisões que mudam o desenho do sistema estão registradas em [`docs/adr`](docs/adr):

- [ADR-0001](docs/adr/0001-linguagem-ubiqua-em-portugues.md) — o código fala a língua do comercial: `Lead`, `Conta`, `Qualificacao`, `Cnpj`.
- [ADR-0002](docs/adr/0002-a-conta-e-identificada-pelo-documento.md) — a conta é identificada pelo documento, que pode chegar depois mas não muda; matriz e filial são contas com a mesma raiz; cada agregado tem identidade do seu tipo.
- [ADR-0003](docs/adr/0003-qualificacao-e-conversao-do-lead.md) — o lead se qualifica por critérios escritos, e só vira conta depois de procurar os duplicados.

E algumas que não precisaram de ADR:

- **O domínio não depende de nada** além da biblioteca padrão do .NET. Um teste de arquitetura falha se ele passar a referenciar qualquer outro assembly.
- **Valores só nascem pela leitura que valida.** CNPJ, CPF, e-mail e telefone não têm construtor público: um CNPJ que não confere não existe.
- **Erros de quem usa vêm todos de uma vez.** O contato sem nome e sem meio de contato ouve as duas coisas numa mensagem só.
- **Aviso é erro.** Analisadores no nível `latest-recommended`, estilo verificado no build e `dotnet format` conferido no CI.

## Testes

- **Exemplos conferidos à mão**: os CNPJs e CPFs de exemplo, um CNPJ alfanumérico com a conta dos verificadores feita no papel, os DDDs que não existem, os e-mails que só parecem e-mail.
- **Propriedades em 400 casos sorteados**, com semente fixa: CNPJs numéricos e alfanuméricos e CPFs com os verificadores calculados pelo próprio teste — por uma conta escrita à parte — são aceitos, voltam iguais da máscara, e com um verificador trocado são recusados.
- **A régua da qualificação** em tabela: cada combinação de respostas, e o que falta em cada uma.
- **A busca de duplicados**, um indício de cada tipo, a ordem entre eles, o provedor gratuito que não aproxima ninguém e o nome comparado sem acento, pontuação e natureza jurídica.
- **O exemplo deste README**, de ponta a ponta.
- **Arquitetura.** O domínio só referencia a biblioteca padrão; toda classe pública é selada, estática ou abstrata; nenhuma propriedade tem `set` público; os valores não têm construtor público; e todo agregado tem identidade própria.

## Como rodar

Pré-requisito: o [SDK do .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet test --solution CrmObras.slnx
```

## Estrutura

```text
src/CrmObras.Dominio/                  regras do negócio, sem dependências
  Comum/                               exceção de domínio, identidade e textos
  Identificacao/                       CNPJ, numérico e alfanumérico, e CPF
  MeiosDeContato/                      e-mail e telefone
  Clientes/                            conta, contatos e o papel de cada um na compra
  Leads/                               lead, qualificação, descarte, conversão e busca de duplicados
tests/CrmObras.Dominio.Testes/         exemplos, propriedades e o exemplo deste README
tests/CrmObras.Arquitetura.Testes/     regras que o domínio não pode quebrar
docs/adr/                              decisões de arquitetura
```
