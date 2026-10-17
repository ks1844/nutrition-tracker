// 検索用の文字列正規化（全角/半角・ひらがな/カタカナ・大文字小文字をそろえる）。

export function kataToHira(s: string): string {
  return s.replace(/[ァ-ヶ]/g, (c) => String.fromCharCode(c.charCodeAt(0) - 0x60));
}

export function hiraToKata(s: string): string {
  return s.replace(/[ぁ-ゖ]/g, (c) => String.fromCharCode(c.charCodeAt(0) + 0x60));
}

/** 比較用の正規形（ひらがなに統一）。 */
export function normalizeForSearch(s: string): string {
  return kataToHira(s.normalize('NFKC').toLowerCase().trim());
}

/** 検索語 `q` が `text` に部分一致するか（かな・カナ・全角半角を区別しない）。空の検索語は常に一致。 */
export function matchesSearch(text: string, q: string): boolean {
  const needle = normalizeForSearch(q);
  return needle === '' || normalizeForSearch(text).includes(needle);
}
