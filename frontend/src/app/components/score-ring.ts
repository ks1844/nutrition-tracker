import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ScoreView } from '../types/api';
import { BAND_META } from '../services/format';
import { Icon, IconName } from './icon';

const CIRC = 2 * Math.PI * 42;

@Component({
  selector: 'app-score-ring',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <figure class="m-0 flex flex-col items-center gap-1.5" role="img" [attr.aria-label]="aria()">
      <div class="relative" [style.width.px]="size()" [style.height.px]="size()">
        <svg viewBox="0 0 100 100" class="h-full w-full -rotate-90">
          <defs>
            <linearGradient [attr.id]="gid()" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" [attr.stop-color]="from()" />
              <stop offset="100%" [attr.stop-color]="to()" />
            </linearGradient>
          </defs>
          <circle cx="50" cy="50" r="42" fill="none" stroke="rgba(255,255,255,0.65)" stroke-width="10" />
          <circle cx="50" cy="50" r="42" fill="none" stroke="rgba(120,120,180,0.14)" stroke-width="10" />
          @if (value() !== null) {
            <circle
              cx="50" cy="50" r="42" fill="none" stroke-width="10" stroke-linecap="round"
              [attr.stroke]="'url(#' + gid() + ')'"
              [attr.stroke-dasharray]="dash()"
              style="transition: stroke-dasharray 0.7s ease; filter: drop-shadow(0 2px 4px rgba(80,70,200,0.35))"
            />
          }
        </svg>
        <div class="absolute inset-0 flex flex-col items-center justify-center">
          <app-icon [name]="icon()" [size]="16" class="muted" />
          @if (value() !== null) {
            <span class="tabnum text-[1.7rem] leading-none font-extrabold">{{ value() }}</span>
          } @else {
            <span class="text-[1.4rem] leading-none font-extrabold muted">—</span>
          }
        </div>
      </div>
      <figcaption class="flex flex-col items-center gap-1 text-center">
        <span class="text-[0.85rem] font-extrabold">{{ label() }}</span>
        @if (band(); as b) {
          <span class="badge" [class]="b.badge"><app-icon [name]="b.icon" [size]="13" />{{ b.label }}</span>
        } @else {
          <span class="badge st-none">{{ view().insufficientData ? 'データ不足' : '記録なし' }}</span>
        }
        @if (view().hasIncomplete && value() !== null) {
          <span class="text-[0.7rem] font-bold muted">※未測定を含む</span>
        }
      </figcaption>
    </figure>
  `,
})
export class ScoreRing {
  readonly label = input.required<string>();
  readonly icon = input<IconName>('sparkle');
  readonly view = input.required<ScoreView>();
  readonly from = input('#f472b6');
  readonly to = input('#8b5cf6');
  readonly gid = input.required<string>();
  readonly size = input(104);

  protected readonly value = computed(() => this.view().value);
  protected readonly band = computed(() => {
    const b = this.view().band;
    return b ? BAND_META[b] : null;
  });
  protected readonly dash = computed(() => {
    const v = Math.min(Math.max(this.value() ?? 0, 0), 100);
    return `${(CIRC * v) / 100} ${CIRC}`;
  });
  protected readonly aria = computed(() => {
    const v = this.view();
    if (v.value === null) return `${this.label()}：${v.insufficientData ? 'データ不足' : '記録なし'}`;
    return `${this.label()} ${v.value}点 ${v.band ? BAND_META[v.band].label : ''}`;
  });
}
