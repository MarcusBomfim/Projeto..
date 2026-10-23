import { createHmac } from 'node:crypto';

/*
 * O provedor de identidade dos testes: uma chave simétrica que a API aceita
 * por configuração — como aceita a do `dotnet user-jwts` em desenvolvimento
 * — e com que os testes assinam os seus tokens. Não é segredo: só vale aqui.
 */

export const Emissor = 'crm-obras-e2e';

export const Audiencia = 'crm-obras-e2e';

export const ChaveEmBase64 = Buffer.from('crm-obras-e2e: chave dos testes de ponta a ponta').toString(
  'base64',
);

/** A configuração do JWT da API, em variáveis de ambiente: emissor, audiência e chave. */
export const ConfiguracaoDoJwt = {
  Authentication__Schemes__Bearer__ValidIssuer: Emissor,
  Authentication__Schemes__Bearer__ValidAudiences__0: Audiencia,
  Authentication__Schemes__Bearer__SigningKeys__0__Issuer: Emissor,
  Authentication__Schemes__Bearer__SigningKeys__0__Value: ChaveEmBase64,
};

const emBase64Url = (texto: string) => Buffer.from(texto).toString('base64url');

/** Um token assinado de verdade, que a API confere como confere qualquer outro. */
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
  const agora = Math.floor(Date.now() / 1000);
  const cabecalho = emBase64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const carga = emBase64Url(
    JSON.stringify({
      name: nome,
      preferred_username: login,
      role: papeis,
      construtora,
      iss: Emissor,
      aud: Audiencia,
      nbf: agora - 5,
      iat: agora,
      exp: agora + 3600,
    }),
  );
  const assinatura = createHmac('sha256', Buffer.from(ChaveEmBase64, 'base64'))
    .update(`${cabecalho}.${carga}`)
    .digest('base64url');

  return `${cabecalho}.${carga}.${assinatura}`;
}
