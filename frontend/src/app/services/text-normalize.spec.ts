import { describe, expect, it } from 'vitest';
import { matchesSearch, normalizeForSearch } from './text-normalize';

describe('text-normalize', () => {
  it('ひらがな・カタカナ・全角半角をそろえる', () => {
    expect(normalizeForSearch('ブタジル')).toBe('ぶたじる');
    expect(normalizeForSearch('ＡＢＣ')).toBe('abc');
  });

  it('部分一致で検索できる（空の検索語は常に一致）', () => {
    expect(matchesSearch('豚汁', '豚')).toBe(true);
    expect(matchesSearch('蒸し鶏とブロッコリー', 'ぶろっこりー')).toBe(true);
    expect(matchesSearch('豚汁', '鶏')).toBe(false);
    expect(matchesSearch('豚汁', '  ')).toBe(true);
  });
});
