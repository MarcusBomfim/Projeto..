# ADR-0015 — Front-end tipado pelo contrato da API, com a regra no domínio e o estado do servidor no cache

**Status:** aceita

## Contexto

Até aqui, quem usa o CRM fala com a API. A Parte 7 traz telas: o funil em kanban, a ficha do cliente com a linha do tempo, o painel da previsão. Uma aplicação no navegador traz problemas que a API não tinha:

- o contrato pode mudar de um lado e não do outro;
- o que veio do servidor envelhece na tela — e, no kanban, outra pessoa pode ter movido a mesma venda;
- a tela pode ser tentada a decidir regra — "esta venda pode ir para a proposta?" — e decidir diferente do domínio;
- o token tem de chegar a cada pedido;
- arrastar um cartão e ler um gráfico têm de funcionar para quem não usa mouse ou não enxerga.

## Decisão

**1. Uma aplicação React no próprio repositório**, em `web/`, com Vite e TypeScript estrito. Ela fala com a API na mesma origem: em desenvolvimento, o Vite repassa `/api` para ela, e a API não precisa de CORS.

**2. O contrato da API é a fonte dos tipos.** O documento OpenAPI fica versionado em `docs/api/openapi.json`, e um teste da API falha se ela mudar sem o arquivo. O front-end gera dele os seus tipos, com o openapi-typescript, e o CI falha se o arquivo gerado não bater com o contrato. O cliente HTTP, o openapi-fetch, tipa o caminho, os parâmetros e a resposta de cada chamada — e também os cabeçalhos que o contrato exige: alteração sem `If-Match` e criação sem `Idempotency-Key` são erro de compilação, e não um 428 em produção. Os enums chegam pelo nome, e cada nome tem o seu rótulo na tela: um valor novo no contrato sem rótulo não compila.

**3. A tela pede; o domínio decide.** Mover o cartão no kanban é um `PUT` com a versão que o quadro mostrou no `If-Match` (ADR-0014). A tela não confere se a venda tem o que a etapa pede, nem move o cartão antes da resposta: espera a API, e o que o domínio recusar aparece com a mensagem dele — "falta quem decide". Dando certo ou não, o quadro é lido de novo: a versão mudou, ou outra pessoa mexeu antes, e o 412 diz isso.

**4. O que vem da API mora no cache, não num estado global.** O TanStack Query guarda as leituras, junta pedidos iguais e pergunta de novo ao voltar para a aba; uma alteração invalida as leituras que ela muda. Erro de rede e do servidor são tentados de novo; um 404 ou um 403, não. O único estado da aplicação é a sessão.

**5. A sessão é o token do provedor de identidade, guardado na aba.** Em desenvolvimento, cola-se o token do `dotnet user-jwts`. O front-end só lê o token — a construtora, o login, o cargo, quando vence —, para mostrar quem entrou; quem confere a assinatura é a API, a cada pedido. Se a API recusar o token, ou se ele vencer, a sessão acaba, e a tela de entrada diz por quê. Trocar de sessão esvazia o cache: o que uma construtora leu não aparece para a próxima.

**6. Cada área da aplicação se registra sozinha**, como os recursos da API (ADR-0014). Cada pasta com um `modulo.tsx` declara as suas rotas e a sua entrada no menu, e a moldura as encontra na compilação. Tela nova não mexe nas rotas.

**7. Todo gesto tem um caminho sem mouse, e toda cor tem palavra.** Arrastar o cartão tem o par no próprio cartão: um seletor da etapa, rotulado para leitor de tela, que faz o mesmo pedido; o resultado é anunciado. A venda parada tem cor, e o selo escreve "Parada". Gráficos são SVG feito à mão, sem biblioteca: a geometria fica em funções puras, testadas com números, e cada gráfico tem ao lado a tabela dos seus números — é ela que um leitor de tela lê.

**8. Os testes abrem a aplicação inteira.** As rotas, a sessão e o cache são os de verdade; só a API é de mentira, com o MSW, e ela recusa pedido que o teste não previu — inclusive o `PUT` que a tela não devia ter feito. Os exemplos são a carteira do README, tipados pelo contrato.

## Consequências

- **A API mudou e o front-end não acompanhou: quebra o CI, e não a tela em produção.** E quem muda o contrato vê o diff dele no mesmo commit.
- **O kanban responde no tempo da API.** Sem atualização otimista, o cartão espera a resposta para mudar de coluna — uma fração de segundo na rede da obra, e nunca uma venda que parecia ter ido e não foi.
- **TypeScript 5.9**, e não o 7: o typescript-eslint e o openapi-typescript ainda não o suportam. A troca é uma linha quando suportarem.
- **Dinheiro chega como número de ponto flutuante.** Basta para mostrar com centavos; o front-end não faz conta de dinheiro, e quem faz é o domínio, em `decimal`.
- **O token fica ao alcance de script da própria página.** O que o protege é não haver script de fora: nenhuma dependência carregada de outro site, e o React escapando o que mostra. Com um provedor de identidade de verdade, o caminho mais seguro é um back-end para o front-end, com o token num cookie que script nenhum lê. A tela de colar token sai; o resto não muda.
- **Em desenvolvimento são dois processos**, a API e o Vite. Em produção, servir os dois na mesma origem fica com quem implanta.
