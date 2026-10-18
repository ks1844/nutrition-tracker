import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NutrientStatusView } from '../../types/api';
import { STATUS_META, fmtAmount } from '../../services/format';
import { Icon } from '../icon/icon';
import { StatusBadge } from '../status-badge/status-badge';

/** 栄養素ごとの達成バー。不足=青系、達成=緑系、上限超過=橙系。数値と文言を必ず併記する。 */
@Component({
  selector: 'app-nutrient-bar',
  imports: [RouterLink, StatusBadge, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './nutrient-bar.html',
})
export class NutrientBar {
  readonly n = input.required<NutrientStatusView>();
  protected readonly scale = 1.5;
  protected readonly meta = computed(() => STATUS_META[this.n().status]);
  /** 超えると警告する上限（耐容上限量を優先し、なければ目標量の上限）。 */
  protected readonly limit = computed(() => this.n().target.ul ?? this.n().target.max);
  /** 下限型の目標がなく、上限だけある（食塩など）。この場合は上限を基準に表示する。 */
  protected readonly upper = computed(() => this.n().target.amount === null && this.limit() !== null);
  /** バーと目安の基準にする値（下限型の目標、なければ上限）。 */
  protected readonly reference = computed(() => this.n().target.amount ?? this.limit());
  protected readonly hasTarget = computed(() => this.reference() !== null);
  protected readonly intake = computed(() => fmtAmount(this.n().intake, this.n().unit));
  protected readonly target = computed(() => fmtAmount(this.reference() ?? 0, this.n().unit));
  protected readonly limitText = computed(() => fmtAmount(this.limit() ?? 0, this.n().unit));
  protected readonly pct = computed(() => {
    const t = this.reference();
    return t ? Math.round((this.n().intake / t) * 100) : 0;
  });
  protected readonly fill = computed(() => {
    const t = this.reference();
    if (!t) return 0;
    const ratio = this.n().intake / t;
    return this.upper() ? Math.min(ratio / this.scale, 1) * 100 : Math.min(ratio, 1) * 100;
  });
}
