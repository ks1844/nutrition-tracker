// 【使い捨て】Mock API のメモリ上データ。localStorage に保存して再読込後も残す（プロトタイプ確認用）。

import { InputMode, MealType, NutrientAmount, ProfileInput } from '../../types/api';
import { addDays, todayString } from '../../services/date-util';
import {
  Totals,
  emptyTotals,
  addTotals,
  foodNutrition,
  roundHalfUp,
  scaleTotals,
  toAmounts,
  toAmountG,
  unitIdOf,
} from './mock-calc';

export interface StoredMenuItem {
  id: number;
  foodId: number;
  unitId: number | null;
  quantity: number;
  amountG: number;
  sortOrder: number;
}

export interface StoredMenu {
  id: number;
  name: string;
  cookedOn: string | null;
  memo: string | null;
  cookedTotalWeightG: number;
  servings: number;
  servingLabel: string;
  items: StoredMenuItem[];
  deletedAt: string | null;
}

export interface StoredRecordItem {
  id: number;
  menuId: number | null;
  foodId: number | null;
  unitId: number | null;
  inputMode: InputMode;
  inputValue: number;
  portionRatio: number | null;
  amountG: number | null;
  nutrients: NutrientAmount[];
}

export interface StoredRecord {
  id: number;
  eatenOn: string;
  mealType: MealType;
  memo: string | null;
  items: StoredRecordItem[];
}

export interface MockState {
  profile: ProfileInput | null;
  menus: StoredMenu[];
  records: StoredRecord[];
  seq: { menu: number; menuItem: number; record: number; recordItem: number };
}

const KEY = 'nt-mock-state-v2';

export function menuTotals(menu: StoredMenu): Totals {
  let t = emptyTotals();
  for (const it of menu.items) t = addTotals(t, foodNutrition(it.foodId, it.amountG));
  return t;
}

export function newItemNutrients(menu: StoredMenu | null, foodId: number | null, ratio: number | null, amountG: number | null): NutrientAmount[] {
  if (menu && ratio != null) return scaleTotals(menuTotals(menu), ratio);
  if (foodId != null && amountG != null) return toAmounts(foodNutrition(foodId, amountG));
  return toAmounts(emptyTotals());
}

