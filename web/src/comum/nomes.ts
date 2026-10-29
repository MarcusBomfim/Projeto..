/** As palavras que não dão inicial: "Maria das Dores" é MD. */
const Ligacoes = new Set(['de', 'da', 'do', 'das', 'dos', 'e']);

/** "Carlos Souza" → "CS"; "carlos.souza" → "CS"; "Rota Sul Logística — Campinas" → "RS". */
export function iniciais(nome: string): string {
  const palavras = nome
    .split(/[\s._-]+/)
    .filter((palavra) => /^\p{L}/u.test(palavra) && !Ligacoes.has(palavra.toLocaleLowerCase('pt-BR')))
    .slice(0, 2);

  return palavras.map((palavra) => palavra.charAt(0).toLocaleUpperCase('pt-BR')).join('') || '?';
}

/** Um matiz para cada nome, sempre o mesmo: a mesma pessoa tem a mesma cor em toda tela. */
export function matiz(nome: string): number {
  let soma = 0;

  for (const letra of nome) {
    soma = (soma * 31 + (letra.codePointAt(0) ?? 0)) % 360;
  }

  return soma;
}
