import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DISCLAIMER } from '../services/format';
import { environment } from '../../environments/environment';
import { Icon } from './icon';

/** 免責表示（FR-047）。プロトタイプでは、ダミーデータである旨も併記する。 */
@Component({
  selector: 'app-disclaimer',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="muted flex gap-2 px-2 pt-1 pb-2 text-[0.75rem] leading-relaxed font-semibold">
      <app-icon name="info" [size]="15" class="mt-0.5 shrink-0" />
      <div>
        <p class="m-0">{{ text }}</p>
        @if (mock) {
          <p class="m-0 mt-0.5">プロトタイプ表示中：食材・栄養値・目標値はダミーデータです（公式の値ではありません）。</p>
        }
      </div>
    </aside>
  `,
})
export class Disclaimer {
  protected readonly text = DISCLAIMER;
  protected readonly mock = environment.useMockApi;
}
