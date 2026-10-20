# ADR-0002 — A conta é identificada pelo documento, e cada agregado pela sua identidade

**Status:** aceita

## Contexto

O cliente de uma construtora é uma empresa — incorporadora, indústria, loja — ou uma pessoa que constrói a casa. O comercial cadastra o cliente quando ele aparece, e ele aparece de muitos jeitos: um formulário do site com nome e e-mail, uma indicação por telefone, uma visita ao estande. O CNPJ ou o CPF raramente vêm no primeiro contato.

Sem uma regra de identidade, o mesmo cliente vira três cadastros — um por vendedor, um por canal —, cada um com uma parte da história. É o problema que o CRM existe para resolver.

## Decisão

**1. Conta é o cliente**, empresa ou pessoa. Contato é a pessoa com quem se fala, e só existe dentro de uma conta.

**2. O documento é opcional, mas, informado, identifica a conta e não muda.** CNPJ — numérico ou alfanumérico — para empresa; CPF para pessoa; os dois conferidos pelos dígitos verificadores. Documento diferente é outro cliente: a regra está na conta, e a unicidade entre contas, quando houver banco, num índice.

**3. Matriz e filial são CNPJs diferentes**, com a mesma raiz. A raiz fica exposta, para quem procura duplicados saber que a filial e a matriz são a mesma empresa.

**4. Contato precisa de um jeito de falar com ele** — e-mail ou telefone —, e o e-mail não se repete dentro da conta. Havendo contatos, um é o principal: o primeiro, até que se escolha outro; e o principal só sai depois de escolhido outro.

**5. O contato tem um papel na compra** — decisor, influenciador, financeiro, usuário —, porque numa obra raramente é a mesma pessoa que escolhe, que assina e que paga.

**6. Cada agregado tem identidade do seu tipo** — `ContaId`, `ContatoId` —, sobre um Guid versão 7. Passar o id de um onde se espera o de outro é erro de compilação, e os ids novos começam pelo instante da criação: criados em sequência, ficam em sequência no índice.

## Consequências

- O cliente que chega pelo site sem CNPJ entra no CRM na hora, e o documento é completado quando aparecer.
- Corrigir um documento informado errado não é editar a conta: os dígitos verificadores pegam quase todo erro de digitação, e o que passar é tratado como cliente errado, e não como campo errado.
- O e-mail só é único dentro da conta. A mesma pessoa em duas contas — o arquiteto que indica clientes, por exemplo — é possível, e quem aponta a coincidência é a busca de duplicados.
- Um telefone e um e-mail por contato. Mais de um de cada é extensão sem mudança de regra, para quando fizer falta.
