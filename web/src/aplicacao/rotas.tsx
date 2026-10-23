import type { RouteObject } from 'react-router';
import { TelaDeEntrada } from '../sessao/TelaDeEntrada';
import { Moldura } from './Moldura';
import { modulos } from './modulos';
import { NaoEncontrada } from './NaoEncontrada';

/** A entrada, fora da moldura; e as rotas de cada área, dentro dela. */
export function criarRotas(): RouteObject[] {
  return [
    { path: '/entrar', element: <TelaDeEntrada /> },
    {
      path: '/',
      element: <Moldura />,
      children: [...modulos.flatMap((modulo) => modulo.rotas), { path: '*', element: <NaoEncontrada /> }],
    },
  ];
}
