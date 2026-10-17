// 食事記録の自動保存（AC-REC-012）：入力のたびには API を呼ばず、切替・離脱のタイミングでまとめて保存する。
import { HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { mockApiInterceptor } from '../api/mock/mock-api.interceptor';
import { resetState } from '../api/mock/mock-store';
import { addDays, todayString } from '../services/date-util';
import { RecordPage } from './record.page';

describe('RecordPage 自動保存', () => {
  const calls: string[] = [];
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
    resetState(true);
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(withInterceptors([counter, mockApiInterceptor]))] });
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
    fixture.detectChanges();
    expect(other.getAttribute('aria-checked')).toBe('true');
  });

  it('項目がなければ何も保存しない', async () => {
    const fixture = TestBed.createComponent(RecordPage);
    fixture.componentRef.setInput('date', addDays(todayString(), -30));
    fixture.detectChanges();
    expect(await fixture.componentInstance.flush()).toBe(true);
    expect(writes()).toEqual([]);
  });
});
