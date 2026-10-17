import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { addDays, formatJa, todayString } from '../services/date-util';
import { Icon } from './icon';

@Component({
  selector: 'app-date-nav',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="glass flex items-center justify-between gap-1 px-2 py-1.5" role="group" aria-label="日付の切り替え">
      <button type="button" class="btn btn-sm !w-10 !p-0" (click)="changed.emit(prev())" aria-label="前の日"><app-icon name="chevron-left" /></button>
      <div class="text-center">
        <div class="tabnum text-[1.05rem] font-extrabold">{{ label() }}</div>
        @if (isToday()) { <div class="muted text-[0.7rem] font-bold">今日</div> }
        @else { <button type="button" class="muted text-[0.7rem] font-bold underline" (click)="changed.emit(today)">今日に戻る</button> }
      </div>
      <button type="button" class="btn btn-sm !w-10 !p-0" [disabled]="!canNext()" (click)="changed.emit(next())" aria-label="次の日"><app-icon name="chevron-right" /></button>
    </div>
  `,
})
export class DateNav {
  readonly date = input.required<string>();
  readonly step = input(1);
  readonly changed = output<string>();
  protected readonly today = todayString();
  protected readonly label = computed(() => formatJa(this.date()));
  protected readonly isToday = computed(() => this.date() === this.today);
  protected readonly prev = computed(() => addDays(this.date(), -this.step()));
  protected readonly next = computed(() => addDays(this.date(), this.step()));
  protected readonly canNext = computed(() => this.next() <= this.today);
}
