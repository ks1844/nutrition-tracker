import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { UiService } from '../../services/ui.service';
import { Icon } from '../icon/icon';

@Component({
  selector: 'app-confirm-dialog',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirm-dialog.html',
})
export class ConfirmDialog {
  protected readonly ui = inject(UiService);
}
