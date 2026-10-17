// 週トレンドのグラフ共通の寸法（SVG の viewBox 基準）。
export const W = 340;
export const H = 190;
export const PAD = { l: 34, r: 10, t: 14, b: 26 };

/** n 個の点のうち i 番目の x 座標。 */
export function xAt(i: number, n: number): number {
  return PAD.l + (W - PAD.l - PAD.r) * (n === 1 ? 0.5 : i / (n - 1));
}
