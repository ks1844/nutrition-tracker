import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export type ChipValue = string | number | null;
export interface ChipOption {
  value: ChipValue;
  label: string;
}

/**
 * 選択肢をチップで並べる（ラジオ相当）。OS 標準のプルダウンは、開くと小さく表示されて押しにくいため使わない。
 * 画面幅・PC/スマホのどちらでも、押しやすい大きさ（高さ 2.5rem 以上）で表示する。
 */
@Component({
  selector: 'app-chip-group',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="radiogroup" [attr.aria-label]="label()" class="flex gap-2" [class]="stack() ? 'flex-col' : 'flex-wrap'">
      @for (o of options(); track o.value) {
        <button
          type="button"
          class="chip"
          [class.!justify-start]="stack()"
          [class.!rounded-2xl]="stack()"
          [class.!text-left]="stack()"
          role="radio"
          [attr.aria-checked]="o.value === value()"
          [attr.aria-pressed]="o.value === value()"
          (click)="changed.emit(o.value)"
        >{{ o.label }}</button>
      }
    </div>
  `,
})
export class ChipGroup {
  readonly options = input.required<ChipOption[]>();
  readonly value = input<ChipValue>(null);
  readonly label = input.required<string>();
  /** true なら縦に 1 列で並べる（選択肢の文言が長いとき）。 */
  readonly stack = input(false);
  readonly changed = output<ChipValue>();
}