function seed(): MockState {
  const state: MockState = {
    profile: {
      sex: 'female',
      birthDate: '1996-04-01',
      heightCm: 160,
      weightKg: 55,
      activityLevel: 2,
      goal: 'both',
      energyTargetKcal: null,
    },
    menus: [],
    records: [],
    seq: { menu: 1, menuItem: 1, record: 1, recordItem: 1 },
  };
  const today = todayString();

  const mkMenu = (
    name: string,
    cookedOn: string,
    cooked: number,
    servings: number,
    label: string,
    items: { foodId: number; unitIndex: number | null; quantity: number }[],
    memo: string | null,
  ): StoredMenu => {
    const id = state.seq.menu++;
    const menu: StoredMenu = {
      id, name, cookedOn, memo, cookedTotalWeightG: cooked, servings, servingLabel: label, deletedAt: null, items: [],
    };
    items.forEach((it, i) => {
      const unitId = it.unitIndex == null ? null : unitIdOf(it.foodId, it.unitIndex);
      menu.items.push({
        id: state.seq.menuItem++, foodId: it.foodId, unitId, quantity: it.quantity,
        amountG: toAmountG(it.foodId, unitId, it.quantity), sortOrder: i,
      });
    });
    state.menus.push(menu);
    return menu;
  };

  // 豚汁: docs/03.栄養計算仕様.md §9 の正解データと同じ構成
  const buta = mkMenu('豚汁', addDays(today, -3), 2400, 8, 'お椀', [
    { foodId: 1, unitIndex: 0, quantity: 2 },
    { foodId: 2, unitIndex: null, quantity: 400 },
    { foodId: 3, unitIndex: 0, quantity: 4 },
  ], 'テスト用の正解データと同じ構成');
  const chicken = mkMenu('蒸し鶏とブロッコリー', addDays(today, -4), 900, 4, '皿', [
    { foodId: 5, unitIndex: 0, quantity: 2 },
    { foodId: 11, unitIndex: 0, quantity: 1 },
    { foodId: 18, unitIndex: 0, quantity: 1 },
  ], null);

  const addRecord = (
    offset: number,
    mealType: MealType,
    items: { menu?: StoredMenu; foodId?: number; unitIndex?: number | null; mode: InputMode; value: number }[],
  ): void => {
    const rec: StoredRecord = { id: state.seq.record++, eatenOn: addDays(today, offset), mealType, memo: null, items: [] };
    for (const it of items) {
      let ratio: number | null = null;
      let amountG: number | null = null;
      let unitId: number | null = null;
      if (it.menu) {
        ratio =
          it.mode === 'SERVING' ? roundHalfUp(it.value / it.menu.servings, 6)
          : it.mode === 'GRAM' ? roundHalfUp(it.value / it.menu.cookedTotalWeightG, 6)
          : it.value;
        if (it.mode === 'GRAM') amountG = it.value;
      } else if (it.foodId != null) {
        unitId = it.unitIndex == null || it.unitIndex === undefined ? null : unitIdOf(it.foodId, it.unitIndex);
        amountG = toAmountG(it.foodId, unitId, it.value);
      }
      rec.items.push({
        id: state.seq.recordItem++,
        menuId: it.menu?.id ?? null,
        foodId: it.foodId ?? null,
        unitId,
        inputMode: it.mode,
        inputValue: it.value,
        portionRatio: ratio,
        amountG,
        nutrients: newItemNutrients(it.menu ?? null, it.foodId ?? null, ratio, amountG),
      });
    }
    state.records.push(rec);
  };

  // 食材は { foodId, unitIndex: 0, mode: 'UNIT', value: 個数 } の形で追加（単位が無い食材は g）。
  const u = (foodId: number, value: number): { foodId: number; unitIndex: number; mode: InputMode; value: number } => ({ foodId, unitIndex: 0, mode: 'UNIT', value });
  const bread = 15, egg = 7, milk = 16, banana = 20, yogurt = 21, rice = 4, natto = 17, tomato = 23, salmon = 6, spinach = 10, tofu = 14, broccoli = 11, cabbage = 8, soy = 18;

  // 今日（昼食まで）
  addRecord(0, 'breakfast', [u(bread, 1), u(egg, 1), u(milk, 1), u(banana, 1)]);
  addRecord(0, 'lunch', [{ menu: buta, mode: 'SERVING', value: 1.5 }, u(rice, 1), u(natto, 1), u(tomato, 1)]);
  // 昨日
  addRecord(-1, 'breakfast', [u(yogurt, 1), u(banana, 1), u(bread, 1)]);
  addRecord(-1, 'lunch', [{ menu: chicken, mode: 'SERVING', value: 1 }, u(rice, 1.5), u(egg, 1)]);
  addRecord(-1, 'dinner', [{ menu: buta, mode: 'SERVING', value: 1 }, u(rice, 1), u(salmon, 1), u(spinach, 0.5), u(tofu, 0.5)]);
  // 2 日前
  addRecord(-2, 'breakfast', [u(bread, 2), u(milk, 1), u(egg, 1)]);
  addRecord(-2, 'lunch', [u(rice, 1.5), u(natto, 1), u(spinach, 0.5), u(egg, 1), u(soy, 1)]);
  addRecord(-2, 'dinner', [{ menu: chicken, mode: 'SERVING', value: 1 }, u(rice, 1.5), u(tomato, 1), { menu: buta, mode: 'SERVING', value: 1.5 }]);
  // 4・5 日前（3 日前は記録なし）
  addRecord(-4, 'lunch', [u(rice, 1), u(egg, 2), u(cabbage, 2), u(yogurt, 1)]);
  addRecord(-4, 'dinner', [{ menu: buta, mode: 'SERVING', value: 1 }, u(rice, 1), u(salmon, 1), u(broccoli, 0.5)]);
  addRecord(-5, 'breakfast', [u(bread, 1), u(yogurt, 1), u(banana, 1)]);
  addRecord(-5, 'dinner', [{ menu: chicken, mode: 'SERVING', value: 1.5 }, u(rice, 1.5), u(tofu, 0.5), u(tomato, 1)]);
  return state;
}

function load(): MockState {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) return JSON.parse(raw) as MockState;
  } catch {
    // localStorage が使えない環境では毎回初期データ
  }
  return seed();
}

let state: MockState = load();

export function getState(): MockState {
  return state;
}

export function persist(): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(state));
  } catch {
    // 保存できなくても動作は継続
  }
}

export function resetState(withProfile: boolean): void {
  state = seed();
  if (!withProfile) state.profile = null;
  persist();
}

export function nextId(kind: keyof MockState['seq']): number {
  return state.seq[kind]++;
}
