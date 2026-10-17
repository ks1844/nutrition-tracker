// Mock の簡易計算が、docs/03.栄養計算仕様.md §9 の正解データ（テスト用ダミー値）と一致することを確認する。
import { describe, expect, it } from 'vitest';
import { addTotals, emptyTotals, foodNutrition, roundHalfUp, scaleTotals, toAmountG, unitIdOf } from './mock-calc';

const carrotUnit = unitIdOf(1, 0); // 1本=150g
const misoUnit = unitIdOf(3, 0); // 大さじ=18g

describe('mock-calc（正解データ）', () => {
  it('AC-CALC-008 単位換算: 2本=300g、大さじ4=72g', () => {
    expect(toAmountG(1, carrotUnit, 2)).toBe(300);
    expect(toAmountG(3, misoUnit, 4)).toBe(72);
  });

  it('AC-CALC-001〜003 食材別の栄養量（廃棄率・未測定）', () => {
    const carrot = foodNutrition(1, 300);
    expect(carrot['PROT']?.amount).toBeCloseTo(2.7, 10);
    expect(carrot['VITC']?.amount).toBeCloseTo(27, 10);
    expect(carrot['VITA_RAE']?.amount).toBeCloseTo(1890, 8);

    const pork = foodNutrition(2, 400);
    expect(pork['PROT']?.amount).toBeCloseTo(80, 10);
    expect(pork['VITC']?.amount).toBeCloseTo(4, 10);
    expect(pork['VITA_RAE']?.amount).toBeCloseTo(40, 10);

    const miso = foodNutrition(3, 72);
    expect(miso['PROT']?.amount).toBeCloseTo(8.64, 10);
    expect(miso['VITC']).toEqual({ amount: 0, inc: true }); // 未測定は 0 + 不完全
    expect(miso['VITA_RAE']).toEqual({ amount: 0, inc: false });
  });

  it('AC-CALC-004〜007 メニュー合計とスナップショットの丸め', () => {
    const total = addTotals(addTotals(foodNutrition(1, 300), foodNutrition(2, 400)), foodNutrition(3, 72));
    expect(total['PROT']?.amount).toBeCloseTo(91.34, 10);
    expect(total['VITC']).toMatchObject({ inc: true });
    expect(total['VITC']?.amount).toBeCloseTo(31, 10);
    expect(total['VITA_RAE']?.amount).toBeCloseTo(1930, 8);

    const pick = (ratio: number, code: string): number => scaleTotals(total, ratio).find((n) => n.code === code)?.amount ?? NaN;
    expect(pick(0.125, 'PROT')).toBe(11.4175);
    expect(pick(0.1875, 'PROT')).toBe(17.1263); // 17.12625 を HALF_UP
    expect(pick(0.1875, 'VITC')).toBe(5.8125);
    expect(pick(0.1875, 'VITA_RAE')).toBe(361.875);
    expect(pick(0.333333, 'PROT')).toBe(30.4466);
    expect(pick(0.333333, 'VITA_RAE')).toBe(643.3327);
  });

  it('roundHalfUp は 5 を切り上げる', () => {
    expect(roundHalfUp(17.12625, 4)).toBe(17.1263);
    expect(roundHalfUp(0.125, 2)).toBe(0.13);
    expect(emptyTotals()['PROT']).toEqual({ amount: 0, inc: false });
  });
});
