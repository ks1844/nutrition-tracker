import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { UiService } from '../services/ui.service';
import { Icon } from './icon';

@Component({
  selector: 'app-toast-host',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pointer-events-none fixed inset-x-0 top-3 z-[60] mx-auto flex max-w-md flex-col gap-2 px-4" aria-live="polite">
      @for (t of ui.toasts(); track t.id) {
        <div class="glass-strong pop-in pointer-events-auto flex items-center gap-2 px-4 py-3 text-[0.9rem] font-bold"
             [attr.role]="t.kind === 'error' ? 'alert' : 'status'">
          <app-icon [name]="t.kind === 'error' ? 'alert' : t.kind === 'info' ? 'info' : 'check'" [size]="18"
                    [class]="t.kind === 'error' ? 'text-rose-600' : 'text-emerald-600'" />
          <span class="flex-1">{{ t.text }}</span>
          <button type="button" class="muted" (click)="ui.dismiss(t.id)" aria-label="閉じる"><app-icon name="x" [size]="16" /></button>
        </div>
      }
    </div>
  `,
})
export class ToastHost {
  protected readonly ui = inject(UiService);
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (ui.confirmRequest(); as c) {
      <div class="fixed inset-0 z-[70] grid place-items-center bg-indigo-950/25 p-5 backdrop-blur-sm" (click)="c.resolve(false)" (keydown.escape)="c.resolve(false)">
        <div class="glass-strong pop-in w-full max-w-sm p-5" role="alertdialog" aria-modal="true" aria-labelledby="cf-title" (click)="$event.stopPropagation()">
          <h2 id="cf-title" class="h-sub m-0 flex items-center gap-2"><app-icon [name]="c.danger ? 'alert' : 'info'" [size]="18" />{{ c.title }}</h2>
          <p class="muted mt-2 mb-4 text-[0.9rem] font-semibold">{{ c.message }}</p>
          <div class="flex justify-end gap-2">
            <button type="button" class="btn" (click)="c.resolve(false)">キャンセル</button>
            <button type="button" class="btn" [class]="c.danger ? 'btn-danger' : 'btn-primary'" (click)="c.resolve(true)" autofocus>{{ c.okLabel }}</button>
          </div>
        </div>
      </div>
    }
  `,
})
export class ConfirmDialog {
  protected readonly ui = inject(UiService);
}
