import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MenusApi } from '../../api/menus.api';
import { toApiError } from '../../api/api-client';
import { MenuSummary } from '../../types/api';
import { fmtNum } from '../../services/format';
import { matchesSearch } from '../../services/text-normalize';
import { EmptyState } from '../empty-state/empty-state';
import { Icon } from '../icon/icon';

/** メニュー選択ダイアログ（食材検索ダイアログと同じく、文字を入力して絞り込める）。 */
@Component({
  selector: 'app-menu-picker-dialog',
  imports: [RouterLink, EmptyState, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './menu-picker-dialog.html',
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
