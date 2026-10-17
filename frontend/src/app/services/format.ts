import { Goal, InputMode, MealRecordItemView, MealType, NutrientStatus, ScoreBand } from '../types/api';
import { IconName } from '../components/icon/icon';

export const MEAL_LABEL: Record<MealType, string> = {
  breakfast: '朝食',
  lunch: '昼食',
  dinner: '夕食',
  snack: '間食',
};
export const MEAL_ORDER: MealType[] = ['breakfast', 'lunch', 'dinner', 'snack'];

export const GOAL_LABEL: Record<Goal, string> = { skin: '美肌', muscle: '筋肉', both: '美肌と筋肉' };

export const ACTIVITY_LABEL: Record<number, string> = {
  1: '1：ほぼ座って過ごす',
  2: '2：軽い活動（立ち仕事・通勤など）',
  3: '3：よく動く（運動習慣あり）',
  4: '4：かなり活発（毎日しっかり運動）',
};

export const INPUT_MODE_LABEL: Record<InputMode, string> = {
  RATIO: '割合',
  SERVING: '杯',
  GRAM: 'g',
  UNIT: '単位',
};

/** 栄養値の表示（保存値から作る。kcal は整数、それ以外は小数 1 桁）。 */
export function fmtAmount(value: number, unit: string): string {
  if (unit === 'kcal') return Math.round(value).toLocaleString('ja-JP');
  return value.toLocaleString('ja-JP', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}

export function fmtNum(value: number, digits = 1): string {
  return value.toLocaleString('ja-JP', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

export function fmtPct(ratio: number, digits = 0): string {
  return `${(ratio * 100).toFixed(digits)}%`;
}

export interface StatusMeta {
  label: string;
  icon: IconName;
  badge: string;
  fill: string;
}

export const STATUS_META: Record<NutrientStatus, StatusMeta> = {
  DEFICIENT: { label: '不足', icon: 'arrow-down', badge: 'st-deficient', fill: 'fill-deficient' },
  ACHIEVED: { label: '達成', icon: 'check', badge: 'st-achieved', fill: 'fill-achieved' },
  OVER_UL: { label: '上限超過', icon: 'alert', badge: 'st-over', fill: 'fill-over' },
  NO_TARGET: { label: '目標なし', icon: 'info', badge: 'st-none', fill: 'fill-none' },
};

export interface BandMeta {
  label: string;
  icon: IconName;
  badge: string;
  stroke: string;
}

export const BAND_META: Record<ScoreBand, BandMeta> = {
  GOOD: { label: '良好', icon: 'check', badge: 'st-achieved', stroke: '#10b981' },
  FAIR: { label: 'もう少し', icon: 'trend-up', badge: 'st-deficient', stroke: '#3b82f6' },
  LOW: { label: '不足気味', icon: 'arrow-down', badge: 'st-deficient', stroke: '#6366f1' },
};

export const DISCLAIMER = '食事摂取基準に基づく目安であり、医療的助言ではありません。';
export const COOKING_LOSS_NOTE = '調理による栄養の変化は考慮していません。';

/** 記録項目の表示用の説明（例：豚汁 1.5杯／ご飯 150g）。 */
export function describeItem(it: MealRecordItemView): string {
  if (it.menuName) {
    const v = it.inputValue;
    const amount = it.inputMode === 'SERVING' ? `${v}杯` : it.inputMode === 'RATIO' ? `${Math.round(v * 100)}%` : `${v}g`;
    return `${it.menuName} ${amount}`;
  }
  return `${it.foodName ?? ''} ${it.amountG !== null ? fmtNum(it.amountG, 0) : ''}g`;
}

export function itemKcal(it: MealRecordItemView): number {
  return it.nutrients.find((n) => n.code === 'ENERGY')?.amount ?? 0;
}

/** 現在時刻から食事区分の初期値を決める。 */
export function guessMealType(now = new Date()): MealType {
  const h = now.getHours();
  return h < 10 ? 'breakfast' : h < 15 ? 'lunch' : h < 21 ? 'dinner' : 'snack';
}
