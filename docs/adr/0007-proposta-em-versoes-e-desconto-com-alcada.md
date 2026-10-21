# ADR-0007 — A proposta é feita em versões, e o desconto acima da alçada precisa de quem aprove

**Status:** aceita

## Contexto

A proposta de obra raramente é aceita na primeira: o cliente pede para tirar a cobertura, trocar o piso, rever o prazo, baixar o preço. Cada rodada é uma proposta nova — e a construtora precisa saber o que mandou em cada uma, porque o cliente cita "aquele valor de outubro". Sobrescrever a proposta apaga essa história; um arquivo por rodada a espalha.

E o preço é onde a margem se perde. Na pressão do fechamento, o vendedor dá o desconto que o cliente pede. Toda construtora tem uma regra — o vendedor dá até tanto, o gerente até tanto, acima disso só o diretor —, mas regra que não está no sistema é conferida quando a proposta já foi.

## Decisão

**1. A proposta tem versões.** A primeira nasce em rascunho, e o rascunho muda à vontade. Enviada, a versão não muda mais: para mudar o que o cliente viu, cria-se a versão seguinte, igual à última, e redige-se nela. Só há um rascunho por vez. Enviada, a versão nova substitui a que estava com o cliente.

**2. Os valores são feitos como o cliente os lê.** Cada linha é quantidade vezes preço unitário, arredondada ao centavo; o subtotal é a soma das linhas arredondadas; o desconto é uma porcentagem do subtotal, também ao centavo; o total é o subtotal menos o desconto. Assim a soma que o cliente faz na calculadora bate com a da proposta. O preço unitário é em centavos; a quantidade, qualquer positiva.

**3. A alçada é por cargo, e de cada construtora.** Vendedor, gerente e diretor dão, cada um, até uma porcentagem — a padrão é 5%, 10% e 15% —, e quem está acima não dá menos que quem está abaixo. Desconto até a alçada de quem envia não pede nada. Acima dela, precisa da aprovação de alguém cuja alçada o cubra; acima da alçada do diretor, não há quem aprove.

**4. A aprovação vale para o que foi aprovado.** Redigir a versão de novo desfaz a aprovação, ainda que o desconto continue o mesmo: com outros itens, o mesmo percentual é outro valor. E a alçada é conferida no envio, com a regra que estiver valendo: se a construtora reduziu a alçada do gerente depois de ele aprovar, a aprovação não basta mais.

**5. O cliente responde à versão que está com ele.** Aceita dentro da validade — até o último dia, no fuso de quem registra — ou recusa, com o motivo, a que a versão seguinte vai responder. A versão vencida não se aceita: manda-se outra. Aceita a proposta, nada mais muda, e o rascunho que estivesse em andamento é descartado.

## Consequências

- O histórico de versões é a história da negociação: o que foi oferecido, quando, por quem, com que desconto e com aprovação de quem.
- As alçadas são parâmetro, e não configuração guardada no domínio. Quando houver banco, cada construtora guarda as suas, e quem envia ou aprova passa as que estiverem valendo.
- O desconto é um só, sobre o subtotal. Desconto por item, ou acréscimo, entram quando alguém precisar — e a alçada vai continuar olhando o desconto total sobre o preço cheio.
