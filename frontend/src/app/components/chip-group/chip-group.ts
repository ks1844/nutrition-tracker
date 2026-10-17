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
  templateUrl: './chip-group.html',
})
export class ChipGroup {
  readonly options = input.required<ChipOption[]>();
  readonly value = input<ChipValue>(null);
  readonly label = input.required<string>();
  /** true なら縦に 1 列で並べる（選択肢の文言が長いとき）。 */
  readonly stack = input(false);
  readonly changed = output<ChipValue>();
}
