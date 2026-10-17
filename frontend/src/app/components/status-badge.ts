import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NutrientStatus } from '../types/api';
import { STATUS_META } from '../services/format';
import { Icon } from './icon';

/** 状態（不足/達成/上限超過/目標なし）を、色だけでなくアイコンと文言でも示す（NFR-004）。 */
@Component({
  selector: 'app-status-badge',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="meta().badge"><app-icon [name]="meta().icon" [size]="14" />{{ meta().label }}</span>`,
})
export class StatusBadge {
  readonly status = input.required<NutrientStatus>();
  protected readonly meta = computed(() => STATUS_META[this.status()]);
}
