// SCR-05 メニュー作成/編集ウィザード（FR-010, 011, 020〜025）
import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MenusApi } from '../api/menus.api';
import { FoodsApi } from '../api/foods.api';
import { fieldErrors, toApiError } from '../api/api-client';
import { FoodSummary, FoodUnit, MenuInput, MenuPreview, NutrientAmount } from '../types/api';
import { UiService } from '../services/ui.service';
import { NutrientCatalog } from '../services/nutrient-catalog';
import { COOKING_LOSS_NOTE, fmtAmount, fmtNum } from '../services/format';
import { todayString } from '../services/date-util';
import { PageHeader } from '../components/page-header';
import { FoodSearchDialog } from '../components/food-search-dialog';
import { Disclaimer } from '../components/disclaimer';
import { EmptyState } from '../components/empty-state';
import { Icon } from '../components/icon';
import { ChipGroup, ChipOption, ChipValue } from '../components/chip-group';

interface Row {
  key: number;
  foodId: number;
  name: string;
  units: FoodUnit[];
  unitId: number | null;
  quantity: number;
}

const STEPS = ['名前', '食材', '完成重量', '1食分'] as const;
const PREVIEW_CODES = ['ENERGY', 'PROT', 'FAT', 'CHO', 'VITC', 'VITA_RAE'];

