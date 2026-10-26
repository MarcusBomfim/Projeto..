#!/bin/sh
# Os papéis do planejamento-obras, como o repositório dele os cria
# (docker/banco/papeis.sh de lá, ADR-0013 e ADR-0017 dele): o banco dele é
# outro, e o CRM só o sobe para a venda ganha ter onde virar obra. A API e o
# processador do planejamento leem e gravam as tabelas; só o processador
# executa as funções que atravessam empresas.
set -e

psql --set ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    --set senha_da_aplicacao="$SENHA_DA_APLICACAO" \
    --set senha_do_processador="$SENHA_DO_PROCESSADOR" <<'SQL'
create role planejamento_aplicacao login password :'senha_da_aplicacao';
create role planejamento_processador login password :'senha_do_processador';

create schema planejamento;
grant usage on schema planejamento to planejamento_aplicacao, planejamento_processador;

alter default privileges in schema planejamento
    grant select, insert, update, delete on tables to planejamento_aplicacao, planejamento_processador;
alter default privileges in schema planejamento
    grant execute on functions to planejamento_processador;
SQL
