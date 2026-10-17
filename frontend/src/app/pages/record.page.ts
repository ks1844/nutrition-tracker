// SCR-03 食事記録（FR-010, 011, 030〜037）
//
// 自動保存：この画面は「ある日の 1 つの食事区分（スロット）」を編集する。保存ボタンの押し忘れを防ぐため、
// 次のタイミングで未保存の変更をまとめて保存する（入力のたびには API を呼ばない）。
//   ・日付を変えたとき／食事区分を変えたとき（変える前の内容を保存してから切り替える）
//   ・画面を離れるとき（戻る・他のタブへ移動・リンク遷移。CanDeactivate ガード）
//   ・「完了して戻る」を押したとき
//   ・ページが非表示になるとき／閉じるとき（best effort）
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FoodsApi } from '../api/foods.api';
import { MealRecordsApi } from '../api/meal-records.api';
import { MenusApi } from '../api/menus.api';
import { fieldErrors, toApiError } from '../api/api-client';
import { FoodSummary, FoodUnit, InputMode, MealRecordInput, MealRecordSaved, MealType, MenuSummary, RecentItems } from '../types/api';
import { UiService } from '../services/ui.service';
import { MEAL_LABEL, MEAL_ORDER, fmtNum, guessMealType } from '../services/format';
import { todayString } from '../services/date-util';
import { PageHeader } from '../components/page-header';
import { FoodSearchDialog } from '../components/food-search-dialog';
import { MenuPickerDialog } from '../components/menu-picker-dialog';
import { ChipGroup, ChipOption, ChipValue } from '../components/chip-group';
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

type SaveState = 'idle' | 'saving' | 'saved' | 'error';

const CHIPS: { label: string; mode: InputMode; value: number | null }[] = [
  { label: '1杯', mode: 'SERVING', value: 1 },
  { label: '1.5杯', mode: 'SERVING', value: 1.5 },
  { label: '1/2杯', mode: 'SERVING', value: 0.5 },
  { label: 'g入力', mode: 'GRAM', value: null },
  { label: '割合', mode: 'RATIO', value: null },
];

