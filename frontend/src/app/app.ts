import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BottomNav } from './components/bottom-nav/bottom-nav';
import { ConfirmDialog } from './components/confirm-dialog/confirm-dialog';
import { ToastHost } from './components/toast-host/toast-host';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, BottomNav, ToastHost, ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
})
export class App {}
