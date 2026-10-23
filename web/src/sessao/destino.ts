/**
 * Para onde voltar depois de entrar: só um caminho desta aplicação. Um
 * endereço de fora — `//site.com`, `/\site.com` —, posto no link por quem
 * quer enganar, vira o painel.
 */
export function destinoSeguro(destino: string | null): string {
  return destino?.startsWith('/') && !destino.startsWith('//') && !destino.startsWith('/\\') ? destino : '/';
}
