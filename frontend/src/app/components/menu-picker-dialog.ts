import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MenusApi } from '../api/menus.api';
import { toApiError } from '../api/api-client';
import { MenuSummary } from '../types/api';
import { fmtNum } from '../services/format';
import { matchesSearch } from '../services/text-normalize';
import { EmptyState } from './empty-state';
import { Icon } from './icon';

/** メニュー選択ダイアログ（食材検索ダイアログと同じく、文字を入力して絞り込める）。 */
@Component({
  selector: 'app-menu-picker-dialog',
  imports: [RouterLink, EmptyState, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fixed inset-0 z-[55] flex items-end justify-center bg-indigo-950/25 backdrop-blur-sm sm:items-center sm:p-4" (click)="closed.emit()">
      <section class="glass-strong pop-in flex max-h-[88vh] w-full max-w-lg flex-col overflow-hidden !rounded-b-none sm:!rounded-b-3xl"
               role="dialog" aria-modal="true" aria-labelledby="mpd-title" (click)="$event.stopPropagation()">
        <header class="flex items-center justify-between gap-2 px-4 pt-4 pb-2">
          <h2 id="mpd-title" class="h-sub m-0">メニューを選ぶ</h2>
          <button type="button" class="btn btn-sm !w-10 !p-0" (click)="closed.emit()" aria-label="閉じる"><app-icon name="x" /></button>
        </header>
        <div class="px-4 pb-2">
          <label class="field-label" for="mpd-q">メニュー名で検索</label>
          <div class="relative">
            <app-icon name="search" [size]="18" class="muted pointer-events-none absolute top-1/2 left-3 -translate-y-1/2" />
            <input id="mpd-q" class="field !pl-10" type="search" autocomplete="off" placeholder="メニュー名の一部を入力（かな・カナは区別しません）"
                   [value]="q()" (input)="q.set($any($event.target).value)" />
          </div>
        </div>
        <div class="min-h-0 flex-1 overflow-y-auto px-4 pb-4" aria-live="polite">
          @if (error()) {
            <p class="field-error">{{ error() }}</p>
          } @else if (loading()) {
            <p class="muted my-4 text-[0.9rem] font-bold">読み込み中…</p>
          } @else if (all().length === 0) {
            <app-empty-state icon="pot" title="メニューがありません" text="先にメニューを作成しましょう">
              <a routerLink="/menus/new" class="btn btn-primary btn-sm">メニューを作る</a>
            </app-empty-state>
          } @else if (filtered().length === 0) {
            <p class="my-4 text-[0.9rem] font-bold">「{{ q() }}」に一致するメニューがありません。</p>
          } @else {
            <ul class="m-0 flex list-none flex-col gap-2 p-0">
              @for (m of filtered(); track m.id) {
                <li>
                  <button type="button" class="glass-inset flex w-full items-center justify-between gap-2 px-3.5 py-3 text-left" (click)="picked.emit(m)">
                    <span class="min-w-0">
                      <span class="block truncate text-[0.95rem] font-extrabold">{{ m.name }}</span>
                      <span class="muted tabnum block text-[0.78rem] font-bold">残り {{ fmtNum(m.remaining.servings, 1) }}杯（{{ Math.round(m.remaining.ratio * 100) }}%）</span>
                    </span>
                    <app-icon name="plus" />
                  </button>
                </li>
              }
            </ul>
          }
        </div>
      </section>
    </div>
  `,
})
export class MenuPickerDialog implements OnInit {
  readonly picked = output<MenuSummary>();
  readonly closed = output<void>();

  private readonly api = inject(MenusApi);
  protected readonly Math = Math;
  protected readonly fmtNum = fmtNum;
  protected readonly q = signal('');
  protected readonly all = signal<MenuSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly filtered = computed(() => this.all().filter((m) => matchesSearch(m.name, this.q())));

  async ngOnInit(): Promise<void> {
    try {
      this.all.set(await this.api.list());
    } catch (e) {
      this.error.set(toApiError(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.closed.emit();
  }
}
