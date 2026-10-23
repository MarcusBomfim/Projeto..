export const ConstrutoraDosTestes = '0199e3b4-0000-7000-8000-000000000001';

function base64url(texto: string): string {
  return btoa(String.fromCharCode(...new TextEncoder().encode(texto)))
    .replaceAll('+', '-')
    .replaceAll('/', '_')
    .replace(/=+$/, '');
}

/**
 * Um JWT com a assinatura qualquer: o front-end não confere assinatura —
 * quem confere é a API. O do Carlos, vendedor, válido por uma hora, a menos
 * que a carga diga outra coisa.
 */
export function tokenDeTeste(carga: Record<string, unknown> = {}): string {
  const completa = {
    name: 'Carlos Souza',
    preferred_username: 'carlos.souza',
    construtora: ConstrutoraDosTestes,
    role: ['vendedor'],
    exp: Math.floor(Date.now() / 1000) + 3600,
    ...carga,
  };

  return `${base64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))}.${base64url(JSON.stringify(completa))}.assinatura`;
}
