# ADR-0018 — Observabilidade com OpenTelemetry, com o rastro atravessando a caixa de saída até o planejamento

**Status:** aceita

## Contexto

São dois serviços — a API e o processador —, e um fato passa pelos dois e sai do CRM: a venda ganha num pedido HTTP vira, segundos ou horas depois, noutro processo, um pedido ao planejamento-obras (ADR-0017). Quando a obra não aparece lá, a pergunta é sempre a mesma: o que aconteceu com *aquela* venda? Log solto em três processos não responde; o `traceId` do ProblemDetails (ADR-0014) aponta para um rastro que ninguém está guardando.

E quem vai rodar os serviços — o Compose hoje, um orquestrador amanhã — precisa saber, sem ler log, se cada um está vivo e se está pronto para trabalhar.

## Decisão

**1. OpenTelemetry nos dois serviços, configurado num lugar só.** O `CrmObras.Observabilidade` instrumenta os pedidos HTTP que o serviço atende e os que ele faz, os comandos no PostgreSQL (pelo Npgsql), o runtime do .NET e as fontes do próprio sistema — tudo o que se chama `CrmObras.*` —, e manda os logs no mesmo rastro. O recurso diz o serviço, a versão com o commit e o ambiente. É a mesma configuração do planejamento-obras, para os dois sistemas caberem no mesmo painel.

**2. Para onde vai é configuração do ambiente.** Com `OTEL_EXPORTER_OTLP_ENDPOINT` definido, rastros, métricas e logs saem por OTLP — para um coletor, um painel local ou um fornecedor, sem mudar código. Sem ele, nada sai do processo; o rastro continua existindo, e é dele o `traceId` dos erros. O rastro de quem chama — um `traceparent` no pedido — é continuado.

**3. O rastro atravessa a caixa de saída e chega ao planejamento.** A publicação da mensagem é um passo do pedido que ganhou a venda, e a mensagem leva o contexto dela (ADR-0016). O processador trata em continuação a ele, e o pedido ao planejamento leva o `traceparent` do tratamento — o próprio `HttpClient` o põe, sem pacote de instrumentação. O pedido que ganhou a venda, a publicação, o tratamento, o pedido ao planejamento e o que a API do planejamento fez com ele ficam num rastro só, mesmo com horas entre um e outro. O tratamento guarda o id do cronograma aberto. Os atributos seguem as convenções de mensageria do OpenTelemetry: o sistema é `caixa_de_saida`, e o destino é o tipo da mensagem — os tratadores assinam por tipo, como se assina um tópico.

**4. Olhar a fila não é rastro.** A reserva da próxima mensagem e a abertura da escuta do `LISTEN` rodam com a instrumentação suspensa: a volta vazia acontece a cada aviso e a cada intervalo, e encheria o painel de rastros sem história. Só o tratamento de uma mensagem conta uma — e cada execução de rotina de limpeza, que também vira um rastro.

**5. A fila tem métricas próprias**, com só o tipo da mensagem e o resultado — tratada, falhou, abandonada — como atributos: tentativas, duração de cada uma e a **espera**, da gravação do fato ao fim do tratamento. A espera é o número de quem vende: quanto a venda ganha leva para virar obra no planejamento. Id de mensagem ou de construtora numa métrica multiplicaria as séries sem fim; esses ficam no rastro.

**6. Duas verificações de saúde**, sem token e fora do documento OpenAPI: `/saude/viva` e `/saude/pronta`.
- **Viva** quer dizer que reiniciar não resolveria: o processo responde e, no processador, a fila está girando — deu sinal de vida há menos de quatro intervalos. Um tratador que trava para a fila, e só reiniciar a destrava.
- **Pronta** quer dizer viva e alcançando o banco. O banco fora do ar não mata ninguém: o serviço só deixa de receber trabalho até ele voltar.
- **O planejamento fora do ar não tira o processador de pronto.** A fila segura a venda ganha e tenta de novo; reiniciar o CRM não põe o planejamento no ar.

A resposta diz a situação de cada verificação, e nada do erro por trás dela, que vai para o log. As verificações não viram rastro, nem o `select 1` delas: o orquestrador pergunta a cada poucos segundos.

**7. O processador responde HTTP**, só para as verificações: é um host web sem nenhuma rota de negócio.

## Consequências

- **Um `traceId` no erro leva ao rastro inteiro** — o pedido, os comandos no banco e, se houve fato, o tratamento dele e o pedido ao planejamento.
- **O atraso da obra vira número.** A espera da fila responde "quanto tempo a venda ganha leva para chegar ao planejamento" sem abrir rastro nenhum.
- **Rastro longo.** O tratamento que falha volta em 30 segundos, um minuto, até uma hora — e continua no mesmo rastro. É a história verdadeira.
- **O processamento depende do SDK do OpenTelemetry**, e não só da API do .NET, por um motivo: suspender a instrumentação ao olhar a fila. As fontes e os medidores são da biblioteca padrão.
- **Amostragem fica para o coletor.** Todo rastro é registrado; com volume, quem decide o que guardar é o coletor, de preferência depois de ver o rastro inteiro.
- **O processador ocupa uma porta**, que em desenvolvimento é a 5092.
