import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DISCLAIMER } from '../../services/format';
import { environment } from '../../../environments/environment';
import { Icon } from '../icon/icon';

/** 免責表示（FR-047）。プロトタイプでは、ダミーデータである旨も併記する。 */
@Component({
  selector: 'app-disclaimer',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './disclaimer.html',
})
export class Disclaimer {
  protected readonly text = DISCLAIMER;
  protected readonly mock = environment.useMockApi;
}
