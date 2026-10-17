import { describe, expect, it } from 'vitest';
import { MealRecordItemView } from '../types/api';
import { describeItem, fmtAmount, guessMealType, itemKcal } from './format';

const item = (p: Partial<MealRecordItemView>): MealRecordItemView => ({
  id: 1, menuId: null, menuName: null, foodId: null, foodName: null, unitId: null,
  inputMode: 'GRAM', inputValue: 0, portionRatio: null, amountG: null, nutrients: [], ...p,
});

describe('format', () => {
  it('kcal は整数、それ以外は小数 1 桁で表示する', () => {
    expect(fmtAmount(1704.4, 'kcal')).toBe('1,704');
    expect(fmtAmount(11.4175, 'g')).toBe('11.4');
    expect(fmtAmount(17.1263, 'g')).toBe('17.1');
  });

  it('記録項目の説明（メニューは杯数・割合・g、食材は g）', () => {
    expect(describeItem(item({ menuName: '豚汁', inputMode: 'SERVING', inputValue: 1.5 }))).toBe('豚汁 1.5杯');
    expect(describeItem(item({ menuName: '豚汁', inputMode: 'RATIO', inputValue: 0.25 }))).toBe('豚汁 25%');
    expect(describeItem(item({ menuName: '豚汁', inputMode: 'GRAM', inputValue: 300 }))).toBe('豚汁 300g');
    expect(describeItem(item({ foodName: 'ご飯', amountG: 150 }))).toBe('ご飯 150g');
  });

  it('記録項目のエネルギーを取り出す', () => {
    expect(itemKcal(item({ nutrients: [{ code: 'ENERGY', amount: 123.4, isIncomplete: false }] }))).toBe(123.4);
    expect(itemKcal(item({}))).toBe(0);
  });

  it('時刻から食事区分の初期値を決める', () => {
    expect(guessMealType(new Date(2026, 9, 17, 8))).toBe('breakfast');
    expect(guessMealType(new Date(2026, 9, 17, 12))).toBe('lunch');
    expect(guessMealType(new Date(2026, 9, 17, 19))).toBe('dinner');
    expect(guessMealType(new Date(2026, 9, 17, 23))).toBe('snack');
  });
});
