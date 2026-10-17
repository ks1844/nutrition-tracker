import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SeriesPoint } from '../../types/api';
import { formatShort } from '../../services/date-util';
import { H, PAD, W } from '../chart-geometry/chart-geometry';

/** kcal の棒グラフ（目標ライン付き）。記録のない日は欠けた棒として表示（0 にしない）。 */
@Component({
  selector: 'app-kcal-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kcal-chart.html',
})
export class KcalChart {
  readonly series = input.required<SeriesPoint[]>();
  readonly target = input<number | null>(null);
  protected readonly w = W;
  protected readonly h = H;
  protected readonly pad = PAD;

  private readonly max = computed(() => {
    const vals = this.series().map((s) => s.intakeKcal ?? 0);
    return Math.max(500, Math.ceil(Math.max(...vals, this.target() ?? 0) / 500) * 500);
  });
  private y(v: number): number {
    return H - PAD.b - ((H - PAD.t - PAD.b) * v) / this.max();
  }
  protected readonly grid = computed(() =>
    [0, 0.5, 1].map((r) => ({ y: this.y(this.max() * r), label: String(Math.round(this.max() * r)) })),
  );
  protected readonly targetY = computed(() => (this.target() ? this.y(this.target() as number) : null));
  protected readonly bars = computed(() => {
    const s = this.series();
    const slot = (W - PAD.l - PAD.r) / s.length;
    return s.map((p, i) => {
      const v = p.intakeKcal ?? 0;
      const y = this.y(v);
      return {
        date: p.date, has: p.hasRecords && p.intakeKcal !== null, value: v,
        x: PAD.l + slot * i + slot * 0.18, w: slot * 0.64, y, h: Math.max(H - PAD.b - y, 0), label: formatShort(p.date),
      };
    });
  });
}
