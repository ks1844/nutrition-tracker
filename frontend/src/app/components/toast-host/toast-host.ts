import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { UiService } from '../../services/ui.service';
import { Icon } from '../icon/icon';

@Component({
  selector: 'app-toast-host',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './toast-host.html',
})
export class ToastHost {
  protected readonly ui = inject(UiService);
}
