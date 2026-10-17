import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NutrientStatusView } from '../types/api';
import { STATUS_META, fmtAmount } from '../services/format';
import { Icon } from './icon';
import { StatusBadge } from './status-badge';

/** 栄養素ごとの達成バー。不足=青系、達成=緑系、上限超過=橙系。数値と文言を必ず併記する。 */
@Component({
  selector: 'app-nutrient-bar',
  imports: [RouterLink, StatusBadge, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a [routerLink]="['/nutrients', n().code]" class="glass-inset block px-3.5 py-3 no-underline text-inherit transition hover:bg-white/70">
      <div class="flex items-center justify-between gap-2">
        <span class="text-[0.92rem] font-extrabold">
          {{ n().nameJa }}
          @if (n().isIncomplete && !n().isReference) {
            <span class="muted text-[0.75rem]" title="未測定の食材を含みます">※</span>
          }
        </span>
        <app-status-badge [status]="n().status" />
      </div>
      <div class="mt-1.5 flex items-baseline justify-between gap-2">
        <span class="tabnum text-[1rem] font-extrabold">
          {{ intake() }}<span class="muted ml-0.5 text-[0.75rem] font-bold">{{ n().unit }}</span>
        </span>
        @if (n().target.amount !== null) {
          <span class="tabnum muted text-[0.78rem] font-bold">
            {{ upper() ? '上限' : '目標' }} {{ target() }}{{ n().unit }}
            @if (!upper()) { （{{ pct() }}%） }
          </span>
        } @else {
          <span class="muted text-[0.78rem] font-bold">{{ n().isReference ? '参考値（スコア外）' : '目標は未設定' }}</span>
        }
      </div>
      @if (n().target.amount !== null) {
        <div class="bar-track mt-2" role="progressbar" [attr.aria-valuenow]="pct()" aria-valuemin="0" aria-valuemax="100"
             [attr.aria-label]="n().nameJa + ' ' + meta().label">
          <div class="bar-fill" [class]="meta().fill" [style.width.%]="fill()"></div>
          @if (upper()) {
            <span class="absolute top-0 bottom-0 w-0.5 bg-slate-700/70" [style.left.%]="(1 / scale) * 100" aria-hidden="true"></span>
          }
        </div>
      }
      @if (n().ulExceededDays) {
        <p class="mt-1.5 flex items-center gap-1 text-[0.75rem] font-bold st-over rounded-lg px-2 py-1">
          <app-icon name="alert" [size]="13" />期間内に上限を超えた日: {{ n().ulExceededDays }}日
        </p>
      }
    </a>
  `,
})
export class NutrientBar {
  readonly n = input.required<NutrientStatusView>();
  protected readonly scale = 1.5;
  protected readonly meta = computed(() => STATUS_META[this.n().status]);
  protected readonly upper = computed(() => this.n().target.kind === 'DG_MAX' || this.n().target.kind === 'UL');
  protected readonly intake = computed(() => fmtAmount(this.n().intake, this.n().unit));
  protected readonly target = computed(() => fmtAmount(this.n().target.amount ?? 0, this.n().unit));
  protected readonly pct = computed(() => {
    const t = this.n().target.amount;
    return t ? Math.round((this.n().intake / t) * 100) : 0;
  });
  protected readonly fill = computed(() => {
    const t = this.n().target.amount;
    if (!t) return 0;
    const ratio = this.n().intake / t;
    return this.upper() ? Math.min(ratio / this.scale, 1) * 100 : Math.min(ratio, 1) * 100;
  });
}
