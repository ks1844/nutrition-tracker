// SCR-03 食事記録（作成/編集）（FR-010, 011, 030〜037）
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FoodsApi } from '../api/foods.api';
import { MealRecordsApi } from '../api/meal-records.api';
import { MenusApi } from '../api/menus.api';
import { fieldErrors, toApiError } from '../api/api-client';
import { FoodSummary, FoodUnit, InputMode, MealRecordInput, MealType, MenuSummary, RecentItems } from '../types/api';
import { UiService } from '../services/ui.service';
import { MEAL_LABEL, MEAL_ORDER, fmtNum, guessMealType } from '../services/format';
import { todayString } from '../services/date-util';
import { PageHeader } from '../components/page-header';
import { FoodSearchDialog } from '../components/food-search-dialog';
import { EmptyState } from '../components/empty-state';
import { Icon } from '../components/icon';

interface Draft {
  key: number;
  kind: 'menu' | 'food';
  id: number;
  name: string;
  mode: InputMode;
  value: number;
  unitId: number | null;
  // メニュー
  servings: number;
  cookedG: number;
  energyTotal: number;
  /** この項目を除いた残量の割合（超過判定用） */
  baseRemaining: number;
  // 食材
  refusePct: number;
  energyPer100: number | null;
  units: FoodUnit[];
}

const CHIPS: { label: string; mode: InputMode; value: number | null }[] = [
  { label: '1杯', mode: 'SERVING', value: 1 },
  { label: '1.5杯', mode: 'SERVING', value: 1.5 },
  { label: '1/2杯', mode: 'SERVING', value: 0.5 },
  { label: 'g入力', mode: 'GRAM', value: null },
  { label: '割合', mode: 'RATIO', value: null },
];

