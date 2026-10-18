// 栄養素バーの目標表示。実 API の target は、下限型（kind / amount）と上限（max / ul）を別々に持つ（docs/09.API仕様.md API-17）。
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { NutrientStatusView } from '../../types/api';
import { NutrientBar } from './nutrient-bar';

const base: NutrientStatusView = {
  code: 'NACL_EQ',
  nameJa: '食塩相当量',
  unit: 'g',
  intake: 5,
  target: { kind: null, amount: null, min: null, max: null, ul: null, basis: 'PER_DAY' },
  achievementRate: null,
  status: 'NO_TARGET',
  isIncomplete: false,
  isReference: false,
};

function render(n: NutrientStatusView): string {
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const fixture = TestBed.createComponent(NutrientBar);
  fixture.componentRef.setInput('n', n);
  fixture.detectChanges();
  return (fixture.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ');
}

describe('NutrientBar の目標表示', () => {
  it('下限型の目標があれば「目標 ○○（達成%）」を出す', () => {
    const text = render({ ...base, intake: 50, status: 'DEFICIENT', target: { ...base.target, kind: 'RDA', amount: 100 }, achievementRate: 0.5 });

    expect(text).toContain('目標 100.0g （50%）');
    expect(text).not.toContain('目標は未設定');
  });

  it('下限型がなく上限（DG_MAX）だけのとき（食塩など）は「上限 ○○」を出し、未設定とは言わない', () => {
    const text = render({ ...base, status: 'ACHIEVED', target: { ...base.target, max: 7 } });

    expect(text).toContain('上限 7.0g');
    expect(text).not.toContain('目標は未設定');
    expect(text).not.toContain('%');
  });

  it('下限型の目標と耐容上限量（UL）の両方があれば、目標に加えて上限も出す', () => {
    const text = render({ ...base, code: 'ZN', status: 'ACHIEVED', target: { ...base.target, kind: 'RDA', amount: 10, ul: 35 }, achievementRate: 1 });

    expect(text).toContain('目標 10.0g');
    expect(text).toContain('上限 35.0g');
  });

  it('目標がなければ「目標は未設定」、参考値なら「参考値（スコア外）」を出す', () => {
    expect(render(base)).toContain('目標は未設定');
    TestBed.resetTestingModule();
    expect(render({ ...base, isReference: true })).toContain('参考値（スコア外）');
  });

  it('期間内に上限を超えた日があれば、その日数を出す（7 日平均）', () => {
    const text = render({ ...base, status: 'OVER_UL', target: { ...base.target, ul: 35 }, ulExceededDays: 2 });

    expect(text).toContain('期間内に上限を超えた日: 2日');
  });
});
