// AC-NFR-004: 状態を色だけで表現しない（文言とアイコンを併記する）。
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { NutrientStatus } from '../../types/api';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  const cases: [NutrientStatus, string][] = [
    ['DEFICIENT', '不足'],
    ['ACHIEVED', '達成'],
    ['OVER_UL', '上限超過'],
    ['NO_TARGET', '目標なし'],
  ];

  for (const [status, label] of cases) {
    it(`${status} は「${label}」の文言とアイコンを表示する`, async () => {
      const fixture = TestBed.createComponent(StatusBadge);
      fixture.componentRef.setInput('status', status);
      await fixture.whenStable();
      const el = fixture.nativeElement as HTMLElement;
      expect(el.textContent?.trim()).toBe(label);
      expect(el.querySelector('svg')).not.toBeNull();
    });
  }
});
