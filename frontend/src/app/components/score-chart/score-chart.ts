import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SeriesPoint } from '../../types/api';
import { formatShort } from '../../services/date-util';
import { H, PAD, W, xAt } from '../chart-geometry/chart-geometry';

type Key = 'beauty' | 'muscle' | 'base';

/** スコアの折れ線グラフ。線種とマーカー形状で系列を区別する（色だけに頼らない: NFR-004）。 */
@Component({
  selector: 'app-score-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './score-chart.html',
})
export class ScoreChart {
  readonly series = input.required<SeriesPoint[]>();
  protected readonly w = W;
  protected readonly h = H;
  protected readonly pad = PAD;

  protected y(v: number): number {
    return H - PAD.b - ((H - PAD.t - PAD.b) * v) / 100;
  }
  protected tri(x: number, y: number): string {
    return `${x},${y - 5} ${x + 4.6},${y + 3.6} ${x - 4.6},${y + 3.6}`;
  }
  protected readonly ticks = computed(() =>
    this.series().map((s, i) => ({ x: xAt(i, this.series().length), label: formatShort(s.date) })),
  );
  protected readonly lines = computed(() => {
    const defs: { key: Key; label: string; color: string; dash: string | null; marker: 'circle' | 'square' | 'tri' }[] = [
      { key: 'beauty', label: '美肌（実線・●）', color: '#d946ef', dash: null, marker: 'circle' },
      { key: 'muscle', label: '筋肉（破線・■）', color: '#2563eb', dash: '7 4', marker: 'square' },
      { key: 'base', label: '基礎（点線・▲）', color: '#059669', dash: '2 4', marker: 'tri' },
    ];
    const s = this.series();
    return defs.map((d) => {
      const points = s
        .map((p, i) => ({ date: p.date, v: p.scores?.[d.key] ?? null, x: xAt(i, s.length) }))
        .filter((p): p is { date: string; v: number; x: number } => p.v !== null)
        .map((p) => ({ date: p.date, x: p.x, y: this.y(p.v) }));
      // 記録のない日で線を途切れさせる
      const segments: string[] = [];
      let cur: string[] = [];
      s.forEach((p, i) => {
        const v = p.scores?.[d.key] ?? null;
        if (v === null) {
          if (cur.length > 1) segments.push(cur.join(' '));
          cur = [];
        } else cur.push(`${xAt(i, s.length)},${this.y(v)}`);
      });
      if (cur.length > 1) segments.push(cur.join(' '));
      return { ...d, points, segments };
    });
  });
}
