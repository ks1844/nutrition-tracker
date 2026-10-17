// SCR-04 メニュー一覧（FR-025, 026, 027）
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MenusApi } from '../../api/menus.api';
import { toApiError } from '../../api/api-client';
import { MenuSummary } from '../../types/api';
import { UiService } from '../../services/ui.service';
import { fmtNum } from '../../services/format';
import { formatJa } from '../../services/date-util';
import { PageHeader } from '../../components/page-header/page-header';
import { EmptyState } from '../../components/empty-state/empty-state';
import { Disclaimer } from '../../components/disclaimer/disclaimer';
import { Icon } from '../../components/icon/icon';

@Component({
  selector: 'app-menus-page',
  imports: [RouterLink, PageHeader, EmptyState, Disclaimer, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './menus.html',
})
export class MenusPage implements OnInit {
  private readonly api = inject(MenusApi);
  private readonly ui = inject(UiService);
  private readonly router = inject(Router);

  protected readonly Math = Math;
  protected readonly fmtNum = fmtNum;
  protected readonly menus = signal<MenuSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal('');

  ngOnInit(): void {
    void this.load();
  }

  protected fmt(d: string): string {
    return formatJa(d);
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      this.menus.set(await this.api.list());
    } catch (e) {
      this.error.set(toApiError(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  protected async duplicate(m: MenuSummary): Promise<void> {
    try {
      const copy = await this.api.duplicate(m.id);
      this.ui.toast(`「${copy.name}」を作成しました。内容を確認してください`);
      await this.router.navigate(['/menus', copy.id, 'edit']);
    } catch (e) {
      this.ui.error(toApiError(e).message);
    }
  }

  protected async remove(m: MenuSummary): Promise<void> {
    const ok = await this.ui.confirm({
      title: 'メニューを削除', message: `「${m.name}」を削除します。過去の記録は残ります。`, okLabel: '削除する', danger: true,
    });
    if (!ok) return;
    try {
      await this.api.remove(m.id);
      this.ui.toast('メニューを削除しました');
      await this.load();
    } catch (e) {
      this.ui.error(toApiError(e).message);
    }
  }
}
