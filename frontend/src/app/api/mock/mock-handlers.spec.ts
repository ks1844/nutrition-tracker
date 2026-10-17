// Mock API が docs/09.API仕様.md の挙動（プレビュー・100%超過警告・欠測・エラー形式）を再現していることを確認する。
import { beforeEach, describe, expect, it } from 'vitest';
import { MealRecordSaved, MenuPreview } from '../../types/api';
import { MockHttpError, handle } from './mock-handlers';
import { resetState } from './mock-store';
import { todayString } from '../../services/date-util';

const call = (method: string, path: string, body?: unknown, q = ''): { status: number; body: unknown } =>
  handle({ method, path, query: new URLSearchParams(q), body });

describe('mock-handlers', () => {
  beforeEach(() => resetState(true));

  it('AC-MENU-002 / AC-CALC-004 プレビューが豚汁の正解データと一致する', () => {
    const res = call('POST', '/api/menus/preview', {
      items: [
        { foodId: 1, unitId: 101, quantity: 2 },
        { foodId: 2, unitId: null, quantity: 400 },
        { foodId: 3, unitId: 301, quantity: 4 },
      ],
      cookedTotalWeightG: 2400,
      servings: 8,
    });
    const p = res.body as MenuPreview;
    const get = (code: string): { amount: number; isIncomplete: boolean } | undefined => p.nutrients.find((n) => n.code === code);
    expect(get('PROT')).toEqual({ code: 'PROT', amount: 91.34, isIncomplete: false });
    expect(get('VITC')).toMatchObject({ amount: 31, isIncomplete: true });
    expect(get('VITA_RAE')).toMatchObject({ amount: 1930 });
    expect(p.servingWeightG).toBe(300);
    expect(p.perServing?.find((n) => n.code === 'PROT')?.amount).toBe(11.4175);
  });

  it('AC-REC-008 メニューの記録が 100% を超えても保存でき、警告を返す', () => {
    // 豚汁(id=1)は 4.5杯分が記録済み。さらに 5 杯を記録すると超過する。
    const res = call('POST', '/api/meal-records', {
      eatenOn: todayString(), mealType: 'dinner', memo: null,
      items: [{ menuId: 1, inputMode: 'SERVING', inputValue: 5 }],
    });
    expect(res.status).toBe(201);
    const saved = res.body as MealRecordSaved;
    expect(saved.warnings[0]?.code).toBe('MENU_OVER_100PCT');
  });

  it('入力不正は 400 VALIDATION_ERROR と details を返す', () => {
    try {
      call('POST', '/api/meal-records', { eatenOn: todayString(), mealType: 'lunch', memo: null, items: [{ menuId: 1, inputMode: 'SERVING', inputValue: 0 }] });
      expect.unreachable();
    } catch (e) {
      expect(e).toBeInstanceOf(MockHttpError);
      const err = e as MockHttpError;
      expect(err.status).toBe(400);
      expect(err.body.code).toBe('VALIDATION_ERROR');
      expect(err.body.details[0]?.field).toBe('items[0].inputValue');
    }
  });

  it('AC-FOOD-001 カナでも検索できる（にんじん / ニンジン / 人参）', () => {
    for (const q of ['にんじん', 'ニンジン', '人参']) {
      const res = call('GET', '/api/foods', undefined, `q=${encodeURIComponent(q)}`);
      const names = (res.body as { items: { name: string }[] }).items.map((i) => i.name);
      expect(names).toContain('にんじん');
    }
  });

  it('未登録のプロフィールは 404 を返す', () => {
    resetState(false);
    try {
      call('GET', '/api/profile');
      expect.unreachable();
    } catch (e) {
      expect((e as MockHttpError).status).toBe(404);
    }
  });
});
