import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ScoreView } from '../../types/api';
import { BAND_META } from '../../services/format';
import { Icon, IconName } from '../icon/icon';

const CIRC = 2 * Math.PI * 42;

@Component({
  selector: 'app-score-ring',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './score-ring.html',
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
