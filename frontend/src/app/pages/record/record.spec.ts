// 食事記録の自動保存（AC-REC-012）：入力のたびには API を呼ばず、切替・離脱のタイミングでまとめて保存する。
import { HttpInterceptorFn, HttpResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { MealRecordInput, MealRecordSaved, MealRecordView, MenuDetail } from '../../types/api';
import { addDays, todayString } from '../../services/date-util';
import { RecordPage } from './record';

/** 豚汁（2400g・8 杯分）。栄養は ENERGY だけ（画面の kcal 表示に使われる）。 */
const TONJIRU: MenuDetail = {
  id: 1, name: '豚汁', cookedOn: null, memo: null, cookedTotalWeightG: 2400, servings: 8, servingLabel: 'お椀', servingWeightG: 300,
  items: [], nutrients: [{ code: 'ENERGY', amount: 1200, isIncomplete: false }], perServing: [],
  remaining: { ratio: 1, servings: 8, isOver: false },
};

describe('RecordPage 自動保存', () => {
  const calls: string[] = [];
  let stored: MealRecordView[] = [];
  let nextId = 1;

  /** バックエンドの代わりに、必要な API だけ答える。保存された記録はメモリに持つ。 */
  const fakeBackend: HttpInterceptorFn = (req) => {
    const url = new URL(req.url, 'http://localhost');
    const reply = (body: unknown, status = 200) => of(new HttpResponse({ status, body }));
    const toView = (id: number, input: MealRecordInput): MealRecordView => ({
      id, eatenOn: input.eatenOn, mealType: input.mealType, memo: input.memo,
      items: input.items.map((it, i) => ({
        id: id * 100 + i, menuId: it.menuId ?? null, menuName: it.menuId ? '豚汁' : null, foodId: it.foodId ?? null, foodName: null,
        unitId: it.unitId ?? null, inputMode: it.inputMode, inputValue: it.inputValue, portionRatio: null, amountG: null, nutrients: [],
      })),
    });
    if (req.method === 'GET' && url.pathname === '/api/recent-items') return reply({ menus: [], foods: [] });
    if (req.method === 'GET' && url.pathname === '/api/meal-records') return reply({ date: url.searchParams.get('date'), records: stored });
    if (req.method === 'GET' && url.pathname === '/api/menus/1') return reply(TONJIRU);
    if (req.method === 'POST' && url.pathname === '/api/meal-records') {
      const view = toView(nextId++, req.body as MealRecordInput);
      stored.push(view);
      return reply({ ...view, warnings: [] } satisfies MealRecordSaved, 201);
    }
    const id = /^\/api\/meal-records\/(\d+)$/.exec(url.pathname)?.[1];
    if (req.method === 'PUT' && id) {
      const view = toView(Number(id), req.body as MealRecordInput);
      stored = stored.map((r) => (r.id === view.id ? view : r));
      return reply({ ...view, warnings: [] } satisfies MealRecordSaved);
    }
    if (req.method === 'DELETE' && id) {
      stored = stored.filter((r) => r.id !== Number(id));
      return reply(null, 204);
    }
    throw new Error(`想定していない API 呼び出し: ${req.method} ${req.url}`);
  };

  const counter: HttpInterceptorFn = (req, next) => {
    calls.push(`${req.method} ${req.url}`);
    return next(req);
  };
  const writes = (): string[] => calls.filter((c) => /^(POST|PUT|DELETE) /.test(c));

  async function open(): Promise<{ page: RecordPage; el: HTMLElement; fixture: ReturnType<typeof TestBed.createComponent<RecordPage>> }> {
    const fixture = TestBed.createComponent(RecordPage);
    fixture.componentRef.setInput('date', addDays(todayString(), -30)); // 記録のない日
    fixture.componentRef.setInput('menuId', '1'); // 豚汁を最初から追加
    fixture.detectChanges();
    const page = fixture.componentInstance;
    await vi.waitFor(() => expect(page.dirty()).toBe(true), { timeout: 3000 });
    return { page, el: fixture.nativeElement as HTMLElement, fixture };
  }

  const typeValue = (el: HTMLElement, value: string): void => {
    const input = el.querySelector('article input[type=number]') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  };

  beforeEach(() => {
    calls.length = 0;
    stored = [];
    nextId = 1;
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(withInterceptors([counter, fakeBackend]))] });
  });

  it('入力を何度変えても保存の API は呼ばれず、flush で 1 回だけ保存される', async () => {
    const { page, el } = await open();
    expect(writes()).toEqual([]);
    for (const v of ['2', '3', '1.5']) typeValue(el, v);
    expect(writes()).toEqual([]); // 入力のたびには保存しない

    expect(await page.flush()).toBe(true);
    expect(writes()).toEqual(['POST /api/meal-records']); // 作成が 1 回
    expect(page.dirty()).toBe(false);

    expect(await page.flush()).toBe(true);
    expect(writes()).toHaveLength(1); // 変更がなければ呼ばない

    typeValue(el, '1');
    await page.flush();
    expect(writes()).toHaveLength(2);
    expect(writes()[1]).toMatch(/^PUT \/api\/meal-records\/\d+$/); // 2 回目以降は更新
  });

  it('画面を離れるとき（canLeave）に未保存の変更が保存される', async () => {
    const { page } = await open();
    expect(await page.canLeave()).toBe(true);
    expect(writes()).toEqual(['POST /api/meal-records']);
  });

  it('食事区分を変えると、変える前の内容が保存されてから切り替わる', async () => {
    const { page, el, fixture } = await open();
    const radios = [...el.querySelectorAll('[aria-label=食事区分] button')] as HTMLButtonElement[];
    const other = radios.find((b) => b.getAttribute('aria-checked') === 'false') as HTMLButtonElement;
    other.click();
    await vi.waitFor(() => expect(writes()).toEqual(['POST /api/meal-records']), { timeout: 3000 });
    await vi.waitFor(() => expect(page.dirty()).toBe(false), { timeout: 3000 });
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(other.getAttribute('aria-checked')).toBe('true'); // 保存のあと、切替先の食事区分に変わる
    }, { timeout: 3000 });
  });

  it('項目がなければ何も保存しない', async () => {
    const fixture = TestBed.createComponent(RecordPage);
    fixture.componentRef.setInput('date', addDays(todayString(), -30));
    fixture.detectChanges();
    expect(await fixture.componentInstance.flush()).toBe(true);
    expect(writes()).toEqual([]);
  });
});
