// SCR-07 週トレンド（FR-043, 045）
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardApi } from '../api/dashboard.api';
import { toApiError } from '../api/api-client';
import { WeeklyDashboard } from '../types/api';
import { addDays, formatJa, formatShort, todayString } from '../services/date-util';
import { DateNav } from '../components/date-nav';
import { PageHeader } from '../components/page-header';
import { KcalChart, ScoreChart } from '../components/trend-charts';
import { EmptyState } from '../components/empty-state';
import { Disclaimer } from '../components/disclaimer';
import { Icon } from '../components/icon';

@Component({
  selector: 'app-trend-page',
  imports: [RouterLink, DateNav, PageHeader, KcalChart, ScoreChart, EmptyState, Disclaimer, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="7日の推移" [subtitle]="label()" [back]="['/dashboard']" />
    <app-date-nav [date]="date()" (changed)="setDate($event)" />

    @if (error()) {
      <div class="glass flex flex-col items-start gap-2 p-4" role="alert">
        <p class="m-0 text-[0.9rem] font-bold">{{ error() }}</p>
        <button type="button" class="btn btn-sm" (click)="load()">再試行</button>
      </div>
    } @else if (w(); as d) {
      @if (d.recordDays === 0) {
        <app-empty-state icon="chart" title="この期間に記録がありません" text="食事を記録すると、推移が表示されます">
          <a routerLink="/record" class="btn btn-primary btn-sm">食事を記録する</a>
        </app-empty-state>
      } @else {
        <section class="glass pop-in p-4" aria-label="エネルギーの推移">
          <h2 class="h-sub m-0 mb-1">エネルギー摂取量（kcal）</h2>
          <app-kcal-chart [series]="d.series" [target]="d.averages.energy?.targetKcal ?? null" />
          <p class="muted m-0 text-[0.72rem] font-semibold">記録のない日は「なし」と表示します（0kcalではありません）。</p>
        </section>
        <section class="glass pop-in p-4" aria-label="スコアの推移" style="animation-delay:.05s">
          <h2 class="h-sub m-0 mb-1">スコアの推移</h2>
          <app-score-chart [series]="d.series" />
          <p class="muted m-0 mt-1 text-[0.72rem] font-semibold">各日の点は「その日だけ」の進捗です。7日平均のスコアは分析画面で見られます。</p>
        </section>
        <section class="glass pop-in p-4" aria-label="日ごとの記録" style="animation-delay:.1s">
          <h2 class="h-sub m-0 mb-2">日ごとの記録</h2>
          <ul class="m-0 flex list-none flex-col gap-1.5 p-0">
            @for (s of d.series; track s.date) {
              <li class="glass-inset flex items-center justify-between gap-2 px-3.5 py-2">
                <span class="tabnum text-[0.88rem] font-extrabold">{{ fmt(s.date) }}</span>
                @if (s.hasRecords) {
                  <span class="tabnum text-[0.85rem] font-bold">{{ s.intakeKcal?.toLocaleString('ja-JP') }}kcal ・ 美肌{{ s.scores?.beauty ?? '—' }} 筋肉{{ s.scores?.muscle ?? '—' }} 基礎{{ s.scores?.base ?? '—' }}</span>
                } @else {
                  <span class="badge st-none">記録なし</span>
                }
              </li>
            }
          </ul>
        </section>
        @if (ulDays(); as u) {
          <p class="badge st-over !whitespace-normal !rounded-xl !px-3 !py-2" role="note"><app-icon name="alert" [size]="14" />食塩相当量が上限を超えた日：{{ u }}日</p>
        }
      }
    }
    <app-disclaimer />
  `,
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
