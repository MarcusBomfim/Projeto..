# ADR-0011 — Cada agregado é um documento `jsonb`, e cada construtora só enxerga os seus

**Status:** aceita

## Contexto

A Parte 5 grava os agregados no PostgreSQL. Cada um é uma árvore pequena — a conta com os contatos, o funil com as etapas, a oportunidade com as passagens, a proposta com as versões e os itens —, e nunca é usado pela metade: ninguém busca uma passagem solta, ou um item de proposta sem a proposta. Mapear cada nó para uma tabela daria uma dúzia delas, e o domínio teria de ceder ao ORM.

E o CRM atende várias construtoras no mesmo banco. Os clientes, as vendas e os descontos de uma são informação comercial: a outra não pode vê-los, nem por engano.

## Decisão

**1. Uma tabela por agregado, uma linha por agregado, o agregado inteiro numa coluna `jsonb`**, no esquema `crm`, com nomes em snake_case. Ao lado do documento, só o que o banco precisa enxergar: identidade, versão, formato, quando foi gravado, a construtora — e o que se lista ou se procura sem abrir o documento: o nome, o CNPJ ou CPF da conta, a situação do lead, da venda e da atividade, o vendedor, a data da atividade.

**2. As referências entre agregados vivem em colunas, e o banco as confere.** A venda aponta para a conta e para o funil; a atividade, para a conta e, se for o caso, para a venda; a proposta, para a venda e para a conta. Cada referência é uma chave estrangeira composta — a construtora e o id —, que não deixa apontar para o agregado de outra construtora, e com `RESTRICT`: a conta não sai do banco enquanto houver venda dela.

**3. O banco garante o que consegue sem repetir regra de negócio:** versão e formato a partir de 1, documento que é um objeto JSON, situação entre as que o domínio conhece, e o CNPJ ou CPF que não se repete na construtora — porque identifica o cliente (ADR-0002). As restrições têm nome, e o erro diz qual foi violada. O resto das regras fica no domínio, e vale também na leitura (ADR-0010).

**4. O documento é um tipo da infraestrutura, e não o agregado serializado.** Renomear uma propriedade do domínio não muda o formato gravado sem ninguém ver: muda o mapeamento, e um teste compara o JSON de cada agregado com o formato escrito por extenso.

**5. A leitura é estrita.** Propriedade que falta, propriedade que o documento não deveria ter e nulo onde não cabe fazem a leitura falhar. Enums vão pelo nome, e só pelo nome: o número deixaria entrar um valor que o enum não tem. O formato tem número; documento noutro formato é recusado com a razão, em vez de lido pela metade.

**6. Cada construtora só enxerga o que é dela, por duas barreiras.** No código, toda consulta sai com um filtro global pela construtora do contexto, e sem construtora nada se lê nem se grava. No banco, segurança por linha, forçada até para o dono das tabelas: a conexão declara a construtora num parâmetro de sessão, `crm.construtora_id`, e a política só deixa ver e gravar as linhas dela. A construtora é declarada a cada conexão aberta — inclusive nenhuma —, e a conexão que volta ao pool não a leva para o pedido seguinte. Cada barreira é testada sem a outra.

**7. A aplicação conecta com um papel comum**, sem superusuário e sem `BYPASSRLS`. As migrações rodam com o dono do banco, que passa por cima da segurança por linha. Os testes fazem igual, num PostgreSQL de verdade, num contêiner: sem Docker, os testes de banco são pulados com o motivo; no CI, Docker indisponível é falha.

## Consequências

- **Ler e gravar um agregado é uma linha**, sem junção e sem N+1.
- **Esquecer o filtro não vaza.** É preciso errar duas vezes, em duas camadas.
- **Toda tabela nova precisa da política e do filtro.** Dois testes listam as tabelas e falham se alguma não tiver um dos dois.
- **Consultar dentro dos documentos é mais caro.** O que se pergunta com frequência vira coluna, numa migração; o resto, os operadores do `jsonb` respondem.
- **A evolução do formato é responsabilidade do código**, não do banco: quem segura são o número do formato, o teste de formato e a leitura estrita.
