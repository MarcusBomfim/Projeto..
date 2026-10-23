# Problemas da API

Todo erro da API sai em [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) (`application/problem+json`). O `type` de cada problema aponta para uma seção desta página; o `title` é fixo por tipo; o `detail` diz o que aconteceu naquele pedido; o `traceId` acha o pedido nos registros do servidor. As decisões estão na [ADR-0014](../adr/0014-contrato-http.md).

```json
{
  "type": "https://github.com/MarcusBomfim/Projeto../blob/main/docs/api/problemas.md#versao-desatualizada",
  "title": "A versão informada não é mais a atual.",
  "status": 412,
  "detail": "A conta 0199… mudou desde a versão 1: está na versão 2. Leia de novo e refaça a alteração.",
  "instance": "PATCH /api/contas/0199…",
  "versaoAtual": 2,
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

## pedido-invalido

**400 — O pedido não está no formato esperado.** O corpo não é o JSON do contrato — falta um campo obrigatório, sobra um que o contrato não tem, há nulo onde não cabe, um enum veio como número ou com um nome que não existe, um número veio como texto —, um parâmetro da consulta está errado ou faltando, ou um cabeçalho está malformado: `If-Match` que não é uma versão, criação sem `Idempotency-Key`. O `detail` diz qual campo, parâmetro ou cabeçalho. Corrija o pedido; repetir igual dá o mesmo erro.

## nao-autenticado

**401 — É preciso se autenticar.** Faltou o token, ou ele não vale: assinatura, emissor, audiência ou validade. Obtenha um token novo no provedor de identidade.

## sem-permissao

**403 — Sem permissão para este pedido.** O token vale, mas não satisfaz a política do endpoint: falta o papel que ela exige, ou o token não traz uma construtora válida.

## nao-encontrado

**404 — Não encontrado.** Não existe o que foi pedido — ou existe, mas é de outra construtora: para quem pede, as duas coisas são iguais. Vale também para o que o caminho aponta dentro de um agregado: um contato que a conta não tem, uma etapa que o funil não tem.

## metodo-nao-permitido

**405 — Este recurso não aceita este método.**

## ja-existe

**409 — Já existe.** O que se tentou gravar repete o que identifica outro registro: uma conta com o CNPJ ou o CPF de outra conta da construtora, por exemplo. O `detail` diz qual.

## em-uso

**409 — Em uso por outro registro.** O agregado não pode ser removido enquanto outro depender dele: a conta com vendas, atividades ou propostas, o funil com vendas.

## versao-desatualizada

**412 — A versão informada não é mais a atual.** Alguém gravou o agregado depois que ele foi lido. O corpo traz a versão atual em `versaoAtual`. Leia de novo, refaça a alteração sobre o que está gravado e envie com a versão nova no `If-Match`.

## tipo-nao-suportado

**415 — O corpo do pedido precisa ser JSON.** Envie `Content-Type: application/json`.

## regra-do-negocio

**422 — O pedido fere uma regra do negócio.** O pedido está bem formado, mas o domínio o recusa: um CNPJ com o dígito verificador errado, um lead que ainda não se qualifica, uma etapa cuja probabilidade cairia, um desconto acima da alçada de quem envia. O `detail` traz a mensagem do domínio, escrita para quem usa o sistema — e, quando há mais de um problema, todos de uma vez.

## chave-reutilizada

**422 — A chave de idempotência já foi usada em outro pedido.** A mesma `Idempotency-Key` chegou com um pedido diferente do primeiro — outro corpo, outro caminho. Repetição usa a mesma chave com o mesmo pedido; pedido novo leva chave nova.

## versao-ausente

**428 — Falta a versão lida, no cabeçalho If-Match.** Toda alteração e toda remoção dizem a versão que leram: a ETag da leitura, como `"3"`. `If-Match: *` não vale — gravar sem dizer o que se leu é gravar por cima do que os outros fizeram.

## erro-interno

**500 — Erro interno.** Algo falhou no servidor. O corpo não diz o quê, de propósito: o detalhe fica nos registros, com o `traceId` que veio na resposta.