@Component({
  selector: 'app-record-page',
  imports: [PageHeader, FoodSearchDialog, MenuPickerDialog, ChipGroup, EmptyState, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="食事を記録" [back]="['/']" />

    <section class="glass flex flex-col gap-3 p-4" aria-label="日付と食事区分">
      <div>
        <label class="field-label" for="rc-date">日付</label>
        <input id="rc-date" class="field" type="date" [value]="eatenOn()" (change)="changeDate($any($event.target).value)" />
      </div>
      <div role="radiogroup" aria-label="食事区分" class="grid grid-cols-4 gap-2">
        @for (m of meals; track m) {
          <button type="button" class="chip justify-center !px-1" role="radio" [attr.aria-pressed]="mealType() === m" [attr.aria-checked]="mealType() === m" (click)="changeMeal(m)">{{ mealLabel[m] }}</button>
        }
      </div>
      <p class="muted m-0 flex items-start gap-1.5 text-[0.75rem] font-bold" role="status" aria-live="polite">
        <app-icon [name]="statusIcon()" [size]="14" class="mt-0.5 shrink-0" />{{ statusText() }}
      </p>
    </section>

    @if (!loading() && recentChips().length) {
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
              <span class="muted tabnum ml-auto text-[0.8rem] font-bold">≒ {{ gramsText(d) }}</span>
            </div>
            <div class="mt-2">
              <app-chip-group [options]="unitOptions(d)" [value]="d.unitId" [label]="d.name + 'の単位'" (changed)="setUnit(d.key, $event)" />
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
        @if (loading()) {
          <p class="muted text-center text-[0.9rem] font-bold">読み込み中…</p>
        } @else {
          <app-empty-state icon="pot" title="まだ項目がありません" text="メニューか食材を追加してください。選んだ内容は自動で保存されます" />
        }
      }
    </section>

    <div class="grid grid-cols-2 gap-2">
      <button type="button" class="btn" (click)="menuPicker.set(true)"><app-icon name="pot" [size]="18" />メニューから</button>
      <button type="button" class="btn" (click)="foodDialog.set(true)"><app-icon name="search" [size]="18" />食材から</button>
    </div>
    @if (errs()['items']) { <p class="field-error">{{ errs()['items'] }}</p> }

    <section class="glass flex flex-col gap-3 p-4">
      <div>
        <label class="field-label" for="rc-memo">メモ（任意）</label>
        <input id="rc-memo" class="field" type="text" maxlength="500" [value]="memo()" (input)="setMemo($any($event.target).value)" />
      </div>
      <div class="flex items-baseline justify-between">
        <span class="muted text-[0.85rem] font-bold">この食事の合計</span>
        <span class="tabnum text-[1.5rem] font-extrabold">約 {{ totalKcal() }}<span class="muted ml-1 text-[0.8rem]">kcal</span></span>
      </div>
      <button type="button" class="btn btn-primary w-full" [disabled]="saveState() === 'saving'" (click)="done()">完了して戻る</button>
      @if (recordIds().length) {
        <button type="button" class="btn btn-danger w-full" (click)="removeRecord()">この食事の記録を削除</button>
      }
    </section>

    @if (foodDialog()) {
      <app-food-search-dialog (picked)="addFood($event)" (closed)="foodDialog.set(false)" />
    }
    @if (menuPicker()) {
      <app-menu-picker-dialog (picked)="addMenu($event)" (closed)="menuPicker.set(false)" />
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

  protected readonly meals = MEAL_ORDER;
  protected readonly mealLabel = MEAL_LABEL;
  protected readonly chips = CHIPS;

  protected readonly eatenOn = signal(todayString());
  protected readonly mealType = signal<MealType>(guessMealType());
  protected readonly memo = signal('');
  protected readonly items = signal<Draft[]>([]);
  protected readonly errs = signal<Record<string, string>>({});
  protected readonly loading = signal(true);
  protected readonly foodDialog = signal(false);
  protected readonly menuPicker = signal(false);
  /** このスロット（日付＋食事区分）に保存済みの記録 ID。 */
  protected readonly recordIds = signal<number[]>([]);
  readonly dirty = signal(false);
  protected readonly saveState = signal<SaveState>('idle');
  private readonly recent = signal<RecentItems>({ menus: [], foods: [] });
  private seq = 1;
  private rev = 0;
  private slotSeq = 0;
  private inflight: Promise<boolean> | null = null;

  protected readonly recentChips = computed(() => {
    const r = this.recent();
    const out: { key: string; label: string; add: () => void }[] = [];
    for (const m of r.menus.slice(0, 4)) out.push({ key: `m${m.menuId}`, label: m.name, add: () => void this.addMenuById(m.menuId) });
    for (const f of r.foods.slice(0, 4)) out.push({ key: `f${f.foodId}`, label: f.name, add: () => void this.addFoodById(f.foodId) });
    return out;
  });

  protected readonly totalKcal = computed(() => this.fmtK(this.items().reduce((s, d) => s + this.kcalOf(d), 0)));

  protected readonly statusText = computed(() => {
    if (this.saveState() === 'saving') return '保存中…';
    if (this.saveState() === 'error') return '保存できていません。入力内容を確認してください。';
    if (this.dirty()) return '未保存の変更があります。日付・食事区分を変えるか、画面を離れると自動で保存されます。';
    if (this.recordIds().length) return '保存済みです。変更は自動で保存されます。';
    return '項目を選ぶと、日付・食事区分を変えたときや画面を離れるときに自動で保存されます。';
  });
  protected readonly statusIcon = computed(() =>
    this.saveState() === 'error' ? 'alert' as const : this.dirty() || this.saveState() === 'saving' ? 'clock' as const : 'check' as const,
  );

  async ngOnInit(): Promise<void> {
    const q = this.date();
    if (q) this.eatenOn.set(q);
    try {
      this.recent.set(await this.recApi.recent(8));
      if (this.id()) {
        // 編集リンク（/record/:id）：その記録の食事区分のスロットを開く
        const day = await this.recApi.ofDay(this.eatenOn());
        const rec = day.records.find((r) => r.id === Number(this.id()));
        if (rec) this.mealType.set(rec.mealType);
      }
      await this.loadSlot();
      if (!this.id() && this.menuId()) await this.addMenuById(Number(this.menuId()));
    } catch (e) {
      this.ui.error(toApiError(e).message);
      this.loading.set(false);
    }
  }

  // ---- スロット（日付＋食事区分）の読み込み・切替 ----
  private async loadSlot(): Promise<void> {
    const seq = ++this.slotSeq;
    this.loading.set(true);
    this.items.set([]);
    this.recordIds.set([]);
    this.memo.set('');
    this.errs.set({});
    this.dirty.set(false);
    this.saveState.set('idle');
    const day = await this.recApi.ofDay(this.eatenOn());
    if (seq !== this.slotSeq) return;
    const recs = day.records.filter((r) => r.mealType === this.mealType());
    const drafts: Draft[] = [];
    for (const rec of recs) {
      for (const it of rec.items) {
        const d =
          it.menuId != null ? await this.makeMenuDraft(it.menuId, it.inputMode, it.inputValue, it.portionRatio ?? 0)
          : it.foodId != null ? await this.makeFoodDraft(it.foodId, it.unitId, it.inputMode, it.inputValue)
          : null;
        if (d) drafts.push(d);
      }
    }
    if (seq !== this.slotSeq) return;
    this.items.set(drafts);
    this.recordIds.set(recs.map((r) => r.id));
    this.memo.set(recs[0]?.memo ?? '');
    this.dirty.set(false);
    if (recs.length) this.saveState.set('saved');
    this.loading.set(false);
  }

  /** 切替の前に、いまのスロットの未保存の変更を保存する。保存できなければ切り替えない。 */
  private async switchSlot(date: string, meal: MealType): Promise<void> {
    if (date === this.eatenOn() && meal === this.mealType()) return;
    if (!(await this.flush())) return;
    this.eatenOn.set(date);
    this.mealType.set(meal);
    try {
      await this.loadSlot();
    } catch (e) {
      this.ui.error(toApiError(e).message);
      this.loading.set(false);
    }
  }

  protected async changeDate(value: string): Promise<void> {
    if (!value) return;
    await this.switchSlot(value, this.mealType());
  }

  protected async changeMeal(m: MealType): Promise<void> {
    await this.switchSlot(this.eatenOn(), m);
  }

  // ---- 保存（自動） ----
  /** 未保存の変更を保存する。保存すべきものが無ければ何もしない。成功（または不要）なら true。 */
  async flush(announce = false): Promise<boolean> {
    if (this.inflight) await this.inflight;
    if (!this.dirty()) return true;
    const p = this.doFlush(announce);
    this.inflight = p;
    try {
      return await p;
    } finally {
      this.inflight = null;
    }
  }

  private buildInput(): MealRecordInput {
    return {
      eatenOn: this.eatenOn(), mealType: this.mealType(), memo: this.memo().trim() || null,
      items: this.items().map((d) =>
        d.kind === 'menu'
          ? { menuId: d.id, inputMode: d.mode, inputValue: d.value }
          : { foodId: d.id, unitId: d.unitId, inputMode: d.unitId === null ? ('GRAM' as const) : ('UNIT' as const), inputValue: d.value },
      ),
    };
  }

  private async doFlush(announce: boolean): Promise<boolean> {
    const rev0 = this.rev;
    this.errs.set({});
    const list = this.items();
    const ids = this.recordIds();
    try {
      if (list.length === 0) {
        // 項目をすべて外した場合は、保存済みの記録も削除して状態をそろえる
        if (ids.length) {
          this.saveState.set('saving');
          for (const id of ids) await this.recApi.remove(id);
          this.recordIds.set([]);
          if (announce) this.ui.toast('項目がないため、この食事の記録を削除しました', 'info');
        }
        if (this.rev === rev0) this.dirty.set(false);
        this.saveState.set('idle');
        return true;
      }
      const bad = list.findIndex((d) => !(d.value > 0));
      if (bad >= 0) {
        this.errs.set({ [`items[${bad}].inputValue`]: '0より大きい値を入力してください' });
        this.saveState.set('error');
        return false;
      }
      this.saveState.set('saving');
      const input = this.buildInput();
      let saved: MealRecordSaved;
      if (ids.length) {
        saved = await this.recApi.update(ids[0] as number, input);
        for (const extra of ids.slice(1)) await this.recApi.remove(extra); // 同じ食事区分の記録が複数あれば 1 件にまとめる
      } else {
        saved = await this.recApi.create(input);
      }
      this.recordIds.set([saved.id]);
      if (this.rev === rev0) this.dirty.set(false);
      this.saveState.set('saved');
      const w = saved.warnings[0];
      if (w) this.ui.toast(`保存しました。${w.message}`, 'info');
      else if (announce) this.ui.toast('食事を保存しました');
      return true;
    } catch (e) {
      this.errs.set(fieldErrors(e));
      this.saveState.set('error');
      this.ui.error(toApiError(e).message);
      return false;
    }
  }

  /** 画面を離れる前の処理（CanDeactivate ガードから呼ぶ）。保存できないときは確認する。 */
  async canLeave(): Promise<boolean> {
    if (await this.flush(true)) return true;
    return this.ui.confirm({
      title: '保存できない項目があります',
      message: 'このまま移動すると、未保存の変更は失われます。移動しますか？',
      okLabel: '移動する',
      danger: true,
    });
  }

  protected async done(): Promise<void> {
    if (!(await this.flush(true))) return;
    await this.router.navigateByUrl('/');
  }

  /** タブを閉じる／更新するときは、未保存があればブラウザの確認を出す。 */
  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(ev: BeforeUnloadEvent): void {
    if (this.dirty()) {
      ev.preventDefault();
      ev.returnValue = '';
    }
  }

  /** 画面が非表示になる（アプリ切替・タブ切替）ときに、未保存の変更を保存する（best effort）。 */
  @HostListener('document:visibilitychange')
  protected onVisibility(): void {
    if (document.visibilityState === 'hidden' && this.dirty()) void this.flush();
  }

  protected async removeRecord(): Promise<void> {
    const ok = await this.ui.confirm({ title: '記録を削除', message: 'この食事の記録を削除します。よろしいですか？', okLabel: '削除する', danger: true });
    if (!ok) return;
    try {
      for (const id of this.recordIds()) await this.recApi.remove(id);
      this.recordIds.set([]);
      this.items.set([]);
      this.dirty.set(false);
      this.ui.toast('記録を削除しました');
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.ui.error(toApiError(e).message);
    }
  }

  // ---- 追加 ----
  private touch(): void {
    this.rev += 1;
    this.dirty.set(true);
    if (this.saveState() === 'error' || this.saveState() === 'saved') this.saveState.set('idle');
  }

  protected setMemo(v: string): void {
    this.memo.set(v);
    this.touch();
  }

  protected async addMenu(m: MenuSummary): Promise<void> {
    this.menuPicker.set(false);
    await this.addMenuById(m.id);
  }

  private async addMenuById(id: number): Promise<void> {
    const d = await this.makeMenuDraft(id, 'SERVING', 1, 0);
    if (d) {
      this.items.update((l) => [...l, d]);
      this.touch();
    }
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
      this.touch();
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
    this.touch();
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

  protected unitOptions(d: Draft): ChipOption[] {
    return [{ value: null, label: 'g' }, ...d.units.map((u) => ({ value: u.id, label: u.unitName }))];
  }

  protected setValue(key: number, raw: string): void {
    const n = raw === '' ? 0 : Number(raw);
    const d = this.items().find((x) => x.key === key);
    this.patch(key, { value: d && d.kind === 'menu' && d.mode === 'RATIO' ? n / 100 : n });
  }

  protected setUnit(key: number, v: ChipValue): void {
    this.patch(key, v === null ? { unitId: null, mode: 'GRAM' } : { unitId: Number(v), mode: 'UNIT' });
  }

  private patch(key: number, p: Partial<Draft>): void {
    this.items.update((l) => l.map((d) => (d.key === key ? { ...d, ...p } : d)));
    this.touch();
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
}
