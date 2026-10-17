// SCR-07 週トレンド（FR-043, 045）
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardApi } from '../../api/dashboard.api';
import { toApiError } from '../../api/api-client';
import { WeeklyDashboard } from '../../types/api';
import { addDays, formatJa, formatShort, todayString } from '../../services/date-util';
import { DateNav } from '../../components/date-nav/date-nav';
import { PageHeader } from '../../components/page-header/page-header';
import { KcalChart } from '../../components/kcal-chart/kcal-chart';
import { ScoreChart } from '../../components/score-chart/score-chart';
import { EmptyState } from '../../components/empty-state/empty-state';
import { Disclaimer } from '../../components/disclaimer/disclaimer';
import { Icon } from '../../components/icon/icon';

@Component({
  selector: 'app-trend-page',
  imports: [RouterLink, DateNav, PageHeader, KcalChart, ScoreChart, EmptyState, Disclaimer, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'page' },
  templateUrl: './trend.html',
})
export class TrendPage implements OnInit {
  private readonly api = inject(DashboardApi);
  protected readonly date = signal(todayString());
  protected readonly w = signal<WeeklyDashboard | null>(null);
  protected readonly error = signal('');
  protected readonly label = computed(() => `${formatShort(addDays(this.date(), -6))}〜${formatShort(this.date())}`);
  protected readonly ulDays = computed(() => this.w()?.averages.nutrients.find((n) => n.code === 'NACL_EQ')?.ulExceededDays ?? 0);

  ngOnInit(): void {
    void this.load();
  }

  protected fmt(d: string): string {
    return formatJa(d);
  }

  protected setDate(d: string): void {
    this.date.set(d);
    void this.load();
  }

  protected async load(): Promise<void> {
    this.error.set('');
    try {
      this.w.set(await this.api.weekly(this.date()));
    } catch (e) {
      this.error.set(toApiError(e).message);
    }
  }
}
