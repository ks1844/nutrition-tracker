import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BottomNav } from './components/bottom-nav';
import { ConfirmDialog, ToastHost } from './components/overlays';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, BottomNav, ToastHost, ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="blob" style="width:22rem;height:22rem;left:-6rem;top:-5rem;background:#ff9ec9" aria-hidden="true"></div>
    <div class="blob" style="width:20rem;height:20rem;right:-7rem;top:18%;background:#8fc2ff;animation-delay:-6s" aria-hidden="true"></div>
    <div class="blob" style="width:24rem;height:24rem;left:20%;bottom:-9rem;background:#8fe8c2;animation-delay:-12s" aria-hidden="true"></div>
    <main class="mx-auto flex min-h-screen w-full max-w-lg flex-col gap-6 px-4 pt-5 pb-28">
      <router-outlet />
    </main>
    <app-bottom-nav />
    <app-toast-host />
    <app-confirm-dialog />
  `,
})
export class App {}
