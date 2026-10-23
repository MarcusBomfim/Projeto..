# ADR-0014 — O contrato HTTP: token com a construtora, política por cargo, versão na ETag, erro em ProblemDetails

**Status:** aceita

## Contexto

A Parte 6 abre o CRM pela rede. Quem chama — o front-end, o celular do vendedor na obra, uma integração com o site que capta leads — precisa saber, sem ler o código: como se identifica, o que pode fazer, como sabe que o que leu ainda vale e, quando algo dá errado, o quê. Cada pergunta tem resposta padronizada na web; o trabalho é escolher uma e aplicá-la igual em todo endpoint.

## Decisão

**1. Autenticação por JWT, emitido por um provedor de identidade.** A API não guarda senha nem emite token: confere assinatura, emissor, audiência e validade, configurados em `Authentication:Schemes:Bearer` — em produção, um provedor OIDC; em desenvolvimento, o `dotnet user-jwts`. O token traz quem é (`sub`, `name`, e o login em `preferred_username`), os papéis (`role`) e a construtora (`construtora`).

**2. A construtora vem do token, e de nenhum outro lugar.** Não há id de construtora na rota nem no corpo: o cliente não escolhe em nome de quem pede. Token sem construtora válida recebe 403 em todo endpoint (ADR-0011).

**3. Quem pede é quem responde pelo que cria.** O login do token é o responsável da venda aberta, de quem marca a atividade, de quem redige a proposta. O cliente não informa o responsável de nada que cria em nome próprio.

**4. Autorização por política, e a política pelo cargo.** Os papéis são os cargos do comercial — os mesmos que dão a alçada de desconto (ADR-0007):

| Política | Papéis | Para |
|---|---|---|
| `consultar` | qualquer um da construtora | ler |
| `vender` | `vendedor`, `gerente`, `diretor` | cuidar de leads, contas, vendas, atividades e propostas |
| `gerir` | `gerente`, `diretor` | configurar funis, aprovar desconto, passar a venda a outro vendedor, remover o que foi cadastrado errado |

Quem tem mais de um papel age pelo cargo mais alto. A política de reserva exige ser da construtora: endpoint que esquecer de declarar política — ou rota que nem existe — não fica aberto, e sem token a resposta é 401, que não revela o que existe.

**5. As alçadas de desconto e o fuso são configuração da instalação.** As alçadas, na seção `Alcadas` — 5%, 10% e 15% sem ela. O fuso, em `Fuso`, um nome da base IANA — `America/Sao_Paulo` sem ele: o servidor conta o tempo em UTC, mas o dia é o da construtora — o "hoje" da agenda, o último dia da validade da proposta, os dias de um relatório. A agenda aceita outro fuso no pedido, para quem a olha de longe. Configuração que não se lê impede a API de subir.

**6. Um recurso por agregado; alteração pelo nome do que se faz.** `GET` para ler, `POST` na coleção para criar, `DELETE` para remover — e cada alteração como um pedido com o nome dela: `POST /leads/{id}/qualificacao`, `POST /funis/{id}/etapas/{etapa}/arquivamento`, `PUT /contas/{id}/principal`. `PUT` só substitui uma parte inteira — um contato, a avaliação do lead, uma etapa —, nunca o agregado: é o domínio que confere cada passo, e o erro fala do passo que falhou. Toda alteração devolve o agregado como ficou.

**7. A versão do agregado é a ETag** — forte: `"3"` —, em toda leitura e toda alteração. Alterar e remover exigem `If-Match` com a versão lida: sem ele, 428; com uma versão que já não é a atual, 412, com a atual no corpo do problema. A versão é conferida antes de aplicar a alteração — quem leu uma versão velha fica sabendo que o agregado mudou, e não recebe um erro sobre um estado que nunca viu — e de novo pelo banco, ao gravar (ADR-0012). Na leitura de um agregado, `If-None-Match` com a versão atual dá 304.

**8. Criar exige `Idempotency-Key`** (ADR-0013). Alterar não: o `If-Match` já impede que a mesma alteração se aplique duas vezes.

**9. O que grava mais de um agregado grava numa transação.** O lead que vira conta grava o lead e a conta; a proposta aceita grava a proposta e a venda ganha. Ou fica tudo, ou nada. O `If-Match` é o do agregado do caminho; o outro é gravado na versão em que foi lido ali, e quem gravar nele no meio faz o pedido inteiro ser recusado com 412. Se a operação é também uma criação, ela entra na transação da chave de idempotência.

**10. Todo erro sai em ProblemDetails** (RFC 9457), de um catálogo. O `type` aponta para a explicação em [`docs/api/problemas.md`](../api/problemas.md), o `title` é fixo por tipo, o `detail` diz o que houve naquele pedido, e o `traceId` acha o pedido nos registros. Os endpoints não tratam erro: lançam, e a tradução é uma só.

| Status | Quando |
|---|---|
| 400 | o pedido não é o combinado: JSON malformado, campo que falta ou sobra, parâmetro ou cabeçalho inválido |
| 401 | sem token válido |
| 403 | o token não satisfaz a política do endpoint |
| 404 | não existe — ou é de outra construtora; ou o caminho aponta para o que o agregado não tem |
| 409 | repete o que identifica outro registro, ou está em uso |
| 412 | a versão informada já não é a atual |
| 415 | o corpo não é JSON |
| 422 | regra do negócio, com a mensagem do domínio; ou chave de idempotência usada em outro pedido |
| 428 | alteração sem a versão lida |
| 500 | erro interno: o detalhe fica nos registros; quem pediu recebe só o `traceId` |

O que o caminho aponta e não existe é 404; o que o corpo aponta dentro do agregado e não existe — o contato que passaria a ser o principal — é regra do domínio, 422.

**11. Entrada estrita**, como na leitura dos documentos (ADR-0011): campo obrigatório que falta, nulo onde não cabe, campo que o contrato não tem, enum em número e número em texto dão 400, com a mensagem dizendo qual. Opção na consulta — `?situacao=Novo` — também só pelo nome. CNPJ, CPF, e-mail e telefone chegam como texto, do jeito que as pessoas escrevem, e o domínio os lê; os requisitos de etapa são uma lista de nomes, e não a soma de bits que o domínio guarda. Pedidos e respostas são tipos próprios da API, separados do domínio.

**12. Cada recurso se registra sozinho.** O `Program` mapeia toda classe de recurso que encontra; um recurso novo não mexe em registro central. Em troca, um teste percorre os endpoints mapeados e falha se algum ficar sem política, se uma alteração não exigir `If-Match` ou se uma criação não exigir `Idempotency-Key` — e confere que o documento OpenAPI descreve esses cabeçalhos.

## Consequências

- **O cliente sabe o que fazer com cada resposta** sem ler o código: o `type` do problema é estável, a ETag diz a versão, a criação repetida é segura.
- **Trocar o provedor de identidade é configuração.** A API depende do formato do token, não de quem o emite.
- **Mais pedidos do que com um `PUT` do agregado inteiro.** Montar uma conta com vários contatos são vários pedidos, cada um com a sua versão. Para criar de uma vez, o `POST` aceita os contatos junto — e o funil, as etapas.
- **As alçadas e o fuso valem para a instalação inteira**, e não por construtora. Quando uma construtora quiser os seus, eles viram configuração dela, gravada no banco.
- **A listagem devolve tudo**, sem paginação: uma construtora tem centenas de contas, não milhões. Quando precisar, a paginação entra como parâmetro opcional, sem quebrar o contrato.
- **Erro do servidor não diz nada a quem pediu** além do `traceId` — de propósito.
