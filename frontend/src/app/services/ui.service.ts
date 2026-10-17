import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  kind: 'success' | 'error' | 'info';
  text: string;
}

export interface ConfirmRequest {
  title: string;
  message: string;
  okLabel: string;
  danger: boolean;
  resolve: (ok: boolean) => void;
}

/** トーストと確認ダイアログ。 */
@Injectable({ providedIn: 'root' })
export class UiService {
  readonly toasts = signal<Toast[]>([]);
  readonly confirmRequest = signal<ConfirmRequest | null>(null);
  private seq = 1;

  toast(text: string, kind: Toast['kind'] = 'success'): void {
    const id = this.seq++;
    this.toasts.update((l) => [...l, { id, kind, text }]);
    setTimeout(() => this.dismiss(id), kind === 'error' ? 6000 : 3200);
  }

  error(text: string): void {
    this.toast(text, 'error');
  }

  dismiss(id: number): void {
    this.toasts.update((l) => l.filter((t) => t.id !== id));
  }

  confirm(opts: { title: string; message: string; okLabel?: string; danger?: boolean }): Promise<boolean> {
    return new Promise((resolve) => {
      this.confirmRequest.set({
        title: opts.title,
        message: opts.message,
        okLabel: opts.okLabel ?? 'OK',
        danger: opts.danger ?? false,
        resolve: (ok) => {
          this.confirmRequest.set(null);
          resolve(ok);
        },
      });
    });
  }
}
