// SCR-06 ダッシュボード（日／7日平均）（FR-040〜043, 046, 047）
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardApi } from '../../api/dashboard.api';
import { toApiError } from '../../api/api-client';
import { DailyDashboard, DashboardNotice, NutrientStatusView, PfcView, RemainingMenu, Scores, WeeklyDashboard } from '../../types/api';
import { fmtNum } from '../../services/format';
import { addDays, formatShort, todayString } from '../../services/date-util';
import { DateNav } from '../../components/date-nav/date-nav';
import { PageHeader } from '../../components/page-header/page-header';
import { ScoreRing } from '../../components/score-ring/score-ring';
import { PfcBar } from '../../components/pfc-bar/pfc-bar';
import { NutrientBar } from '../../components/nutrient-bar/nutrient-bar';
import { EmptyState } from '../../components/empty-state/empty-state';
import { Disclaimer } from '../../components/disclaimer/disclaimer';
import { Icon } from '../../components/icon/icon';

type Mode = 'daily' | 'weekly';

interface Vm {
  scores: Scores;
  energy: { intake: number; target: number } | null;
  pfc: PfcView | null;
  nutrients: NutrientStatusView[];
  remaining: RemainingMenu[];
  notices: DashboardNotice[];
  hasData: boolean;
  recordDays: number | null;
  insufficient: boolean;
}

const ORDER: Record<string, number> = { OVER_UL: 0, DEFICIENT: 1, ACHIEVED: 2, NO_TARGET: 3 };

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, DateNav, PageHeader, ScoreRing, PfcBar, NutrientBar, EmptyState, Disclaimer, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'page' },
  templateUrl: './dashboard.html',
})
export class DashboardPage implements OnInit {
  private readonly api = inject(DashboardApi);

  protected readonly Math = Math;
  protected readonly fmtNum = fmtNum;
  protected readonly mode = signal<Mode>('daily');
  protected readonly date = signal(todayString());
  protected readonly dailyData = signal<DailyDashboard | null>(null);
  protected readonly weeklyData = signal<WeeklyDashboard | null>(null);
  protected readonly error = signal('');
  private names = new Map<string, string>();

  protected readonly isToday = computed(() => this.date() === todayString());
  protected readonly windowLabel = computed(() => `直近7日（${formatShort(addDays(this.date(), -6))}〜${formatShort(this.date())}）`);

  protected readonly vm = computed<Vm | null>(() => {
    if (this.mode() === 'daily') {
      const d = this.dailyData();
      if (!d) return null;
      return {
        scores: d.scores,
        energy: d.hasRecords ? { intake: d.energy.intakeKcal, target: d.energy.targetKcal } : null,
        pfc: d.hasRecords ? d.pfc : null,
        nutrients: d.nutrients, remaining: d.remainingMenus, notices: d.notices, hasData: d.hasRecords, recordDays: null, insufficient: false,
      };
    }
    const w = this.weeklyData();
    if (!w) return null;
    return {
      scores: w.scores,
      energy: w.averages.energy ? { intake: w.averages.energy.intakeKcal, target: w.averages.energy.targetKcal } : null,
      pfc: w.averages.pfc,
      nutrients: w.averages.nutrients, remaining: w.remainingMenus, notices: w.notices, hasData: w.recordDays > 0,
      recordDays: w.recordDays, insufficient: w.insufficientData,
    };
  });

  protected readonly sorted = computed(() =>
    (this.vm()?.nutrients ?? [])
      .filter((n) => !n.isReference)
      .sort((a, b) => (ORDER[a.status] ?? 9) - (ORDER[b.status] ?? 9) || (a.achievementRate ?? 1) - (b.achievementRate ?? 1)),
  );
  protected readonly reference = computed(() => (this.vm()?.nutrients ?? []).filter((n) => n.isReference));

  ngOnInit(): void {
    void this.load();
  }

  protected kcal(v: number): string {
    return Math.round(v).toLocaleString('ja-JP');
  }

  protected energyFill(r: number): string {
    return r < 0.9 ? 'fill-deficient' : r <= 1.1 ? 'fill-achieved' : 'fill-over';
  }

  protected incNames(codes: string[]): string {
    const list = this.vm()?.nutrients ?? [];
    this.names = new Map(list.map((n) => [n.code, n.nameJa]));
    return codes.map((c) => this.names.get(c) ?? c).join('・');
  }

  protected setMode(m: Mode): void {
    this.mode.set(m);
    void this.load();
  }

  protected setDate(d: string): void {
    this.date.set(d);
    void this.load();
  }

  protected async load(): Promise<void> {
    this.error.set('');
    const date = this.date();
    const mode = this.mode();
    try {
      if (mode === 'daily') this.dailyData.set(await this.api.daily(date));
      else this.weeklyData.set(await this.api.weekly(date));
    } catch (e) {
      this.error.set(toApiError(e).message);
    }
  }
}
