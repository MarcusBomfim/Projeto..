import { describe, expect, test } from 'vitest';
import { ConstrutoraDosTestes, tokenDeTeste } from '../testes/tokens';
import { lerSessao, type LeituraDoToken, type Sessao } from './token';

function sessao(leitura: LeituraDoToken): Sessao {
  if ('motivo' in leitura) {
    throw new Error(`Esperava uma sessão, e veio: ${leitura.motivo}`);
  }

  return leitura.sessao;
}

function motivo(leitura: LeituraDoToken): string {
  if ('sessao' in leitura) {
    throw new Error('Esperava a recusa do token.');
  }

  return leitura.motivo;
}

describe('o token de quem entra', () => {
  test('diz o nome, o login, a construtora, os cargos e até quando vale', () => {
    const lida = sessao(lerSessao(tokenDeTeste({ exp: 1_893_456_000, role: ['gerente', 'vendedor'] })));

    expect(lida.nome).toBe('Carlos Souza');
    expect(lida.login).toBe('carlos.souza');
    expect(lida.construtora).toBe(ConstrutoraDosTestes);
    expect(lida.papeis).toEqual(['gerente', 'vendedor']);
    expect(lida.venceEm).toEqual(new Date(1_893_456_000 * 1000));
  });

  test('aceita o cargo único em texto e o token com "Bearer" na frente', () => {
    const token = tokenDeTeste({ role: 'diretor' });
    const lida = sessao(lerSessao(`Bearer ${token}\n`));

    expect(lida.papeis).toEqual(['diretor']);
    expect(lida.token).toBe(token);
  });

  test('sem nome, mostra o do dotnet user-jwts, o login ou o sujeito', () => {
    expect(sessao(lerSessao(tokenDeTeste({ name: undefined, unique_name: 'maecu' }))).nome).toBe('maecu');
    expect(sessao(lerSessao(tokenDeTeste({ name: undefined }))).nome).toBe('carlos.souza');
    expect(
      sessao(lerSessao(tokenDeTeste({ name: undefined, preferred_username: undefined, sub: 'u-1' }))),
    ).toMatchObject({ nome: 'u-1', login: undefined });
  });

  test('recusa o que não é JWT, o que não diz a construtora e o vencido', () => {
    const agora = new Date('2026-10-15T12:00:00Z');

    expect(motivo(lerSessao('não-é-token'))).toContain('três partes');
    expect(motivo(lerSessao(tokenDeTeste({ construtora: undefined })))).toContain('construtora');
    expect(motivo(lerSessao(tokenDeTeste({ exp: agora.getTime() / 1000 - 1 }), agora))).toContain('venceu');
  });

  test('vale até o segundo antes do exp: no exp, já venceu (RFC 7519)', () => {
    const agora = new Date('2026-10-15T12:00:00Z');
    const exp = agora.getTime() / 1000;

    expect(motivo(lerSessao(tokenDeTeste({ exp }), agora))).toContain('venceu');
    expect(sessao(lerSessao(tokenDeTeste({ exp: exp + 1 }), agora)).venceEm).toEqual(
      new Date((exp + 1) * 1000),
    );
  });
});
