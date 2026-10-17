// SCR-08 栄養素詳細（寄与食材）（FR-042, 044）
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { DashboardApi } from '../../api/dashboard.api';
import { NutrientsApi } from '../../api/nutrients.api';
import { toApiError } from '../../api/api-client';
import { Contributions, NutrientStatusView } from '../../types/api';
import { NutrientCatalog } from '../../services/nutrient-catalog';
import { fmtAmount } from '../../services/format';
import { addDays, formatShort, todayString } from '../../services/date-util';
import { PageHeader } from '../../components/page-header/page-header';
import { NutrientBar } from '../../components/nutrient-bar/nutrient-bar';
import { EmptyState } from '../../components/empty-state/empty-state';
import { Disclaimer } from '../../components/disclaimer/disclaimer';
import { Icon } from '../../components/icon/icon';

type Period = 'daily' | 'weekly';

@Component({
  selector: 'app-nutrient-detail-page',
  imports: [PageHeader, NutrientBar, EmptyState, Disclaimer, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'page' },
  templateUrl: './nutrient-detail.html',
})
export class NutrientDetailPage implements OnInit {
  readonly code = input.required<string>();

  private readonly nutrientsApi = inject(NutrientsApi);
  private readonly dashApi = inject(DashboardApi);
  private readonly catalog = inject(NutrientCatalog);

  protected readonly period = signal<Period>('daily');
  protected readonly data = signal<Contributions | null>(null);
  protected readonly status = signal<NutrientStatusView | null>(null);
  protected readonly error = signal('');
  protected readonly open = signal<Set<string>>(new Set());
  private readonly today = todayString();

  protected readonly name = computed(() => this.catalog.def(this.code())?.nameJa ?? this.code());
  protected readonly unit = computed(() => this.catalog.def(this.code())?.unit ?? '');
  protected readonly range = computed(() => (this.period() === 'daily' ? '今日' : `${formatShort(addDays(this.today, -6))}〜${formatShort(this.today)}`));

  async ngOnInit(): Promise<void> {
    await this.catalog.load();
    await this.load();
  }

  protected fmt(v: number): string {
    return fmtAmount(v, this.unit());
  }

  protected setPeriod(p: Period): void {
    this.period.set(p);
    void this.load();
  }

  protected toggle(key: string): void {
    this.open.update((s) => {
      const n = new Set(s);
      if (n.has(key)) n.delete(key);
      else n.add(key);
      return n;
    });
  }

  protected async load(): Promise<void> {
    this.error.set('');
    const from = this.period() === 'daily' ? this.today : addDays(this.today, -6);
    try {
      const [c, st] = await Promise.all([
        this.nutrientsApi.contributions(this.code(), from, this.today),
        this.period() === 'daily'
          ? this.dashApi.daily(this.today).then((d) => d.nutrients)
          : this.dashApi.weekly(this.today).then((w) => w.averages.nutrients),
      ]);
      this.data.set(c);
      this.status.set(st.find((n) => n.code === this.code()) ?? null);
    } catch (e) {
      this.error.set(toApiError(e).message);
    }
  }
}
