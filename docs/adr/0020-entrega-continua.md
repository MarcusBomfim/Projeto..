# ADR-0020 — Entrega contínua: as imagens testadas são as publicadas

**Status:** aceita

## Contexto

Com a pilha em imagens (ADR-0019), falta o caminho delas até um servidor. O jeito comum tem dois furos: compilar as imagens de novo na hora de publicar — e publicar algo diferente do que foi testado —, e marcar a versão num commit que nunca passou no CI.

Este projeto não tem um ambiente para onde implantar. O que ele pode garantir é a outra metade: que todo commit da `main` que passa nos testes vira imagens prontas para implantar, e que toda versão marcada é uma delas. É o mesmo caminho do planejamento-obras, de quem o CRM já puxa as imagens publicadas.

## Decisão

**1. Entrega, e não implantação.** O produto do pipeline são as imagens no GitHub Container Registry: `crm-obras-api`, `crm-obras-processador`, `crm-obras-migrador` e `crm-obras-web`. Implantar é o ambiente puxar uma delas pelo digest — fora deste repositório.

**2. Cada commit da `main` que passa no CI é entregue.** O workflow de entrega começa quando o CI termina verde para um push na `main`. Ele não refaz o CI: confia no resultado dele para aquele commit.

**3. Compilar uma vez, testar o que foi compilado, publicar o que foi testado.** As quatro imagens são compiladas uma vez, sobem no Compose como candidatas — com o planejamento-obras publicado do outro lado — e passam pelos testes de ponta a ponta, a venda que vira obra inclusive. Só então recebem os nomes de verdade e sobem para o registro: a imagem publicada é, bit a bit, a testada.

**4. A versão é uma tag, e a tag só vale num commit verde.** Uma tag `v1.2.0` refaz o caminho — compilar, testar, publicar — com a versão dentro da imagem, e publica `1.2.0`, `1.2` e `latest`. Antes de tudo, o workflow confere se o CI passou naquele commit; se não passou, a versão não sai. Cada versão vira uma release, com o digest de cada imagem.

**5. Proveniência assinada.** Cada imagem publicada leva um atestado de proveniência (SLSA), assinado pelo GitHub com a identidade do workflow: de que repositório, de que commit e de que execução ela saiu. Quem puxa confere com `gh attestation verify`.

**6. A versão e o commit dentro da imagem.** O build recebe a versão e o commit, e o .NET os grava na versão informativa do assembly — a mesma que o OpenTelemetry mostra como versão do serviço (ADR-0018). Um rastro diz de que commit veio o código que o produziu.

**7. Nomes.** `main` é o último commit entregue da `main`; `sha-` e sete caracteres, um commit; `1.2.0`, `1.2` e `latest`, as versões. Num ambiente de verdade, implanta-se pelo digest, e não pelo nome. O Compose do CRM fixa o planejamento por um `sha-`, e não pela `main` dele: a versão com que a integração foi testada.

**8. Dependências atualizadas pelo Dependabot**, toda semana e agrupadas: os pacotes do .NET e do npm, as actions — fixadas pelo SHA — e as bases das imagens — fixadas pelo digest. Uma versão nova só é proposta duas semanas depois de sair. Versão maior do .NET, do Node, do TypeScript, do PostgreSQL e das bases das imagens fica de fora: é migração planejada, e não um PR que o CI só pode reprovar.

## Consequências

- **A `main` está sempre pronta para implantar**, e o que está no registro passou por todos os testes, inclusive os de ponta a ponta contra as próprias imagens e o planejamento de verdade.
- **Os testes de ponta a ponta rodam três vezes por commit**: contra o código, contra as imagens no workflow de ponta a ponta e contra as candidatas na entrega. A última é a que importa para publicar; as outras dão o retorno mais cedo, e também nos pull requests.
- **Publicar exige o CI verde, e o CI não espera a entrega.** Um push que falha no CI não chega ao registro; um que passa no CI e falha na entrega fica sem imagem, com o motivo no log e o relatório do Playwright guardado.
- **Os pacotes no registro nascem privados** na conta de quem é dono do repositório. Para qualquer um puxar, a visibilidade de cada um vira pública uma vez, nas configurações do pacote.
- **Atualizar o planejamento é uma linha**: a versão no Compose, e os testes de ponta a ponta dizendo se a integração continua de pé.
