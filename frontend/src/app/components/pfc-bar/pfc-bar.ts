import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PfcView } from '../../types/api';
import { fmtNum } from '../../services/format';

/** PFC の比率バー。色に加えて、記号（P/F/C）と数値で示す。 */
@Component({
  selector: 'app-pfc-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pfc-bar.html',
})
export class PfcBar {
  readonly pfc = input.required<PfcView | null>();
  protected fmt(v: number): string {
    return fmtNum(v, 1);
  }
}
