import { parseArgs } from 'node:util';
import { emitirToken, emitirTokenDoPlanejamento } from './identidade.ts';

/*
 * Um token para entrar pelo navegador na pilha do Compose, que aceita a
 * chave dos testes (ADR-0019). Roda com o Node 24, sem instalar nada:
 *
 *   node e2e/apoio/token.ts --construtora 0199e3b4-0000-7000-8000-000000000001 --papel diretor
 *
 * Com --planejamento, o token é o do planejamento-obras, para ver lá a obra
 * que a venda ganha abriu: a construtora é a empresa dele.
 *
 * Sem --papel, vale para todos os papéis do sistema; sem --construtora, para
 * a construtora dos exemplos.
 */

const { values } = parseArgs({
  options: {
    construtora: { type: 'string', default: '0199e3b4-0000-7000-8000-000000000001' },
    nome: { type: 'string', default: 'Pessoa de teste' },
    login: { type: 'string', default: 'pessoa.de.teste' },
    papel: { type: 'string', multiple: true },
    planejamento: { type: 'boolean', default: false },
  },
});

console.log(
  values.planejamento
    ? emitirTokenDoPlanejamento({
        empresa: values.construtora,
        nome: values.nome,
        papeis: values.papel ?? ['planejador', 'orcamentista', 'gestor', 'engenheiro'],
      })
    : emitirToken({
        construtora: values.construtora,
        nome: values.nome,
        login: values.login,
        papeis: values.papel ?? ['vendedor', 'gerente', 'diretor'],
      }),
);
