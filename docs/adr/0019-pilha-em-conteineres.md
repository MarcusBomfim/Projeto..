# ADR-0019 — A pilha em contêineres, com o planejamento-obras ao lado

**Status:** aceita

## Contexto

Até aqui, rodar o CRM era rodar coisas à mão: o PostgreSQL, as migrações, a API e o front-end — e agora o processador, que só faz sentido com o planejamento-obras no ar, para a venda ganha ter onde virar obra. O banco conectava como `postgres`, um superusuário que passa por cima da segurança por linha (ADR-0011). O que ia para um servidor nunca tinha sido montado como iria.

E os testes de ponta a ponta (ADR-0015) sobem a pilha a partir do código compilado, sem o processador e sem o planejamento: a integração das duas pontas nunca era vista de fora.

## Decisão

**1. Um Dockerfile, quatro imagens:** a API, o processador, o migrador e o front-end. O .NET compila uma vez, e cada alvo copia o que é seu. As dependências são restauradas numa camada só delas, que só se refaz quando um `.csproj` ou o `Directory.Packages.props` muda.

**2. Imagens sem shell e sem root.** A API e o processador rodam sobre a imagem *chiseled* do ASP.NET Core — sem gerenciador de pacotes, sem shell, com o usuário `app` —, na variante com a ICU e a base de fusos: o fuso da construtora (`America/Sao_Paulo`) e o nome sem acento da busca de duplicados dependem delas. Sem a base de fusos, a API não sobe — e foi rodando a imagem que isso apareceu. O migrador vai sobre a de dependências de runtime. O front-end vai no nginx sem privilégios, na porta 8080, com a política de segurança de conteúdo que só deixa rodar o que é da própria origem.

**3. O nginx repassa a API como ela respondeu.** Os arquivos do Vite vão comprimidos; a API, não: o gzip do nginx troca a ETag forte por uma fraca — `"3"` vira `W/"3"` —, e o `If-Match`, que compara forte (ADR-0014), recusaria toda alteração feita pela tela.

**4. As bases são fixadas pelo digest**, e não só pela tag: a mesma tag aponta para outra imagem amanhã. Atualizar é trocar o digest e ver os testes passarem com a base nova.

**5. As migrações são um executável à parte**, o pacote de migrações do EF Core, que roda num contêiner próprio e sai. Ninguém migra o banco ao subir a API: com duas réplicas, seriam duas migrações ao mesmo tempo, e a API teria de conectar como dono das tabelas. O migrador conecta como dono; a API e o processador só sobem depois que ele termina bem.

**6. O banco com os papéis de produção.** Na criação do volume, um script cria o papel da API e o do processador, e os privilégios padrão concedem a eles o que o dono criar depois: as tabelas para os dois, as funções que atravessam construtoras só para o processador (ADR-0016). Nenhum dos dois é dono de nada, e a segurança por linha vale para os dois.

**7. O Compose sobe o CRM e o planejamento-obras.** O planejamento entra com as imagens que ele mesmo publica no GitHub Container Registry, fixadas na versão com que este CRM foi testado, num banco dele, com os papéis dele e numa rede dele — o front-end do planejamento repassa `/api` para `api`, que lá precisa ser a API dele. A API do planejamento aceita dois emissores de token: o provedor de identidade de quem usa, e o CRM, que se apresenta com o token de serviço (ADR-0017). O painel do .NET Aspire recebe os rastros, as métricas e os logs dos dois sistemas, e mostra num rastro só a venda ganha virando obra (ADR-0018). Sem shell nas imagens, a saúde é perguntada de fora: `/saude/pronta` de cada serviço, como faria um orquestrador.

**8. Os testes de ponta a ponta rodam também contra as imagens.** Os mesmos testes, sem o Playwright subir nada: a pilha é a do Compose, a API vem pela mesma origem, pelo nginx, e o provedor de identidade é o dos testes — a chave pública deles, que só a pilha do Compose aceita. Só ali há o processador e o planejamento, e só ali roda o teste que aceita a proposta no CRM e espera o cronograma da obra aparecer no planejamento. O que o navegador não enxerga é conferido à parte: os papéis no banco, a API longe das funções que atravessam construtoras, os cabeçalhos do nginx e a ETag forte passando por ele.

## Consequências

- **O que vai para produção é o que os testes de ponta a ponta usaram**: as mesmas imagens, o mesmo nginx, os mesmos papéis no banco — e o planejamento de verdade do outro lado.
- **Uma imagem sem shell não se depura por dentro.** O diagnóstico é pelo que ela publica — os rastros, as métricas, os logs e as verificações de saúde.
- **O CRM conhece a forma de implantar o planejamento**: o script dos papéis dele e a configuração da API dele. Se o planejamento mudar isso numa versão nova, o Compose do CRM muda junto — é o preço de testar a integração com o outro sistema de verdade, e não com um de mentira.
- **O script dos papéis roda só na criação do volume.** Banco que já existia não ganha os papéis: quem implanta os cria. O script é a receita.
- **As chaves da pilha do Compose são públicas** — a dos tokens de quem usa e a do token de serviço do CRM no planejamento. Servem para a máquina de quem desenvolve e para o CI; em qualquer outro lugar, vêm do ambiente.
- **O primeiro build é lento** — restaurar o .NET e o npm do zero, e baixar as imagens do planejamento —; os seguintes reaproveitam as camadas.
