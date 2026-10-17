import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { addDays, formatJa, todayString } from '../../services/date-util';
import { Icon } from '../icon/icon';

@Component({
  selector: 'app-date-nav',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './date-nav.html',
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
