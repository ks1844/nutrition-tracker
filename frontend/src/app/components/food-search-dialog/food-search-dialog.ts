import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, output, signal } from '@angular/core';
import { FoodsApi } from '../../api/foods.api';
import { toApiError } from '../../api/api-client';
import { FoodDetail, FoodSummary } from '../../types/api';
import { NutrientCatalog } from '../../services/nutrient-catalog';
import { fmtAmount, fmtNum } from '../../services/format';
import { Icon } from '../icon/icon';

/** CMP-01 食材検索ダイアログ（FR-010 検索 / FR-011 食材詳細）。 */
@Component({
  selector: 'app-food-search-dialog',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './food-search-dialog.html',
})
export class FoodSearchDialog implements OnInit {
  readonly picked = output<FoodSummary>();
  readonly closed = output<void>();

  private readonly api = inject(FoodsApi);
  private readonly catalog = inject(NutrientCatalog);
  protected readonly samples = ['にんじん', '豚肉', 'ご飯', '卵', '味噌', 'ブロッコリー'];
  protected readonly q = signal('');
  protected readonly results = signal<FoodSummary[]>([]);
  protected readonly loading = signal(false);
  protected readonly searched = signal(false);
  protected readonly error = signal('');
  protected readonly expandedId = signal<number | null>(null);
  protected readonly detail = signal<FoodDetail | null>(null);
  private timer: ReturnType<typeof setTimeout> | undefined;

  protected readonly rows = computed(() => {
    const d = this.detail();
    if (!d) return [];
    return d.nutrients.map((n) => {
      const def = this.catalog.def(n.code);
      const unit = def?.unit ?? '';
      const text =
        n.per100g === null ? '未測定'
        : n.isTrace ? '微量(Tr)'
        : `${fmtAmount(n.per100g, unit)}${unit}`;
      return { code: n.code, name: def?.nameJa ?? n.code, text };
    });
  });

  ngOnInit(): void {
    void this.catalog.load();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.closed.emit();
  }

  protected unitsText(f: FoodSummary): string {
    return f.units.map((u) => `${u.unitName}=${fmtNum(u.gramsPerUnit, 0)}g`).join('／');
  }

  protected onInput(value: string): void {
    this.q.set(value);
    clearTimeout(this.timer);
    this.expandedId.set(null);
    if (!value.trim()) {
      this.results.set([]);
      this.searched.set(false);
      return;
    }
    this.loading.set(true);
    this.timer = setTimeout(() => void this.run(value), 250);
  }

  private async run(value: string): Promise<void> {
    try {
      const r = await this.api.search(value.trim());
      if (value === this.q()) {
        this.results.set(r.items);
        this.searched.set(true);
        this.error.set('');
      }
    } catch (e) {
      this.error.set(toApiError(e).message);
    } finally {
      this.loading.set(false);
    }
  }

  protected async toggle(f: FoodSummary): Promise<void> {
    if (this.expandedId() === f.id) {
      this.expandedId.set(null);
      return;
    }
    this.expandedId.set(f.id);
    this.detail.set(null);
    try {
      const d = await this.api.get(f.id);
      if (this.expandedId() === f.id) this.detail.set(d);
    } catch (e) {
      this.error.set(toApiError(e).message);
    }
  }
}
