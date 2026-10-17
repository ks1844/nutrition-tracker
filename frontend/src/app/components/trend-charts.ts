import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SeriesPoint } from '../types/api';
import { formatShort } from '../services/date-util';

const W = 340;
const H = 190;
const PAD = { l: 34, r: 10, t: 14, b: 26 };

function xAt(i: number, n: number): number {
  return PAD.l + ((W - PAD.l - PAD.r) * (n === 1 ? 0.5 : i / (n - 1)));
}

/** kcal の棒グラフ（目標ライン付き）。記録のない日は欠けた棒として表示（0 にしない）。 */
@Component({
  selector: 'app-kcal-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.viewBox]="'0 0 ' + w + ' ' + h" class="w-full" role="img" aria-label="直近7日のエネルギー摂取量の棒グラフ">
      @for (g of grid(); track g.y) {
        <line [attr.x1]="pad.l" [attr.x2]="w - pad.r" [attr.y1]="g.y" [attr.y2]="g.y" stroke="rgba(100,100,160,0.18)" />
        <text [attr.x]="pad.l - 6" [attr.y]="g.y + 4" text-anchor="end" font-size="10" fill="#4f5a82">{{ g.label }}</text>
      }
      @for (b of bars(); track b.date) {
        @if (b.has) {
          <rect [attr.x]="b.x" [attr.y]="b.y" [attr.width]="b.w" [attr.height]="b.h" rx="6" fill="url(#kcalGrad)" />
          <text [attr.x]="b.x + b.w / 2" [attr.y]="b.y - 4" text-anchor="middle" font-size="9.5" font-weight="700" fill="#1d2340">{{ b.value }}</text>
        } @else {
          <text [attr.x]="b.x + b.w / 2" [attr.y]="h - pad.b - 6" text-anchor="middle" font-size="9" fill="#4f5a82">なし</text>
        }
        <text [attr.x]="b.x + b.w / 2" [attr.y]="h - 8" text-anchor="middle" font-size="10" fill="#4f5a82">{{ b.label }}</text>
      }
      @if (targetY() !== null) {
        <line [attr.x1]="pad.l" [attr.x2]="w - pad.r" [attr.y1]="targetY()" [attr.y2]="targetY()" stroke="#7c3aed" stroke-width="1.6" stroke-dasharray="5 4" />
        <text [attr.x]="w - pad.r" [attr.y]="targetY()! - 4" text-anchor="end" font-size="9.5" font-weight="700" fill="#6d28d9">目標 {{ target() }}</text>
      }
      <defs>
        <linearGradient id="kcalGrad" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stop-color="#a78bfa" />
          <stop offset="100%" stop-color="#60a5fa" />
        </linearGradient>
      </defs>
    </svg>
  `,
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

type Key = 'beauty' | 'muscle' | 'base';

/** スコアの折れ線グラフ。線種とマーカー形状で系列を区別する（色だけに頼らない: NFR-004）。 */
@Component({
  selector: 'app-score-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.viewBox]="'0 0 ' + w + ' ' + h" class="w-full" role="img" aria-label="直近7日のスコア推移の折れ線グラフ">
      @for (g of [0, 50, 100]; track g) {
        <line [attr.x1]="pad.l" [attr.x2]="w - pad.r" [attr.y1]="y(g)" [attr.y2]="y(g)" stroke="rgba(100,100,160,0.18)" />
        <text [attr.x]="pad.l - 6" [attr.y]="y(g) + 4" text-anchor="end" font-size="10" fill="#4f5a82">{{ g }}</text>
      }
      @for (s of lines(); track s.key) {
        @for (seg of s.segments; track $index) {
          <polyline [attr.points]="seg" fill="none" [attr.stroke]="s.color" stroke-width="2.4" stroke-linejoin="round"
                    [attr.stroke-dasharray]="s.dash" />
        }
        @for (p of s.points; track p.date) {
          @switch (s.marker) {
            @case ('circle') { <circle [attr.cx]="p.x" [attr.cy]="p.y" r="4" [attr.fill]="s.color" stroke="#fff" stroke-width="1.5" /> }
            @case ('square') { <rect [attr.x]="p.x - 4" [attr.y]="p.y - 4" width="8" height="8" [attr.fill]="s.color" stroke="#fff" stroke-width="1.5" /> }
            @default { <polygon [attr.points]="tri(p.x, p.y)" [attr.fill]="s.color" stroke="#fff" stroke-width="1.5" /> }
          }
        }
      }
      @for (t of ticks(); track t.x) {
        <text [attr.x]="t.x" [attr.y]="h - 8" text-anchor="middle" font-size="10" fill="#4f5a82">{{ t.label }}</text>
      }
    </svg>
    <ul class="m-0 mt-1 flex list-none flex-wrap justify-center gap-x-4 gap-y-1 p-0 text-[0.78rem] font-bold">
      @for (s of lines(); track s.key) {
        <li class="flex items-center gap-1.5">
          <svg width="30" height="12" aria-hidden="true">
            <line x1="1" x2="29" y1="6" y2="6" [attr.stroke]="s.color" stroke-width="2.4" [attr.stroke-dasharray]="s.dash" />
            @switch (s.marker) {
              @case ('circle') { <circle cx="15" cy="6" r="4" [attr.fill]="s.color" /> }
              @case ('square') { <rect x="11" y="2" width="8" height="8" [attr.fill]="s.color" /> }
              @default { <polygon points="15,1.5 19.5,10 10.5,10" [attr.fill]="s.color" /> }
            }
          </svg>{{ s.label }}
        </li>
      }
    </ul>
  `,
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
