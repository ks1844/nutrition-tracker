import { describe, expect, it } from 'vitest';
import { addDays, ageOn, formatJa, toDateString } from './date-util';

describe('date-util', () => {
  it('日付を加減算する（月またぎ）', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2026-10-01', -1)).toBe('2026-09-30');
  });

  it('満年齢を計算する', () => {
    expect(ageOn('1996-04-01', '2026-03-31')).toBe(29);
    expect(ageOn('1996-04-01', '2026-04-01')).toBe(30);
  });

  it('曜日付きで表示する', () => {
    expect(formatJa('2026-10-17')).toBe('10/17(土)');
    expect(toDateString(new Date(2026, 0, 5))).toBe('2026-01-05');
  });
});
