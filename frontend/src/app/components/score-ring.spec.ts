import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { ScoreRing } from './score-ring';

function render(view: { value: number | null; band: 'GOOD' | 'FAIR' | 'LOW' | null; insufficientData: boolean; hasIncomplete: boolean }) {
  const fixture = TestBed.createComponent(ScoreRing);
  fixture.componentRef.setInput('label', '美肌');
  fixture.componentRef.setInput('gid', 'test');
  fixture.componentRef.setInput('view', view);
  return fixture;
}

describe('ScoreRing', () => {
  it('スコアと帯の文言を表示し、読み上げ用ラベルを持つ', async () => {
    const f = render({ value: 81, band: 'GOOD', insufficientData: false, hasIncomplete: false });
    await f.whenStable();
    const el = f.nativeElement as HTMLElement;
    expect(el.textContent).toContain('81');
    expect(el.textContent).toContain('良好');
    expect(el.querySelector('figure')?.getAttribute('aria-label')).toBe('美肌 81点 良好');
  });

  it('データ不足ではスコアを出さず「データ不足」と表示する', async () => {
    const f = render({ value: null, band: null, insufficientData: true, hasIncomplete: false });
    await f.whenStable();
    const el = f.nativeElement as HTMLElement;
    expect(el.textContent).toContain('データ不足');
    expect(el.textContent).not.toMatch(/\d{2}/);
  });

  it('未測定を含む場合は注記を出す', async () => {
    const f = render({ value: 60, band: 'FAIR', insufficientData: false, hasIncomplete: true });
    await f.whenStable();
    expect((f.nativeElement as HTMLElement).textContent).toContain('未測定');
  });
});
