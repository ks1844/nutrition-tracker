// SCR-05 メニュー作成/編集ウィザード（FR-010, 011, 020〜025）
import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MenusApi } from '../../api/menus.api';
import { FoodsApi } from '../../api/foods.api';
import { fieldErrors, toApiError } from '../../api/api-client';
import { FoodSummary, FoodUnit, MenuInput, MenuPreview, NutrientAmount } from '../../types/api';
import { UiService } from '../../services/ui.service';
import { NutrientCatalog } from '../../services/nutrient-catalog';
import { COOKING_LOSS_NOTE, fmtAmount, fmtNum } from '../../services/format';
import { todayString } from '../../services/date-util';
import { PageHeader } from '../../components/page-header/page-header';
import { FoodSearchDialog } from '../../components/food-search-dialog/food-search-dialog';
import { Disclaimer } from '../../components/disclaimer/disclaimer';
import { EmptyState } from '../../components/empty-state/empty-state';
import { Icon } from '../../components/icon/icon';
import { ChipGroup, ChipOption, ChipValue } from '../../components/chip-group/chip-group';

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
  host: { class: 'page' },
  templateUrl: './menu-wizard.html',
  styleUrl: './menu-wizard.css',
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
