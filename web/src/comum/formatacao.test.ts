import { describe, expect, test } from 'vitest';
import {
  diasDesde,
  formatarData,
  formatarDataEHora,
  formatarDias,
  formatarMes,
  formatarMoeda,
  formatarMoedaCompacta,
  formatarProbabilidade,
  lerData,
} from './formatacao';

// O Intl separa o símbolo da moeda com um espaço que não quebra.
const r$ = 'R$\u00a0';

describe('formatação', () => {
  test('dinheiro em reais, com centavos, e o traço quando não há valor', () => {
    expect(formatarMoeda(1_843_019.2)).toBe(`${r$}1.843.019,20`);
    expect(formatarMoeda(null)).toBe('—');
    expect(formatarMoedaCompacta(2_400_000)).toBe(`${r$}2,4\u00a0mi`);
    expect(formatarProbabilidade(60)).toBe('60%');
  });

  test('o mês da previsão abreviado como o comercial escreve, e maio por extenso', () => {
    expect(formatarMes('2027-01')).toBe('jan./2027');
    expect(formatarMes('2027-05')).toBe('maio/2027');
    expect(() => formatarMes('2027-13')).toThrow('aaaa-mm');
    expect(() => formatarMes('01/2027')).toThrow('aaaa-mm');
  });

  test('a data da API é a data, e não a meia-noite de UTC', () => {
    // Em UTC−3, new Date('2026-12-01') é 30 de novembro às 21h.
    const data = lerData('2026-12-01');

    expect([data.getFullYear(), data.getMonth(), data.getDate()]).toEqual([2026, 11, 1]);
    expect(formatarData('2026-12-15')).toBe('15/12/2026');
    expect(formatarData(null)).toBe('—');
    expect(() => lerData('15/12/2026')).toThrow('aaaa-mm-dd');
  });

  test('o instante na hora de quem lê', () => {
    expect(formatarDataEHora('2026-11-27T13:00:00+00:00')).toBe('27/11/2026, 10:00');
  });

  test('os dias inteiros desde um instante, e como se dizem', () => {
    const agora = new Date('2026-11-26T12:00:00Z');

    expect(diasDesde('2026-11-05T12:00:00Z', agora)).toBe(21);
    expect(diasDesde('2026-11-25T12:00:01Z', agora)).toBe(0);
    expect(diasDesde('2026-11-27T12:00:00Z', agora)).toBe(0);
    expect([formatarDias(0), formatarDias(1), formatarDias(21)]).toEqual(['hoje', '1 dia', '21 dias']);
  });
});
