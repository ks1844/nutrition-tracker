import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from './icon';

@Component({
  selector: 'app-page-header',
  imports: [RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="flex items-center justify-between gap-2 px-1 pt-1">
      <div class="flex min-w-0 items-center gap-1.5">
        @if (back(); as b) {
          <a [routerLink]="b" class="btn btn-sm !min-h-10 !w-10 !p-0" aria-label="戻る"><app-icon name="chevron-left" /></a>
        }
        <div class="min-w-0">
          <h1 class="h-title m-0 truncate">{{ title() }}</h1>
          @if (subtitle(); as s) { <p class="muted m-0 text-[0.8rem] font-bold">{{ s }}</p> }
        </div>
      </div>
      <div class="flex shrink-0 items-center gap-2"><ng-content /></div>
    </header>
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly back = input<string | string[] | null>(null);
}
