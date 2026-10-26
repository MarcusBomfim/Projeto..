import { createHmac } from 'node:crypto';

/*
 * O provedor de identidade dos testes: uma chave simétrica que a API aceita
 * por configuração — como aceita a do `dotnet user-jwts` em desenvolvimento
 * — e com que os testes assinam os seus tokens. Não é segredo: só vale aqui,
 * e na pilha do Compose.
 *
 * O planejamento-obras tem o dele, também público no repositório de lá: é
 * com ele que os testes conferem a obra que a venda ganha abriu.
 */

export const Emissor = 'crm-obras-e2e';

export const Audiencia = 'crm-obras-e2e';

export const ChaveEmBase64 = Buffer.from('crm-obras-e2e: chave dos testes de ponta a ponta').toString(
  'base64',
);

/** O provedor de identidade dos testes do planejamento-obras, como a pilha do Compose o configura. */
export const DoPlanejamento = {
  emissor: 'planejamento-obras-e2e',
  audiencia: 'planejamento-obras-e2e',
  chaveEmBase64: Buffer.from('planejamento-obras-e2e: chave dos testes de ponta a ponta').toString('base64'),
} as const;

/** A configuração do JWT da API, em variáveis de ambiente: emissor, audiência e chave. */
export const ConfiguracaoDoJwt = {
  Authentication__Schemes__Bearer__ValidIssuer: Emissor,
  Authentication__Schemes__Bearer__ValidAudiences__0: Audiencia,
  Authentication__Schemes__Bearer__SigningKeys__0__Issuer: Emissor,
  Authentication__Schemes__Bearer__SigningKeys__0__Value: ChaveEmBase64,
};

const emBase64Url = (texto: string) => Buffer.from(texto).toString('base64url');

/** Um JWT HS256 assinado de verdade, que a API confere como confere qualquer outro. */
function assinar(carga: Record<string, unknown>, chaveEmBase64: string): string {
  const agora = Math.floor(Date.now() / 1000);
  const cabecalho = emBase64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const corpo = emBase64Url(JSON.stringify({ ...carga, nbf: agora - 5, iat: agora, exp: agora + 3600 }));
  const assinatura = createHmac('sha256', Buffer.from(chaveEmBase64, 'base64'))
    .update(`${cabecalho}.${corpo}`)
    .digest('base64url');

  return `${cabecalho}.${corpo}.${assinatura}`;
}

/** Um token do CRM, de alguém da construtora. */
export function emitirToken({
  construtora,
  nome,
  login,
  papeis,
}: {
  readonly construtora: string;
  readonly nome: string;
  readonly login: string;
  readonly papeis: readonly string[];
}): string {
  return assinar(
    { name: nome, preferred_username: login, role: papeis, construtora, iss: Emissor, aud: Audiencia },
    ChaveEmBase64,
  );
}

/** Um token do planejamento-obras: a construtora é a empresa dele, com o mesmo id. */
export function emitirTokenDoPlanejamento({
  empresa,
  nome,
  papeis,
}: {
  readonly empresa: string;
  readonly nome: string;
  readonly papeis: readonly string[];
}): string {
  return assinar(
    { name: nome, role: papeis, empresa, iss: DoPlanejamento.emissor, aud: DoPlanejamento.audiencia },
    DoPlanejamento.chaveEmBase64,
  );
}