@Component({
  selector: 'app-record-page',
  imports: [RouterLink, PageHeader, FoodSearchDialog, EmptyState, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="editId() ? '記録を編集' : '食事を記録'" [back]="['/']" />

    <section class="glass flex flex-col gap-3 p-4" aria-label="日付と食事区分">
      <div>
        <label class="field-label" for="rc-date">日付</label>
        <input id="rc-date" class="field" type="date" [value]="eatenOn()" (input)="eatenOn.set($any($event.target).value)" [attr.aria-invalid]="!!errs()['eatenOn']" />
        @if (errs()['eatenOn']) { <p class="field-error">{{ errs()['eatenOn'] }}</p> }
      </div>
      <div role="radiogroup" aria-label="食事区分" class="grid grid-cols-4 gap-2">
        @for (m of meals; track m) {
          <button type="button" class="chip justify-center !px-1" role="radio" [attr.aria-pressed]="mealType() === m" [attr.aria-checked]="mealType() === m" (click)="mealType.set(m)">{{ mealLabel[m] }}</button>
        }
      </div>
    </section>

    @if (!editId() && recentChips().length) {
      <section class="glass p-4" aria-label="最近使った">
        <h2 class="h-sub m-0 mb-2 flex items-center gap-1.5"><app-icon name="clock" [size]="16" />最近使った</h2>
        <div class="flex flex-wrap gap-2">
          @for (c of recentChips(); track c.key) {
            <button type="button" class="chip" (click)="c.add()"><app-icon name="plus" [size]="14" />{{ c.label }}</button>
          }
        </div>
      </section>
    }

    <section class="flex flex-col gap-3" aria-label="記録する項目">
      @for (d of items(); track d.key; let i = $index) {
        <article class="glass pop-in p-4">
          <div class="flex items-start justify-between gap-2">
            <div class="min-w-0">
              <p class="muted m-0 text-[0.72rem] font-extrabold">{{ d.kind === 'menu' ? 'メニュー' : '食材' }}</p>
              <h3 class="m-0 truncate text-[1.05rem] font-extrabold">{{ d.name }}</h3>
            </div>
            <button type="button" class="btn btn-sm btn-danger !min-h-9 !w-9 !p-0" (click)="remove(d.key)" [attr.aria-label]="d.name + 'を削除'"><app-icon name="trash" [size]="15" /></button>
          </div>

          @if (d.kind === 'menu') {
            <div class="mt-3 flex flex-wrap gap-2" role="group" aria-label="量のクイック入力">
              @for (c of chips; track c.label) {
                <button type="button" class="chip !min-h-10 !px-3" [attr.aria-pressed]="isChipActive(d, c)" (click)="applyChip(d, c)">{{ c.label }}</button>
              }
            </div>
            <div class="mt-3 flex items-center gap-2">
              <input class="field tabnum !w-28" type="number" inputmode="decimal" min="0" [step]="d.mode === 'GRAM' ? 10 : d.mode === 'RATIO' ? 5 : 0.5"
                     [value]="displayValue(d)" (input)="setValue(d.key, $any($event.target).value)" [attr.aria-label]="d.name + 'の量'" [attr.aria-invalid]="!!errs()['items[' + i + '].inputValue']" />
              <span class="text-[0.95rem] font-extrabold">{{ unitText(d) }}</span>
              <span class="muted tabnum ml-auto text-[0.8rem] font-bold">≒ {{ gramsText(d) }}</span>
            </div>
          } @else {
            <div class="mt-3 flex items-center gap-2">
              <input class="field tabnum !w-28" type="number" inputmode="decimal" min="0" step="any" [value]="d.value || ''"
                     (input)="setValue(d.key, $any($event.target).value)" [attr.aria-label]="d.name + 'の量'" [attr.aria-invalid]="!!errs()['items[' + i + '].inputValue']" />
              <select class="field !w-auto" (change)="setUnit(d.key, $any($event.target).value)" [attr.aria-label]="'単位'">
                <option value="" [selected]="d.unitId === null">g</option>
                @for (u of d.units; track u.id) { <option [value]="u.id" [selected]="d.unitId === u.id">{{ u.unitName }}</option> }
              </select>
              <span class="muted tabnum ml-auto text-[0.8rem] font-bold">≒ {{ gramsText(d) }}</span>
            </div>
          }
          @if (errs()['items[' + i + '].inputValue']) { <p class="field-error">{{ errs()['items[' + i + '].inputValue'] }}</p> }

          <div class="mt-2 flex items-center justify-between gap-2">
            <span class="tabnum text-[0.95rem] font-extrabold">約 {{ kcalText(d) }} <span class="muted text-[0.75rem]">kcal</span></span>
            @if (overPct(d); as o) {
              <span class="badge st-over" role="alert"><app-icon name="alert" [size]="13" />合計{{ o }}%（保存はできます）</span>
            } @else if (d.kind === 'menu') {
              <span class="muted text-[0.75rem] font-bold">残り {{ remainText(d) }}</span>
            }
          </div>
        </article>
      } @empty {
        <app-empty-state icon="pot" title="まだ項目がありません" text="メニューか食材を追加してください" />
      }
    </section>

    <div class="grid grid-cols-2 gap-2">
      <button type="button" class="btn" (click)="openMenuPicker()"><app-icon name="pot" [size]="18" />メニューから</button>
      <button type="button" class="btn" (click)="foodDialog.set(true)"><app-icon name="search" [size]="18" />食材から</button>
    </div>
    @if (errs()['items']) { <p class="field-error">{{ errs()['items'] }}</p> }

    <section class="glass flex flex-col gap-3 p-4">
      <div>
        <label class="field-label" for="rc-memo">メモ（任意）</label>
        <input id="rc-memo" class="field" type="text" maxlength="500" [value]="memo()" (input)="memo.set($any($event.target).value)" />
      </div>
      <div class="flex items-baseline justify-between">
        <span class="muted text-[0.85rem] font-bold">この食事の合計</span>
        <span class="tabnum text-[1.5rem] font-extrabold">約 {{ totalKcal() }}<span class="muted ml-1 text-[0.8rem]">kcal</span></span>
      </div>
      <button type="button" class="btn btn-primary w-full" [disabled]="saving() || items().length === 0" (click)="save()">{{ saving() ? '保存中…' : '保存する' }}</button>
      @if (editId()) {
        <button type="button" class="btn btn-danger w-full" (click)="removeRecord()">この記録を削除</button>
      }
    </section>

    @if (foodDialog()) {
      <app-food-search-dialog (picked)="addFood($event)" (closed)="foodDialog.set(false)" />
    }
    @if (menuPicker()) {
      <div class="fixed inset-0 z-[55] flex items-end justify-center bg-indigo-950/25 backdrop-blur-sm sm:items-center sm:p-4" (click)="menuPicker.set(false)">
        <section class="glass-strong pop-in flex max-h-[80vh] w-full max-w-lg flex-col overflow-hidden !rounded-b-none sm:!rounded-b-3xl" role="dialog" aria-modal="true" aria-labelledby="mp-title" (click)="$event.stopPropagation()">
          <header class="flex items-center justify-between px-4 pt-4 pb-2">
            <h2 id="mp-title" class="h-sub m-0">メニューを選ぶ</h2>
            <button type="button" class="btn btn-sm !w-10 !p-0" (click)="menuPicker.set(false)" aria-label="閉じる"><app-icon name="x" /></button>
          </header>
          <div class="overflow-y-auto px-4 pb-4">
            @if (menuList().length === 0) {
              <app-empty-state icon="pot" title="メニューがありません" text="先にメニューを作成しましょう">
                <a routerLink="/menus/new" class="btn btn-primary btn-sm">メニューを作る</a>
              </app-empty-state>
            } @else {
              <ul class="m-0 flex list-none flex-col gap-2 p-0">
                @for (m of menuList(); track m.id) {
                  <li>
                    <button type="button" class="glass-inset flex w-full items-center justify-between gap-2 px-3.5 py-3 text-left" (click)="addMenu(m)">
                      <span class="min-w-0"><span class="block truncate text-[0.95rem] font-extrabold">{{ m.name }}</span>
                        <span class="muted tabnum block text-[0.78rem] font-bold">残り {{ fmtNum(m.remaining.servings, 1) }}杯（{{ Math.round(m.remaining.ratio * 100) }}%）</span></span>
                      <app-icon name="plus" />
                    </button>
                  </li>
                }
              </ul>
            }
          </div>
        </section>
      </div>
    }
  `,
})
export class RecordPage implements OnInit {
  readonly id = input<string>();
  readonly date = input<string>();
  readonly menuId = input<string>();

  private readonly foodsApi = inject(FoodsApi);
  private readonly menusApi = inject(MenusApi);
  private readonly recApi = inject(MealRecordsApi);
  private readonly ui = inject(UiService);
  private readonly router = inject(Router);

  protected readonly Math = Math;
  protected readonly fmtNum = fmtNum;
  protected readonly meals = MEAL_ORDER;
  protected readonly mealLabel = MEAL_LABEL;
  protected readonly chips = CHIPS;

  protected readonly eatenOn = signal(todayString());
  protected readonly mealType = signal<MealType>(guessMealType());
  protected readonly memo = signal('');
  protected readonly items = signal<Draft[]>([]);
  protected readonly errs = signal<Record<string, string>>({});
  protected readonly saving = signal(false);
  protected readonly foodDialog = signal(false);
  protected readonly menuPicker = signal(false);
  protected readonly menuList = signal<MenuSummary[]>([]);
  private readonly recent = signal<RecentItems>({ menus: [], foods: [] });
  private seq = 1;

  protected readonly editId = computed(() => (this.id() ? Number(this.id()) : null));

  protected readonly recentChips = computed(() => {
    const r = this.recent();
    const out: { key: string; label: string; add: () => void }[] = [];
    for (const m of r.menus.slice(0, 4)) out.push({ key: `m${m.menuId}`, label: m.name, add: () => void this.addMenuById(m.menuId) });
    for (const f of r.foods.slice(0, 4)) out.push({ key: `f${f.foodId}`, label: f.name, add: () => void this.addFoodById(f.foodId) });
    return out;
  });

  protected readonly totalKcal = computed(() => this.fmtK(this.items().reduce((s, d) => s + this.kcalOf(d), 0)));

  async ngOnInit(): Promise<void> {
    const q = this.date();
    if (q) this.eatenOn.set(q);
    try {
      this.recent.set(await this.recApi.recent(8));
      this.menuList.set(await this.menusApi.list());
      if (this.editId()) await this.loadForEdit();
      else if (this.menuId()) await this.addMenuById(Number(this.menuId()));
    } catch (e) {
      this.ui.error(toApiError(e).message);
    }
  }

  private async loadForEdit(): Promise<void> {
    const day = await this.recApi.ofDay(this.eatenOn());
    const rec = day.records.find((r) => r.id === this.editId());
    if (!rec) {
      this.ui.error('記録が見つかりませんでした');
      await this.router.navigateByUrl('/');
      return;
    }
    this.mealType.set(rec.mealType);
    this.memo.set(rec.memo ?? '');
    const drafts: Draft[] = [];
    for (const it of rec.items) {
      if (it.menuId != null) {
        const d = await this.makeMenuDraft(it.menuId, it.inputMode, it.inputValue, it.portionRatio ?? 0);
        if (d) drafts.push(d);
      } else if (it.foodId != null) {
        const d = await this.makeFoodDraft(it.foodId, it.unitId, it.inputMode, it.inputValue);
        if (d) drafts.push(d);
      }
    }
    this.items.set(drafts);
  }

  // ---- 追加 ----
  protected async openMenuPicker(): Promise<void> {
    try {
      this.menuList.set(await this.menusApi.list());
    } catch (e) {
      this.ui.error(toApiError(e).message);
    }
    this.menuPicker.set(true);
  }

  protected async addMenu(m: MenuSummary): Promise<void> {
    this.menuPicker.set(false);
    await this.addMenuById(m.id);
  }

  private async addMenuById(id: number): Promise<void> {
    const d = await this.makeMenuDraft(id, 'SERVING', 1, 0);
    if (d) this.items.update((l) => [...l, d]);
  }

  protected async addFood(f: FoodSummary): Promise<void> {
    this.foodDialog.set(false);
    await this.addFoodById(f.id);
  }

  private async addFoodById(id: number): Promise<void> {
    const d = await this.makeFoodDraft(id, null, 'GRAM', 100);
    if (d) {
      const def = d.units.find((u) => u.isDefault);
      if (def) {
        d.unitId = def.id;
        d.mode = 'UNIT';
        d.value = 1;
      }
      this.items.update((l) => [...l, d]);
    }
  }

  private async makeMenuDraft(id: number, mode: InputMode, value: number, ownRatio: number): Promise<Draft | null> {
    try {
      const m = await this.menusApi.get(id);
      return {
        key: this.seq++, kind: 'menu', id, name: m.name, mode, value, unitId: null,
        servings: m.servings, cookedG: m.cookedTotalWeightG,
        energyTotal: m.nutrients.find((n) => n.code === 'ENERGY')?.amount ?? 0,
        baseRemaining: m.remaining.ratio + ownRatio,
        refusePct: 0, energyPer100: null, units: [],
      };
    } catch (e) {
      this.ui.error(toApiError(e).message);
      return null;
    }
  }

  private async makeFoodDraft(id: number, unitId: number | null, mode: InputMode, value: number): Promise<Draft | null> {
    try {
      const f = await this.foodsApi.get(id);
      return {
        key: this.seq++, kind: 'food', id, name: f.name, mode, value, unitId,
        servings: 1, cookedG: 0, energyTotal: 0, baseRemaining: 0,
        refusePct: f.refuseRatePct, energyPer100: f.nutrients.find((n) => n.code === 'ENERGY')?.per100g ?? null, units: f.units,
      };
    } catch (e) {
      this.ui.error(toApiError(e).message);
      return null;
    }
  }

  protected remove(key: number): void {
    this.items.update((l) => l.filter((d) => d.key !== key));
  }

  // ---- 入力 ----
  protected isChipActive(d: Draft, c: { mode: InputMode; value: number | null }): boolean {
    return c.value === null ? d.mode === c.mode : d.mode === c.mode && d.value === c.value;
  }

  protected applyChip(d: Draft, c: { mode: InputMode; value: number | null }): void {
    let value = c.value;
    if (value === null) value = c.mode === 'GRAM' ? Math.round(d.cookedG / d.servings) : 25;
    this.patch(d.key, { mode: c.mode, value: c.mode === 'RATIO' ? value / 100 : value });
  }

  protected displayValue(d: Draft): number | string {
    if (!d.value) return '';
    return d.mode === 'RATIO' ? Math.round(d.value * 100) : d.value;
  }

  protected unitText(d: Draft): string {
    return d.mode === 'SERVING' ? '杯' : d.mode === 'RATIO' ? '%' : 'g';
  }

  protected setValue(key: number, raw: string): void {
    const n = raw === '' ? 0 : Number(raw);
    const d = this.items().find((x) => x.key === key);
    this.patch(key, { value: d && d.kind === 'menu' && d.mode === 'RATIO' ? n / 100 : n });
  }

  protected setUnit(key: number, raw: string): void {
    this.patch(key, raw === '' ? { unitId: null, mode: 'GRAM' } : { unitId: Number(raw), mode: 'UNIT' });
  }

  private patch(key: number, p: Partial<Draft>): void {
    this.items.update((l) => l.map((d) => (d.key === key ? { ...d, ...p } : d)));
  }

  // ---- 概算（確定値はサーバーが計算する。docs/03.栄養計算仕様.md） ----
  private ratioOf(d: Draft): number {
    return d.mode === 'SERVING' ? d.value / d.servings : d.mode === 'GRAM' ? d.value / d.cookedG : d.value;
  }

  private gramsOf(d: Draft): number {
    if (d.kind === 'menu') return d.mode === 'GRAM' ? d.value : this.ratioOf(d) * d.cookedG;
    if (d.unitId === null) return d.value;
    return d.value * (d.units.find((u) => u.id === d.unitId)?.gramsPerUnit ?? 1);
  }

  private kcalOf(d: Draft): number {
    if (d.kind === 'menu') return d.energyTotal * this.ratioOf(d);
    if (d.energyPer100 === null) return 0;
    return ((this.gramsOf(d) * (1 - d.refusePct / 100)) / 100) * d.energyPer100;
  }

  private fmtK(v: number): string {
    return Math.round(v).toLocaleString('ja-JP');
  }

  protected kcalText(d: Draft): string {
    return this.fmtK(this.kcalOf(d));
  }

  protected gramsText(d: Draft): string {
    return `${fmtNum(this.gramsOf(d), 0)}g`;
  }

  protected remainText(d: Draft): string {
    const rest = d.baseRemaining - this.ratioOf(d);
    return `${fmtNum(Math.max(rest, 0) * d.servings, 1)}杯（${Math.max(Math.round(rest * 100), 0)}%）`;
  }

  /** FR-034：このメニューの記録が合計 100% を超える場合、超過後の合計(%)を返す。 */
  protected overPct(d: Draft): number {
    if (d.kind !== 'menu') return 0;
    const total = 1 - d.baseRemaining + this.ratioOf(d);
    return total > 1.0000001 ? Math.round(total * 100) : 0;
  }

  // ---- 保存 ----
  protected async save(): Promise<void> {
    this.errs.set({});
    const input: MealRecordInput = {
      eatenOn: this.eatenOn(), mealType: this.mealType(), memo: this.memo().trim() || null,
      items: this.items().map((d) =>
        d.kind === 'menu'
          ? { menuId: d.id, inputMode: d.mode, inputValue: d.value }
          : { foodId: d.id, unitId: d.unitId, inputMode: d.unitId === null ? ('GRAM' as const) : ('UNIT' as const), inputValue: d.value },
      ),
    };
    this.saving.set(true);
    try {
      const saved = this.editId() ? await this.recApi.update(this.editId() as number, input) : await this.recApi.create(input);
      const w = saved.warnings[0];
      this.ui.toast(w ? `保存しました。${w.message}` : '記録を保存しました', w ? 'info' : 'success');
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.errs.set(fieldErrors(e));
      this.ui.error(toApiError(e).message);
    } finally {
      this.saving.set(false);
    }
  }

  protected async removeRecord(): Promise<void> {
    if (!(await this.ui.confirm({ title: '記録を削除', message: 'この食事の記録を削除します。よろしいですか？', okLabel: '削除する', danger: true }))) return;
    try {
      await this.recApi.remove(this.editId() as number);
      this.ui.toast('記録を削除しました');
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.ui.error(toApiError(e).message);
    }
  }
}