@Component({
  selector: 'app-menu-wizard-page',
  imports: [PageHeader, FoodSearchDialog, Disclaimer, EmptyState, Icon, ChipGroup],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="editId() ? 'メニューを編集' : 'メニューを作る'" [back]="['/menus']" />

    <!-- ステップ表示 -->
    <ol class="glass m-0 grid list-none grid-cols-4 gap-1 p-2" aria-label="作成の手順">
      @for (s of steps; track s; let i = $index) {
        <li class="flex flex-col items-center gap-0.5 rounded-2xl px-1 py-1.5 text-center" [class.step-on]="i === step()" [attr.aria-current]="i === step() ? 'step' : null">
          <span class="grid h-7 w-7 place-items-center rounded-full text-[0.8rem] font-extrabold" [class]="i <= step() ? 'step-dot' : 'step-dot-off'">
            @if (i < step()) { <app-icon name="check" [size]="14" /> } @else { {{ i + 1 }} }
          </span>
          <span class="text-[0.7rem] font-extrabold">{{ s }}</span>
        </li>
      }
    </ol>

    <!-- リアルタイム栄養プレビュー -->
    @if (step() >= 1) {
      <section class="glass p-3.5" aria-label="栄養プレビュー" aria-live="polite">
        <div class="mb-1.5 flex items-center justify-between">
          <h2 class="h-sub m-0 flex items-center gap-1.5"><app-icon name="sparkle" [size]="16" />メニュー全体の栄養（概算）</h2>
          @if (previewing()) { <span class="muted text-[0.72rem] font-bold">計算中…</span> }
        </div>
        @if (previewRows().length) {
          <dl class="m-0 grid grid-cols-3 gap-2" [class.opacity-60]="previewing()">
            @for (r of previewRows(); track r.code) {
              <div class="glass-inset px-2 py-1.5 text-center">
                <dt class="muted text-[0.68rem] font-extrabold">{{ r.name }}</dt>
                <dd class="tabnum m-0 text-[0.98rem] font-extrabold">{{ r.value }}<span class="muted text-[0.65rem]">{{ r.unit }}</span>@if (r.inc) {<span class="text-[0.7rem]" title="未測定の食材を含みます">※</span>}</dd>
              </div>
            }
          </dl>
          @if (hasIncomplete()) { <p class="muted m-0 mt-1.5 text-[0.72rem] font-bold">※未測定の食材を含むため、実際より少ない可能性があります。</p> }
        } @else {
          <p class="muted m-0 text-[0.82rem] font-bold">食材を追加すると、ここに栄養が表示されます。</p>
        }
      </section>
    }

    <!-- ① 名前 -->
    @if (step() === 0) {
      <section class="glass pop-in flex flex-col gap-3 p-4">
        <div>
          <label class="field-label" for="mw-name">メニュー名</label>
          <input id="mw-name" class="field" type="text" maxlength="100" placeholder="例：豚汁" [value]="name()" (input)="name.set($any($event.target).value)" [attr.aria-invalid]="!!errs()['name']" />
          @if (errs()['name']) { <p class="field-error">{{ errs()['name'] }}</p> }
        </div>
        <div>
          <label class="field-label" for="mw-on">作った日</label>
          <input id="mw-on" class="field" type="date" [value]="cookedOn()" (input)="cookedOn.set($any($event.target).value)" />
        </div>
        <div>
          <label class="field-label" for="mw-memo">メモ（任意）</label>
          <input id="mw-memo" class="field" type="text" maxlength="500" [value]="memo()" (input)="memo.set($any($event.target).value)" />
        </div>
      </section>
    }

    <!-- ② 食材 -->
    @if (step() === 1) {
      <section class="flex flex-col gap-3">
        @for (r of rows(); track r.key; let i = $index) {
          <article class="glass pop-in p-3.5">
            <div class="flex items-center justify-between gap-2">
              <h3 class="m-0 min-w-0 truncate text-[1rem] font-extrabold">{{ r.name }}</h3>
              <div class="flex shrink-0 gap-1">
                <button type="button" class="btn btn-sm !min-h-9 !w-9 !p-0" [disabled]="i === 0" (click)="move(i, -1)" [attr.aria-label]="r.name + 'を上へ'"><app-icon name="arrow-up" [size]="15" /></button>
                <button type="button" class="btn btn-sm !min-h-9 !w-9 !p-0" [disabled]="i === rows().length - 1" (click)="move(i, 1)" [attr.aria-label]="r.name + 'を下へ'"><app-icon name="arrow-down" [size]="15" /></button>
                <button type="button" class="btn btn-sm btn-danger !min-h-9 !w-9 !p-0" (click)="removeRow(r.key)" [attr.aria-label]="r.name + 'を削除'"><app-icon name="trash" [size]="15" /></button>
              </div>
            </div>
            <div class="mt-2 flex items-center gap-2">
              <input class="field tabnum !w-28" type="number" inputmode="decimal" min="0" step="any" [value]="r.quantity || ''" (input)="patchRow(r.key, { quantity: num($event) })"
                     [attr.aria-label]="r.name + 'の数量'" [attr.aria-invalid]="!!errs()['items[' + i + '].quantity']" />
              <span class="muted tabnum ml-auto text-[0.8rem] font-bold">= {{ gramsOf(i) }}</span>
            </div>
            <div class="mt-2">
              <app-chip-group [options]="unitOptions(r)" [value]="r.unitId" [label]="r.name + 'の単位'" (changed)="setUnit(r.key, $event)" />
            </div>
            @if (errs()['items[' + i + '].quantity']) { <p class="field-error">{{ errs()['items[' + i + '].quantity'] }}</p> }
          </article>
        } @empty {
          <app-empty-state icon="search" title="食材を追加しましょう" text="にんじん・豚肉・味噌など、かな／カナ／漢字で検索できます" />
        }
        @if (errs()['items']) { <p class="field-error">{{ errs()['items'] }}</p> }
        <button type="button" class="btn" (click)="foodDialog.set(true)"><app-icon name="plus" [size]="18" />食材を追加</button>
        <p class="muted m-0 text-[0.75rem] font-semibold">入力する重さはすべて<b>皮などを含む廃棄前の重さ</b>です。{{ lossNote }}</p>
      </section>
    }

    <!-- ③ 完成重量 -->
    @if (step() === 2) {
      <section class="glass pop-in flex flex-col gap-3 p-4">
        <h2 class="h-sub m-0">完成後の重さ</h2>
        <p class="muted m-0 text-[0.82rem] font-semibold">調理後の料理全体の重さです。鍋ごと量って、空の鍋の重さを引くと簡単です。</p>
        @if (!direct()) {
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="field-label" for="mw-pot">鍋ごとの重さ (g)</label>
              <input id="mw-pot" class="field tabnum" type="number" inputmode="decimal" min="0" step="any" [value]="potG() || ''" (input)="potG.set(num($event))" />
            </div>
            <div>
              <label class="field-label" for="mw-empty">空の鍋の重さ (g)</label>
              <input id="mw-empty" class="field tabnum" type="number" inputmode="decimal" min="0" step="any" [value]="emptyG() || ''" (input)="emptyG.set(num($event))" />
            </div>
          </div>
          <div class="glass-inset flex items-baseline justify-between px-4 py-3">
            <span class="text-[0.85rem] font-extrabold">完成後重量</span>
            <span class="tabnum text-[1.6rem] font-extrabold" [class.text-rose-600]="cookedError()">{{ cooked() > 0 ? fmtNum(cooked(), 1) : '—' }}<span class="muted ml-1 text-[0.8rem]">g</span></span>
          </div>
          @if (potG() > 0 && emptyG() > 0 && potG() <= emptyG()) { <p class="field-error">鍋ごとの重さは、空の鍋より重くしてください。</p> }
        } @else {
          <div>
            <label class="field-label" for="mw-direct">完成後重量 (g)</label>
            <input id="mw-direct" class="field tabnum" type="number" inputmode="decimal" min="0" step="any" [value]="directG() || ''" (input)="directG.set(num($event))" />
          </div>
        }
        @if (errs()['cookedTotalWeightG']) { <p class="field-error">{{ errs()['cookedTotalWeightG'] }}</p> }
        <button type="button" class="btn btn-sm self-start" (click)="direct.set(!direct())">{{ direct() ? '鍋の重さから計算する' : '完成後重量を直接入力する' }}</button>
        @if (gapNote()) {
          <p class="badge st-over !whitespace-normal !rounded-xl !px-3 !py-2 text-[0.78rem]" role="note"><app-icon name="alert" [size]="14" />{{ gapNote() }}</p>
        }
      </section>
    }

    <!-- ④ 1食分 -->
    @if (step() === 3) {
      <section class="glass pop-in flex flex-col gap-3 p-4">
        <h2 class="h-sub m-0">1食分（1杯）の定義</h2>
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="field-label" for="mw-serv">何杯分か</label>
            <input id="mw-serv" class="field tabnum" type="number" inputmode="decimal" min="0" step="0.5" [value]="servings() || ''" (input)="servings.set(num($event))" [attr.aria-invalid]="!!errs()['servings']" />
            @if (errs()['servings']) { <p class="field-error">{{ errs()['servings'] }}</p> }
          </div>
          <div>
            <span class="field-label">呼び方</span>
            <app-chip-group [options]="labelOptions" [value]="label()" label="1杯の呼び方" (changed)="label.set('' + $event)" />
          </div>
        </div>
        <div class="glass-inset flex items-baseline justify-between px-4 py-3">
          <span class="text-[0.85rem] font-extrabold">1杯の重さ</span>
          <span class="tabnum text-[1.4rem] font-extrabold">{{ servingWeight() }}<span class="muted ml-1 text-[0.8rem]">g</span></span>
        </div>
        @if (perServingRows().length) {
          <div>
            <h3 class="h-sub m-0 mb-1.5">{{ label() }}1杯あたり</h3>
            <dl class="m-0 grid grid-cols-3 gap-2">
              @for (r of perServingRows(); track r.code) {
                <div class="glass-inset px-2 py-1.5 text-center">
                  <dt class="muted text-[0.68rem] font-extrabold">{{ r.name }}</dt>
                  <dd class="tabnum m-0 text-[0.98rem] font-extrabold">{{ r.value }}<span class="muted text-[0.65rem]">{{ r.unit }}</span>@if (r.inc) {<span class="text-[0.7rem]">※</span>}</dd>
                </div>
              }
            </dl>
          </div>
        }
        <div class="glass-inset px-3.5 py-3 text-[0.85rem] font-bold">
          <p class="m-0"><span class="muted">メニュー名：</span>{{ name() || '（未入力）' }}</p>
          <p class="m-0"><span class="muted">食材：</span>{{ rows().length }}品・<span class="muted">完成後重量：</span>{{ cooked() > 0 ? fmtNum(cooked(), 1) + 'g' : '—' }}</p>
        </div>
        <p class="muted m-0 text-[0.75rem] font-semibold">{{ lossNote }}@if (editId()) { 保存しても、すでに記録した食事の栄養は変わりません。 }</p>
      </section>
    }

    <!-- 操作 -->
    <div class="flex gap-2">
      <button type="button" class="btn flex-1" (click)="back()">{{ step() === 0 ? 'やめる' : '戻る' }}</button>
      @if (step() < 3) {
        <button type="button" class="btn btn-primary flex-1" (click)="next()">次へ<app-icon name="chevron-right" [size]="16" /></button>
      } @else {
        <button type="button" class="btn btn-primary flex-1" [disabled]="saving()" (click)="save()">{{ saving() ? '保存中…' : '保存する' }}</button>
      }
    </div>
    <app-disclaimer />

    @if (foodDialog()) { <app-food-search-dialog (picked)="addFood($event)" (closed)="foodDialog.set(false)" /> }
  `,
  styles: `
    .step-on { background: rgba(255, 255, 255, 0.55); box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.9); }
    .step-dot { color: #fff; background: linear-gradient(135deg, #5b4bdb, #2f7bf5); }
    .step-dot-off { color: var(--color-ink-soft); background: rgba(255, 255, 255, 0.7); }
  `,
})
export class MenuWizardPage implements OnInit {
  readonly id = input<string>();

  private readonly api = inject(MenusApi);
  private readonly foodsApi = inject(FoodsApi);
  private readonly ui = inject(UiService);
  private readonly router = inject(Router);
  private readonly catalog = inject(NutrientCatalog);

  protected readonly steps = STEPS;
  protected readonly labelOptions: ChipOption[] = ['お椀', '皿', '個', '食'].map((l) => ({ value: l, label: l }));
  protected readonly fmtNum = fmtNum;
  protected readonly lossNote = COOKING_LOSS_NOTE;

  protected readonly editId = computed(() => (this.id() ? Number(this.id()) : null));
  protected readonly step = signal(0);
  protected readonly name = signal('');
  protected readonly cookedOn = signal(todayString());
  protected readonly memo = signal('');
  protected readonly rows = signal<Row[]>([]);
  protected readonly potG = signal(0);
  protected readonly emptyG = signal(0);
  protected readonly directG = signal(0);
  protected readonly direct = signal(false);
  protected readonly servings = signal(4);
  protected readonly label = signal('お椀');
  protected readonly foodDialog = signal(false);
  protected readonly errs = signal<Record<string, string>>({});
  protected readonly saving = signal(false);
  protected readonly preview = signal<MenuPreview | null>(null);
  protected readonly previewing = signal(false);
  private seq = 1;
  private previewSeq = 0;

  protected readonly cooked = computed(() => (this.direct() ? this.directG() : Math.max(this.potG() - this.emptyG(), 0)));
  protected readonly cookedError = computed(() => !this.direct() && this.potG() > 0 && this.potG() <= this.emptyG());
  protected readonly servingWeight = computed(() => {
    const s = this.servings();
    return s > 0 && this.cooked() > 0 ? fmtNum(this.cooked() / s, 0) : '—';
  });

  protected readonly gapNote = computed(() => {
    const sum = this.preview()?.items.reduce((a, i) => a + i.amountG, 0) ?? 0;
    const c = this.cooked();
    if (sum <= 0 || c <= 0) return '';
    const gap = Math.abs(c - sum) / sum;
    return gap > 0.5 ? `食材の合計は約${fmtNum(sum, 0)}g（廃棄前）です。完成後重量が大きく異なります。計量を確認してください。` : '';
  });

  private readonly previewInput = computed<Partial<MenuInput>>(() => ({
    items: this.rows().filter((r) => r.quantity > 0).map((r) => ({ foodId: r.foodId, unitId: r.unitId, quantity: r.quantity })),
    cookedTotalWeightG: this.cooked() > 0 ? this.cooked() : undefined,
    servings: this.servings() > 0 ? this.servings() : undefined,
  }));

  protected readonly previewRows = computed(() => this.toRows(this.preview()?.nutrients ?? [], PREVIEW_CODES));
  protected readonly perServingRows = computed(() => this.toRows(this.preview()?.perServing ?? [], PREVIEW_CODES));
  protected readonly hasIncomplete = computed(() => (this.preview()?.nutrients ?? []).some((n) => PREVIEW_CODES.includes(n.code) && n.isIncomplete));

  constructor() {
    // FR-021：食材・量・完成重量の変更に応じて、保存前でも栄養をリアルタイムに再計算する。
    effect((onCleanup) => {
      const input = this.previewInput();
      if (!input.items || input.items.length === 0) {
        this.preview.set(null);
        return;
      }
      const t = setTimeout(() => void this.runPreview(input), 300);
      onCleanup(() => clearTimeout(t));
    });
  }

  async ngOnInit(): Promise<void> {
    void this.catalog.load();
    if (!this.editId()) return;
    try {
      const m = await this.api.get(this.editId() as number);
      this.name.set(m.name);
      this.cookedOn.set(m.cookedOn ?? todayString());
      this.memo.set(m.memo ?? '');
      this.direct.set(true);
      this.directG.set(m.cookedTotalWeightG);
      this.servings.set(m.servings);
      this.label.set(m.servingLabel);
      const rows: Row[] = [];
      for (const it of m.items) {
        const f = await this.foodsApi.get(it.foodId);
        rows.push({ key: this.seq++, foodId: it.foodId, name: it.foodName, units: f.units, unitId: it.unitId, quantity: it.quantity });
      }
      this.rows.set(rows);
    } catch (e) {
      this.ui.error(toApiError(e).message);
      await this.router.navigateByUrl('/menus');
    }
  }

  private toRows(list: NutrientAmount[], codes: string[]): { code: string; name: string; value: string; unit: string; inc: boolean }[] {
    return codes
      .map((c) => list.find((n) => n.code === c))
      .filter((n): n is NutrientAmount => !!n)
      .map((n) => {
        const def = this.catalog.def(n.code);
        return { code: n.code, name: def?.nameJa ?? n.code, value: fmtAmount(n.amount, def?.unit ?? ''), unit: def?.unit ?? '', inc: n.isIncomplete };
      });
  }

  private async runPreview(input: Partial<MenuInput>): Promise<void> {
    const seq = ++this.previewSeq;
    this.previewing.set(true);
    try {
      const p = await this.api.preview(input);
      if (seq === this.previewSeq) this.preview.set(p);
    } catch {
      // 直近の成功値を残す
    } finally {
      if (seq === this.previewSeq) this.previewing.set(false);
    }
  }

  protected num(ev: Event): number {
    const v = (ev.target as HTMLInputElement).value;
    return v === '' ? 0 : Number(v);
  }

  protected gramsOf(i: number): string {
    const g = this.preview()?.items[i]?.amountG;
    const r = this.rows()[i];
    const approx = r ? r.quantity * (r.unitId === null ? 1 : (r.units.find((u) => u.id === r.unitId)?.gramsPerUnit ?? 1)) : 0;
    return `${fmtNum(g ?? approx, 0)}g`;
  }

  protected addFood(f: FoodSummary): void {
    this.foodDialog.set(false);
    const def = f.units.find((u) => u.isDefault) ?? null;
    this.rows.update((l) => [...l, { key: this.seq++, foodId: f.id, name: f.name, units: f.units, unitId: def?.id ?? null, quantity: def ? 1 : 100 }]);
  }

  protected removeRow(key: number): void {
    this.rows.update((l) => l.filter((r) => r.key !== key));
  }

  protected move(i: number, d: number): void {
    this.rows.update((l) => {
      const out = [...l];
      const j = i + d;
      const a = out[i];
      const b = out[j];
      if (!a || !b) return l;
      out[i] = b;
      out[j] = a;
      return out;
    });
  }

  protected patchRow(key: number, p: Partial<Row>): void {
    this.rows.update((l) => l.map((r) => (r.key === key ? { ...r, ...p } : r)));
  }

  protected unitOptions(r: Row): ChipOption[] {
    return [{ value: null, label: 'g' }, ...r.units.map((u) => ({ value: u.id, label: u.unitName }))];
  }

  protected setUnit(key: number, v: ChipValue): void {
    this.patchRow(key, { unitId: v === null ? null : Number(v) });
  }

  // ---- 手順の移動 ----
  protected next(): void {
    this.errs.set({});
    const s = this.step();
    if (s === 0 && !this.name().trim()) {
      this.errs.set({ name: 'メニュー名を入力してください' });
      return;
    }
    if (s === 1 && this.rows().filter((r) => r.quantity > 0).length === 0) {
      this.errs.set({ items: '数量が入った食材を1件以上追加してください' });
      return;
    }
    if (s === 2 && !(this.cooked() > 0)) {
      this.errs.set({ cookedTotalWeightG: '完成後重量を入力してください（0より大きい値）' });
      return;
    }
    this.step.set(s + 1);
  }

  protected async back(): Promise<void> {
    if (this.step() > 0) {
      this.step.update((s) => s - 1);
      return;
    }
    const dirty = !!this.name().trim() || this.rows().length > 0;
    if (dirty && !this.editId()) {
      const ok = await this.ui.confirm({ title: '入力を破棄', message: '入力した内容は保存されません。やめますか？', okLabel: 'やめる', danger: true });
      if (!ok) return;
    }
    await this.router.navigateByUrl('/menus');
  }

  protected async save(): Promise<void> {
    this.errs.set({});
    const input: MenuInput = {
      name: this.name().trim(), cookedOn: this.cookedOn() || null, memo: this.memo().trim() || null,
      cookedTotalWeightG: this.cooked(), servings: this.servings(), servingLabel: this.label(),
      items: this.rows().map((r) => ({ foodId: r.foodId, unitId: r.unitId, quantity: r.quantity })),
    };
    this.saving.set(true);
    try {
      if (this.editId()) await this.api.update(this.editId() as number, input);
      else await this.api.create(input);
      this.ui.toast('メニューを保存しました');
      await this.router.navigateByUrl('/menus');
    } catch (e) {
      const fe = fieldErrors(e);
      this.errs.set(fe);
      this.ui.error(toApiError(e).message);
      if (fe['name']) this.step.set(0);
      else if (Object.keys(fe).some((k) => k.startsWith('items'))) this.step.set(1);
      else if (fe['cookedTotalWeightG']) this.step.set(2);
    } finally {
      this.saving.set(false);
    }
  }
}
