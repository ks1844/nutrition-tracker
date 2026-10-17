import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Icon, IconName } from './icon';

@Component({
  selector: 'app-bottom-nav',
  imports: [RouterLink, RouterLinkActive, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="glass-strong fixed inset-x-3 bottom-3 z-40 mx-auto max-w-lg !rounded-[1.6rem] px-1.5 py-1.5 pb-[max(0.375rem,env(safe-area-inset-bottom))]" aria-label="メインメニュー">
      <ul class="m-0 grid list-none grid-cols-5 gap-1 p-0">
        @for (t of tabs; track t.path) {
          <li>
            <a [routerLink]="t.path" routerLinkActive="nav-active" [routerLinkActiveOptions]="{ exact: t.exact }"
               class="nav-item flex min-h-[3.25rem] flex-col items-center justify-center gap-0.5 rounded-2xl text-[0.68rem] font-extrabold text-inherit no-underline">
              <app-icon [name]="t.icon" [size]="21" />{{ t.label }}
            </a>
          </li>
        }
      </ul>
    </nav>
  `,
  styles: `
    .nav-item { color: var(--color-ink-soft); transition: background 0.2s, color 0.2s; }
    .nav-active { color: #fff; background: linear-gradient(135deg, #5b4bdb, #2f7bf5); box-shadow: 0 6px 16px rgba(70, 70, 220, 0.35); }
  `,
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
