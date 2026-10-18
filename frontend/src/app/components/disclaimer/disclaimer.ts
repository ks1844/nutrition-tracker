import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DISCLAIMER } from '../../services/format';
import { Icon } from '../icon/icon';

/** 免責表示（FR-047）。 */
@Component({
  selector: 'app-disclaimer',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './disclaimer.html',
})
export class Disclaimer {
  protected readonly text = DISCLAIMER;
}
