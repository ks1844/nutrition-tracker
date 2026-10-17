import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Icon, IconName } from '../icon/icon';

@Component({
  selector: 'app-bottom-nav',
  imports: [RouterLink, RouterLinkActive, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bottom-nav.html',
  styleUrl: './bottom-nav.css',
})
export class BottomNav {
  protected readonly tabs: { path: string; label: string; icon: IconName; exact: boolean }[] = [
    { path: '/', label: 'ホーム', icon: 'home', exact: true },
    { path: '/record', label: '記録', icon: 'pencil', exact: false },
    { path: '/menus', label: 'メニュー', icon: 'pot', exact: false },
    { path: '/dashboard', label: '分析', icon: 'chart', exact: false },
    { path: '/profile', label: '設定', icon: 'user', exact: false },
  ];
}
