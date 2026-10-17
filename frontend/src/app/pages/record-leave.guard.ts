import { CanDeactivateFn } from '@angular/router';
import type { RecordPage } from './record.page';

/** 食事記録の画面を離れる前に、未保存の変更を保存する（戻る・他のタブへの移動を含む）。 */
export const recordLeaveGuard: CanDeactivateFn<RecordPage> = (component) => component.canLeave();
