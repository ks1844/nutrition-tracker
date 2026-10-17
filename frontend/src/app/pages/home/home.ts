// SCR-02 ホーム（今日）（FR-027, 036, 037, 040〜042, 046, 047）
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardApi } from '../../api/dashboard.api';
import { MealRecordsApi } from '../../api/meal-records.api';
import { toApiError } from '../../api/api-client';
import { DailyDashboard, MealRecordView, RemainingMenu } from '../../types/api';
import { UiService } from '../../services/ui.service';
import { MEAL_LABEL, MEAL_ORDER, describeItem, fmtNum, guessMealType, itemKcal } from '../../services/format';
import { addDays, todayString } from '../../services/date-util';
import { DateNav } from '../../components/date-nav/date-nav';
import { PageHeader } from '../../components/page-header/page-header';
import { ScoreRing } from '../../components/score-ring/score-ring';
import { PfcBar } from '../../components/pfc-bar/pfc-bar';
import { EmptyState } from '../../components/empty-state/empty-state';
import { Disclaimer } from '../../components/disclaimer/disclaimer';
import { Icon, IconName } from '../../components/icon/icon';

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, DateNav, PageHeader, ScoreRing, PfcBar, EmptyState, Disclaimer, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'page' },
  templateUrl: './home.html',
})
export class HomePage implements OnInit {
  private readonly dashApi = inject(DashboardApi);
  private readonly recApi = inject(MealRecordsApi);
  private readonly ui = inject(UiService);

  protected readonly Math = Math;
  protected readonly fmtNum = fmtNum;
  protected readonly date = signal(todayString());
  protected readonly dash = signal<DailyDashboard | null>(null);
  protected readonly records = signal<MealRecordView[]>([]);
  protected readonly error = signal('');
  protected readonly loadingRecords = signal(true);

  protected readonly isToday = computed(() => this.date() === todayString());
  protected readonly pct = computed(() => Math.round((this.dash()?.energy.achievementRate ?? 0) * 100));
  protected readonly energyText = computed(() => {
    const d = this.dash();
    if (!d || !d.hasRecords) return '記録なし';
    const r = d.energy.achievementRate;
    return r < 0.9 ? `あと ${this.kcal(d.energy.targetKcal - d.energy.intakeKcal)} kcal` : r <= 1.1 ? 'ちょうど良い' : `${this.kcal(d.energy.intakeKcal - d.energy.targetKcal)} kcal 多め`;
  });
  protected readonly energyBadge = computed(() => {
    const r = this.dash()?.energy.achievementRate ?? 0;
    return !this.dash()?.hasRecords ? 'st-none' : r < 0.9 ? 'st-deficient' : r <= 1.1 ? 'st-achieved' : 'st-over';
  });
  protected readonly energyIcon = computed<IconName>(() => {
    const r = this.dash()?.energy.achievementRate ?? 0;
    return !this.dash()?.hasRecords ? 'info' : r < 0.9 ? 'arrow-down' : r <= 1.1 ? 'check' : 'arrow-up';
  });
  protected readonly energyFill = computed(() => {
    const r = this.dash()?.energy.achievementRate ?? 0;
    return r < 0.9 ? 'fill-deficient' : r <= 1.1 ? 'fill-achieved' : 'fill-over';
  });
  protected readonly groups = computed(() =>
    [...this.records()]
      .sort((a, b) => MEAL_ORDER.indexOf(a.mealType) - MEAL_ORDER.indexOf(b.mealType) || a.id - b.id)
      .map((r) => ({
        id: r.id,
        label: MEAL_LABEL[r.mealType],
        lines: r.items.map(describeItem),
        kcal: r.items.reduce((s, it) => s + itemKcal(it), 0),
      })),
  );

  ngOnInit(): void {
    void this.load();
  }

  protected kcal(v: number): string {
    return Math.round(v).toLocaleString('ja-JP');
  }

  protected setDate(d: string): void {
    this.date.set(d);
    void this.load();
  }

  protected async load(): Promise<void> {
    this.error.set('');
    this.loadingRecords.set(true);
    const date = this.date();
    try {
      const [dash, recs] = await Promise.all([this.dashApi.daily(date), this.recApi.ofDay(date)]);
      if (date !== this.date()) return;
      this.dash.set(dash);
      this.records.set(recs.records);
    } catch (e) {
      this.error.set(toApiError(e).message);
    } finally {
      this.loadingRecords.set(false);
    }
  }

  protected async removeRecord(id: number, label: string): Promise<void> {
    if (!(await this.ui.confirm({ title: '記録を削除', message: `${label}の記録を削除します。よろしいですか？`, okLabel: '削除する', danger: true }))) return;
    try {
      await this.recApi.remove(id);
      this.ui.toast('記録を削除しました');
      await this.load();
    } catch (e) {
      this.ui.error(toApiError(e).message);
    }
  }

  /** FR-036：前日の記録をコピー。コピー先に記録があれば確認する（409）。 */
  protected async copyPrev(): Promise<void> {
    const to = this.date();
    const from = addDays(to, -1);
    try {
      await this.recApi.copyDay(from, to, false);
    } catch (e) {
      const err = toApiError(e);
      if (err.code === 'CONFLICT') {
        if (!(await this.ui.confirm({ title: '記録を追加', message: 'この日にはすでに記録があります。前日の記録を追加しますか？', okLabel: '追加する' }))) return;
        try {
          await this.recApi.copyDay(from, to, true);
        } catch (e2) {
          this.ui.error(toApiError(e2).message);
          return;
        }
      } else {
        this.ui.error(err.details[0]?.reason ?? err.message);
        return;
      }
    }
    this.ui.toast('前日の記録をコピーしました');
    await this.load();
  }

  /** FR-037：残量カードから 1 杯分を追加記録。 */
  protected async addOne(m: RemainingMenu): Promise<void> {
    try {
      const saved = await this.recApi.create({
        eatenOn: this.date(), mealType: guessMealType(), memo: null,
        items: [{ menuId: m.menuId, inputMode: 'SERVING', inputValue: 1 }],
      });
      const w = saved.warnings[0];
      if (w) this.ui.toast(w.message, 'info');
      else this.ui.toast(`${m.name}を1杯、記録しました`);
      await this.load();
    } catch (e) {
      this.ui.error(toApiError(e).message);
    }
  }

}
