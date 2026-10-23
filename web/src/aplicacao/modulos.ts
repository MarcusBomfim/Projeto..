import type { RouteObject } from 'react-router';

export interface EntradaDoMenu {
  readonly rotulo: string;
  readonly caminho: string;
  /** A posição no menu: menor vem antes. */
  readonly ordem: number;
}

/** Uma área da aplicação: as rotas dela, dentro da moldura, e a entrada no menu, se tiver uma. */
export interface Modulo {
  readonly rotas: readonly RouteObject[];
  readonly menu?: EntradaDoMenu | undefined;
}

/**
 * As áreas da aplicação, encontradas na compilação: cada pasta com um
 * modulo.tsx entra sozinha — rotas e menu —, como os recursos da API se
 * encontram no assembly (ADR-0015). Área nova não mexe aqui nem nas rotas.
 */
const encontrados = import.meta.glob<Modulo>('../*/modulo.tsx', { eager: true, import: 'modulo' });

export const modulos: readonly Modulo[] = Object.values(encontrados);

export function entradasDoMenu(lista: readonly Modulo[] = modulos): EntradaDoMenu[] {
  return lista.flatMap((modulo) => (modulo.menu ? [modulo.menu] : [])).sort((a, b) => a.ordem - b.ordem);
}
