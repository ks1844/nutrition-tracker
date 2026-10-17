// SCR-08 栄養素詳細（寄与食材）（FR-042, 044）
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { DashboardApi } from '../api/dashboard.api';
import { NutrientsApi } from '../api/nutrients.api';
import { toApiError } from '../api/api-client';
import { Contributions, NutrientStatusView } from '../types/api';
import { NutrientCatalog } from '../services/nutrient-catalog';
import { fmtAmount } from '../services/format';
import { addDays, formatShort, todayString } from '../services/date-util';
import { PageHeader } from '../components/page-header';
import { NutrientBar } from '../components/nutrient-bar';
import { EmptyState } from '../components/empty-state';
import { Disclaimer } from '../components/disclaimer';
import { Icon } from '../components/icon';

type Period = 'daily' | 'weekly';

@Component({
  selector: 'app-nutrient-detail-page',
  imports: [PageHeader, NutrientBar, EmptyState, Disclaimer, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="name()" [subtitle]="range()" [back]="['/dashboard']" />

    <div class="glass grid grid-cols-2 gap-1.5 p-1.5" role="group" aria-label="期間">
      <button type="button" class="chip justify-center" [attr.aria-pressed]="period() === 'daily'" (click)="setPeriod('daily')">今日</button>
      <button type="button" class="chip justify-center" [attr.aria-pressed]="period() === 'weekly'" (click)="setPeriod('weekly')">直近7日</button>
    </div>

    @if (error()) {
      <div class="glass flex flex-col items-start gap-2 p-4" role="alert">
        <p class="m-0 text-[0.9rem] font-bold">{{ error() }}</p>
        <button type="button" class="btn btn-sm" (click)="load()">再試行</button>
      </div>
    } @else {
      @if (status(); as s) { <app-nutrient-bar [n]="s" /> }
      @if (data(); as d) {
        @if (d.items.length === 0) {
          <app-empty-state icon="list" title="この期間の記録がありません" text="食事を記録すると、何から摂れているかが分かります" />
        } @else {
          <section class="glass pop-in p-4" aria-label="寄与している食事">
            <h2 class="h-sub m-0">何から摂れているか</h2>
            <p class="muted tabnum m-0 mb-3 text-[0.8rem] font-bold">合計 {{ fmt(d.totalAmount) }}{{ unit() }}@if (d.isIncomplete) { （※未測定の食材を含みます） }</p>
            <ul class="m-0 flex list-none flex-col gap-2 p-0">
              @for (it of d.items; track it.type + it.id) {
                <li class="glass-inset px-3.5 py-3">
                  <div class="flex items-center justify-between gap-2">
                    <span class="min-w-0 truncate text-[0.92rem] font-extrabold">
                      <span class="badge st-none mr-1">{{ it.type === 'MENU' ? 'メニュー' : '食材' }}</span>{{ it.name }}
                    </span>
                    <span class="tabnum shrink-0 text-[0.9rem] font-extrabold">{{ fmt(it.amount) }}{{ unit() }} <span class="muted text-[0.75rem]">（{{ it.sharePct }}%）</span></span>
                  </div>
                  <div class="bar-track mt-1.5 !h-2.5"><div class="bar-fill fill-deficient" style="background:linear-gradient(90deg,#a78bfa,#6366f1)" [style.width.%]="it.sharePct"></div></div>
                  @if (it.children.length) {
                    <button type="button" class="muted mt-1.5 flex items-center gap-1 text-[0.75rem] font-extrabold" (click)="toggle(it.type + it.id)" [attr.aria-expanded]="open().has(it.type + it.id)">
                      <app-icon [name]="open().has(it.type + it.id) ? 'chevron-left' : 'chevron-right'" [size]="13" [style.transform]="open().has(it.type + it.id) ? 'rotate(-90deg)' : 'rotate(0)'" />食材の内訳
                    </button>
                    @if (open().has(it.type + it.id)) {
                      <ul class="m-0 mt-1 flex list-none flex-col gap-1 p-0 pl-2">
                        @for (c of it.children; track c.foodId) {
                          <li class="flex justify-between gap-2 border-b border-white/60 py-0.5 text-[0.8rem] font-bold">
                            <span>{{ c.name }}</span><span class="tabnum">{{ fmt(c.amount) }}{{ unit() }}</span>
                          </li>
                        }
                      </ul>
                      <p class="muted m-0 mt-1 text-[0.7rem] font-semibold">※内訳はメニューの現在の構成から按分した参考値です。</p>
                    }
                  }
                </li>
              }
            </ul>
          </section>
        }
      }
    }
    <app-disclaimer />
  `,
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
