#!/bin/sh
# Os papéis da API e do processador, como quem implanta os criaria (ADR-0011,
# ADR-0016). Roda uma vez, quando o volume do banco nasce, antes das
# migrações: as tabelas e as funções ainda não existem. Os privilégios padrão
# concedem o que o dono — quem migra — criar depois no esquema: as tabelas
# para os dois, as funções só para o processador. Nenhum dos dois é dono de
# nada, então a segurança por linha vale para os dois.
#
# Sem permissão de execução — no Linux, o arquivo chega assim do Git —, o
# entrypoint do PostgreSQL não roda o script: lê no shell dele. Por isso só o
# set -e, que ele mesmo usa, e nada que mude o comportamento do resto dele.
set -e

psql --set ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    --set senha_da_aplicacao="$SENHA_DA_APLICACAO" \
    --set senha_do_processador="$SENHA_DO_PROCESSADOR" <<'SQL'
create role crm_aplicacao login password :'senha_da_aplicacao';
create role crm_processador login password :'senha_do_processador';

create schema crm;
grant usage on schema crm to crm_aplicacao, crm_processador;

alter default privileges in schema crm
    grant select, insert, update, delete on tables to crm_aplicacao, crm_processador;
alter default privileges in schema crm
    grant execute on functions to crm_processador;
SQL
