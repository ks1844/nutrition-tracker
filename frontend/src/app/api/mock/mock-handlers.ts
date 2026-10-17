// 【使い捨て】Mock API のルーティングと処理。レスポンスの形は docs/09.API仕様.md に合わせる。

import {
  ApiError,
  ContributionItem,
  Contributions,
  CopyResult,
  DailyDashboard,
  DashboardNotice,
  FoodSearchResult,
  MealRecordInput,
  MealRecordSaved,
  MealRecordView,
  MealRecordsOfDay,
  MealType,
  MenuDetail,
  MenuInput,
  MenuPreview,
  MenuSummary,
  NutrientStatusView,
  PfcView,
  Profile,
  ProfileInput,
  RecentItems,
  Remaining,
  RemainingMenu,
  SeriesPoint,
  WeeklyDashboard,
} from '../../types/api';
import { addDays, parseDate, todayString } from '../../services/date-util';
import { DUMMY_LOWER_TARGETS, DUMMY_SALT_MAX_G, NUTRIENTS, NUTRIENT_CODES } from './mock-data';
import {
  MealStat,
  Totals,
  computeScores,
  emptyTotals,
  findUnit,
  foodDef,
  foodDetail,
  foodNutrition,
  foodSummary,
  profileView,
  roundHalfUp,
  scaleTotals,
  searchFoods,
  toAmountG,
  toAmounts,
  totalsFromAmounts,
  addTotals,
} from './mock-calc';
import {
  StoredMenu,
  StoredRecord,
  StoredRecordItem,
  getState,
  menuTotals,
  newItemNutrients,
  nextId,
  persist,
  resetState,
} from './mock-store';

export class MockHttpError extends Error {
  constructor(
    readonly status: number,
    readonly body: ApiError,
  ) {
    super(body.message);
  }
}

export interface MockResponse {
  status: number;
  body: unknown;
}

const ok = (body: unknown, status = 200): MockResponse => ({ status, body });
const noContent = (): MockResponse => ({ status: 204, body: null });

function fail(status: number, code: ApiError['code'], message: string, details: ApiError['details'] = []): never {
  throw new MockHttpError(status, { code, message, details });
}

function validation(details: ApiError['details']): never {
  fail(400, 'VALIDATION_ERROR', '入力内容に誤りがあります', details);
}

const MEAL_ORDER: MealType[] = ['breakfast', 'lunch', 'dinner', 'snack'];

// ---------- メニュー ----------
function activeMenu(id: number): StoredMenu {
  const m = getState().menus.find((x) => x.id === id && !x.deletedAt);
  if (!m) fail(404, 'NOT_FOUND', '対象が見つかりません');
  return m;
}

function remainingOf(menu: StoredMenu): Remaining {
  let used = 0;
  for (const r of getState().records) for (const it of r.items) if (it.menuId === menu.id) used += it.portionRatio ?? 0;
  const ratio = roundHalfUp(1 - used, 6);
  return { ratio, servings: roundHalfUp(ratio * menu.servings, 2), isOver: ratio < 0 };
}

function menuSummary(m: StoredMenu): MenuSummary {
  return {
    id: m.id,
    name: m.name,
    cookedOn: m.cookedOn,
    cookedTotalWeightG: m.cookedTotalWeightG,
    servings: m.servings,
    servingLabel: m.servingLabel,
    servingWeightG: roundHalfUp(m.cookedTotalWeightG / m.servings, 1),
    remaining: remainingOf(m),
  };
}

function menuDetail(m: StoredMenu): MenuDetail {
  const totals = menuTotals(m);
  return {
    ...menuSummary(m),
    memo: m.memo,
    items: m.items.map((it) => ({
      id: it.id,
      foodId: it.foodId,
      foodName: foodDef(it.foodId)?.name ?? '',
      unitId: it.unitId,
      unitName: it.unitId == null ? null : (findUnit(it.foodId, it.unitId)?.unitName ?? null),
      quantity: it.quantity,
      amountG: it.amountG,
      sortOrder: it.sortOrder,
    })),
    nutrients: toAmounts(totals),
    perServing: scaleTotals(totals, roundHalfUp(1 / m.servings, 6)),
  };
}

function validateMenu(b: MenuInput): void {
  const d: ApiError['details'] = [];
  if (!b.name || !b.name.trim()) d.push({ field: 'name', reason: 'メニュー名を入力してください' });
  else if (b.name.length > 100) d.push({ field: 'name', reason: '100文字以内で入力してください' });
  if (!(b.cookedTotalWeightG > 0)) d.push({ field: 'cookedTotalWeightG', reason: '0より大きい値を指定してください' });
  if (!(b.servings > 0)) d.push({ field: 'servings', reason: '0より大きい値を指定してください' });
  if (b.servingLabel && b.servingLabel.length > 20) d.push({ field: 'servingLabel', reason: '20文字以内で入力してください' });
  if (b.memo && b.memo.length > 500) d.push({ field: 'memo', reason: '500文字以内で入力してください' });
  if (!b.items || b.items.length === 0) d.push({ field: 'items', reason: '食材を1件以上追加してください' });
  (b.items ?? []).forEach((it, i) => {
    if (!foodDef(it.foodId)) d.push({ field: `items[${i}].foodId`, reason: '存在しない食材です' });
    if (!(it.quantity > 0)) d.push({ field: `items[${i}].quantity`, reason: '0より大きい値を指定してください' });
    if (it.unitId != null && !findUnit(it.foodId, it.unitId))
      d.push({ field: `items[${i}].unitId`, reason: 'この食材の単位ではありません' });
  });
  if (d.length) validation(d);
}

function applyMenu(m: StoredMenu, b: MenuInput): void {
  m.name = b.name.trim();
  m.cookedOn = b.cookedOn || null;
  m.memo = b.memo || null;
  m.cookedTotalWeightG = roundHalfUp(b.cookedTotalWeightG, 1);
  m.servings = roundHalfUp(b.servings, 1);
  m.servingLabel = b.servingLabel || 'お椀';
  m.items = b.items.map((it, i) => ({
    id: nextId('menuItem'),
    foodId: it.foodId,
    unitId: it.unitId,
    quantity: it.quantity,
    amountG: toAmountG(it.foodId, it.unitId, it.quantity),
    sortOrder: i,
  }));
}

function preview(b: Partial<MenuInput>): MenuPreview {
  const items = (b.items ?? []).filter((it) => foodDef(it.foodId) && it.quantity > 0);
  let total = emptyTotals();
  const views = items.map((it) => {
    const def = foodDef(it.foodId);
    const amountG = toAmountG(it.foodId, it.unitId, it.quantity);
    const t = foodNutrition(it.foodId, amountG);
    total = addTotals(total, t);
    return {
      foodId: it.foodId,
      foodName: def?.name ?? '',
      amountG,
      edibleG: roundHalfUp(amountG * (1 - (def?.refuseRatePct ?? 0) / 100), 2),
      nutrients: toAmounts(t),
    };
  });
  const hasServing = !!(b.servings && b.servings > 0 && b.cookedTotalWeightG && b.cookedTotalWeightG > 0);
  return {
    items: views,
    nutrients: toAmounts(total),
    servingWeightG: hasServing ? roundHalfUp((b.cookedTotalWeightG ?? 0) / (b.servings ?? 1), 1) : null,
    perServing: hasServing ? scaleTotals(total, roundHalfUp(1 / (b.servings ?? 1), 6)) : null,
    warnings: [],
  };
}

// ---------- 食事記録 ----------
function recordView(r: StoredRecord): MealRecordView {
  return {
    id: r.id,
    eatenOn: r.eatenOn,
    mealType: r.mealType,
    memo: r.memo,
    items: r.items.map((it) => ({
      id: it.id,
      menuId: it.menuId,
      menuName: it.menuId == null ? null : (getState().menus.find((m) => m.id === it.menuId)?.name ?? null),
      foodId: it.foodId,
      foodName: it.foodId == null ? null : (foodDef(it.foodId)?.name ?? null),
      unitId: it.unitId,
      inputMode: it.inputMode,
      inputValue: it.inputValue,
      portionRatio: it.portionRatio,
      amountG: it.amountG,
      nutrients: it.nutrients,
    })),
  };
}

function buildRecordItems(b: MealRecordInput, allowDeletedMenuIds: number[] = []): StoredRecordItem[] {
  const d: ApiError['details'] = [];
  if (!b.eatenOn) d.push({ field: 'eatenOn', reason: '日付を指定してください' });
  if (!MEAL_ORDER.includes(b.mealType)) d.push({ field: 'mealType', reason: '食事区分を指定してください' });
  if (!b.items || b.items.length === 0) d.push({ field: 'items', reason: '1件以上の項目を追加してください' });
  const out: StoredRecordItem[] = [];
  (b.items ?? []).forEach((it, i) => {
    const hasMenu = it.menuId != null;
    const hasFood = it.foodId != null;
    if (hasMenu === hasFood) {
      d.push({ field: `items[${i}]`, reason: 'メニューか食材のどちらか一方を指定してください' });
      return;
    }
    if (!(it.inputValue > 0)) {
      d.push({ field: `items[${i}].inputValue`, reason: '0より大きい値を指定してください' });
      return;
    }
    if (hasMenu) {
      const menu = getState().menus.find((m) => m.id === it.menuId);
      if (!menu || (menu.deletedAt && !allowDeletedMenuIds.includes(menu.id))) {
        d.push({ field: `items[${i}].menuId`, reason: '選択できないメニューです' });
        return;
      }
      if (!['RATIO', 'SERVING', 'GRAM'].includes(it.inputMode)) {
        d.push({ field: `items[${i}].inputMode`, reason: 'メニューは割合・杯数・gのいずれかです' });
        return;
      }
      const ratio =
        it.inputMode === 'SERVING' ? roundHalfUp(it.inputValue / menu.servings, 6)
        : it.inputMode === 'GRAM' ? roundHalfUp(it.inputValue / menu.cookedTotalWeightG, 6)
        : it.inputValue;
      out.push({
        id: nextId('recordItem'), menuId: menu.id, foodId: null, unitId: null, inputMode: it.inputMode,
        inputValue: it.inputValue, portionRatio: ratio, amountG: it.inputMode === 'GRAM' ? it.inputValue : null,
        nutrients: newItemNutrients(menu, null, ratio, null),
      });
    } else {
      const foodId = it.foodId as number;
      if (!foodDef(foodId)) {
        d.push({ field: `items[${i}].foodId`, reason: '存在しない食材です' });
        return;
      }
      if (!['GRAM', 'UNIT'].includes(it.inputMode)) {
        d.push({ field: `items[${i}].inputMode`, reason: '食材はgか単位です' });
        return;
      }
      const unitId = it.inputMode === 'UNIT' ? (it.unitId ?? null) : null;
      if (it.inputMode === 'UNIT' && (unitId == null || !findUnit(foodId, unitId))) {
        d.push({ field: `items[${i}].unitId`, reason: '単位を指定してください' });
        return;
      }
      const amountG = toAmountG(foodId, unitId, it.inputValue);
      out.push({
        id: nextId('recordItem'), menuId: null, foodId, unitId, inputMode: it.inputMode,
        inputValue: it.inputValue, portionRatio: null, amountG,
        nutrients: newItemNutrients(null, foodId, null, amountG),
      });
    }
  });
  if (d.length) validation(d);
  return out;
}

function warningsFor(items: StoredRecordItem[]): MealRecordSaved['warnings'] {
  const ids = [...new Set(items.map((i) => i.menuId).filter((x): x is number => x != null))];
  const out: MealRecordSaved['warnings'] = [];
  for (const id of ids) {
    const menu = getState().menus.find((m) => m.id === id);
    if (!menu) continue;
    const rem = remainingOf(menu);
    const total = roundHalfUp(1 - rem.ratio, 6);
    if (total > 1.0000001) {
      out.push({
        code: 'MENU_OVER_100PCT',
        menuId: id,
        totalPortionRatio: total,
        message: `${menu.name}の記録が合計${Math.round(total * 100)}%を超えています`,
      });
    }
  }
  return out;
}

// ---------- 集計 ----------
interface DayData {
  date: string;
  hasRecords: boolean;
  totals: Totals;
  mainMealProtein: number[];
}

function dayData(date: string): DayData {
  const recs = getState().records.filter((r) => r.eatenOn === date && r.items.length > 0);
  let totals = emptyTotals();
  const mealProt: Partial<Record<MealType, number>> = {};
  for (const r of recs) {
    for (const it of r.items) {
      totals = addTotals(totals, totalsFromAmounts(it.nutrients));
      if (r.mealType !== 'snack') {
        const p = it.nutrients.find((n) => n.code === 'PROT')?.amount ?? 0;
        mealProt[r.mealType] = (mealProt[r.mealType] ?? 0) + p;
      }
    }
  }
  return { date, hasRecords: recs.length > 0, totals, mainMealProtein: Object.values(mealProt) as number[] };
}

function averageOf(days: DayData[]): { avg: Totals; meals: MealStat; recordDays: number } {
  const rec = days.filter((d) => d.hasRecords);
  let sum = emptyTotals();
  for (const d of rec) sum = addTotals(sum, d.totals);
  const avg = emptyTotals();
  for (const c of NUTRIENT_CODES) {
    avg[c] = { amount: rec.length ? (sum[c]?.amount ?? 0) / rec.length : 0, inc: !!sum[c]?.inc };
  }
  return { avg, meals: { mainMealProtein: rec.flatMap((d) => d.mainMealProtein) }, recordDays: rec.length };
}

function requireProfile(): Profile {
  const p = getState().profile;
  if (!p) fail(404, 'NOT_FOUND', 'プロフィールが未登録です');
  return profileView(p, todayString());
}

function pfcOf(t: Totals): PfcView {
  const p = t['PROT']?.amount ?? 0;
  const f = t['FAT']?.amount ?? 0;
  const c = t['CHO']?.amount ?? 0;
  const kcal = [p * 4, f * 9, c * 4];
  const sum = kcal[0]! + kcal[1]! + kcal[2]!;
  let pct = [0, 0, 0];
  if (sum > 0) {
    const raw = kcal.map((k) => (k / sum) * 100);
    const floor = raw.map(Math.floor);
    let left = 100 - floor.reduce((a, b) => a + b, 0);
    const order = raw.map((v, i) => ({ i, r: v - Math.floor(v) })).sort((a, b) => b.r - a.r);
    for (const o of order) {
      if (left <= 0) break;
      floor[o.i] = (floor[o.i] ?? 0) + 1;
      left -= 1;
    }
    pct = floor;
  }
  return {
    proteinG: roundHalfUp(p, 1),
    fatG: roundHalfUp(f, 1),
    carbG: roundHalfUp(c, 1),
    ratioPct: { p: pct[0] ?? 0, f: pct[1] ?? 0, c: pct[2] ?? 0 },
  };
}

function nutrientViews(avg: Totals, profile: Profile, ulDays: Record<string, number> = {}): NutrientStatusView[] {
  return NUTRIENTS.map((n) => {
    const intake = roundHalfUp(avg[n.code]?.amount ?? 0, 4);
    const inc = !!avg[n.code]?.inc;
    let target: number | null = null;
    let kind: NutrientStatusView['target']['kind'] = 'RDA';
    if (n.code === 'ENERGY') {
      target = profile.targets.energyKcal.effective;
      kind = 'AI';
    } else if (n.code === 'PROT') target = profile.targets.proteinG;
    else if (n.code === 'NACL_EQ') {
      target = DUMMY_SALT_MAX_G;
      kind = 'DG_MAX';
    } else if (n.code in DUMMY_LOWER_TARGETS) target = DUMMY_LOWER_TARGETS[n.code] ?? null;
    if (target == null || n.isReference) {
      return {
        code: n.code, nameJa: n.nameJa, unit: n.unit, intake,
        target: { kind: null, amount: null, basis: 'PER_DAY' }, achievementRate: null,
        status: 'NO_TARGET' as const, isIncomplete: inc, isReference: n.isReference,
      };
    }
    const ratio = intake / target;
    const upper = kind === 'DG_MAX';
    const rate = upper ? roundHalfUp(ratio, 6) : roundHalfUp(Math.min(ratio, 1), 6);
    const status: NutrientStatusView['status'] = upper
      ? ratio > 1 ? 'OVER_UL' : 'ACHIEVED'
      : ratio >= 1 ? 'ACHIEVED' : 'DEFICIENT';
    const view: NutrientStatusView = {
      code: n.code, nameJa: n.nameJa, unit: n.unit, intake,
      target: { kind, amount: target, basis: 'PER_DAY' }, achievementRate: rate, status,
      isIncomplete: inc, isReference: false,
    };
    if (ulDays[n.code] !== undefined) view.ulExceededDays = ulDays[n.code];
    return view;
  });
}

function remainingMenus(): RemainingMenu[] {
  return getState()
    .menus.filter((m) => !m.deletedAt)
    .map((m) => ({ m, rem: remainingOf(m) }))
    .filter(({ rem }) => rem.ratio > 0.0000001)
    .map(({ m, rem }) => ({
      menuId: m.id, name: m.name, remainingRatio: rem.ratio, remainingServings: rem.servings, servingLabel: m.servingLabel,
    }));
}

function noticesOf(views: NutrientStatusView[]): DashboardNotice[] {
  const inc = views.filter((v) => v.isIncomplete && !v.isReference).map((v) => v.code);
  const out: DashboardNotice[] = [];
  if (inc.length) out.push({ code: 'INCOMPLETE_NUTRIENT', nutrientCodes: inc });
  out.push({ code: 'DUMMY_TARGETS', nutrientCodes: [] });
  return out;
}

function daily(date: string): DailyDashboard {
  const profile = requireProfile();
  const day = dayData(date);
  const scores = computeScores(day.totals, { mainMealProtein: day.mainMealProtein }, profile, day.hasRecords ? 1 : 0, 1);
  const views = nutrientViews(day.totals, profile);
  const energy = day.totals['ENERGY']?.amount ?? 0;
  return {
    date,
    hasRecords: day.hasRecords,
    energy: {
      intakeKcal: Math.round(energy),
      targetKcal: profile.targets.energyKcal.effective,
      achievementRate: roundHalfUp(energy / profile.targets.energyKcal.effective, 6),
    },
    pfc: pfcOf(day.totals),
    scores,
    nutrients: views,
    remainingMenus: remainingMenus(),
    notices: noticesOf(views),
  };
}

function weekly(endDate: string): WeeklyDashboard {
  const profile = requireProfile();
  const dates = Array.from({ length: 7 }, (_, i) => addDays(endDate, i - 6));
  const days = dates.map(dayData);
  const { avg, meals, recordDays } = averageOf(days);
  const insufficient = recordDays < 3;
  const scores = computeScores(avg, meals, profile, recordDays, 3);
  const ulDays: Record<string, number> = {
    NACL_EQ: days.filter((d) => d.hasRecords && (d.totals['NACL_EQ']?.amount ?? 0) > DUMMY_SALT_MAX_G).length,
  };
  const views = nutrientViews(avg, profile, ulDays);
  const series: SeriesPoint[] = days.map((d) => {
    if (!d.hasRecords) return { date: d.date, hasRecords: false, intakeKcal: null, scores: null };
    const s = computeScores(d.totals, { mainMealProtein: d.mainMealProtein }, profile, 1, 1);
    return {
      date: d.date, hasRecords: true, intakeKcal: Math.round(d.totals['ENERGY']?.amount ?? 0),
      scores: { beauty: s.beauty.value, muscle: s.muscle.value, base: s.base.value },
    };
  });
  return {
    endDate,
    windowStart: dates[0] ?? endDate,
    recordDays,
    insufficientData: insufficient,
    averages: recordDays
      ? {
          energy: { intakeKcal: Math.round(avg['ENERGY']?.amount ?? 0), targetKcal: profile.targets.energyKcal.effective },
          pfc: pfcOf(avg),
          nutrients: views,
        }
      : { energy: null, pfc: null, nutrients: [] },
    scores,
    series,
    remainingMenus: remainingMenus(),
    notices: noticesOf(views),
  };
}

function contributions(code: string, from: string, to: string): Contributions {
  if (!NUTRIENTS.some((n) => n.code === code)) fail(404, 'NOT_FOUND', '栄養素が見つかりません');
  if (!from || !to || from > to) validation([{ field: 'from', reason: 'from は to 以前の日付を指定してください' }]);
  const map = new Map<string, ContributionItem>();
  let total = 0;
  let inc = false;
  for (const r of getState().records) {
    if (r.eatenOn < from || r.eatenOn > to) continue;
    for (const it of r.items) {
      const n = it.nutrients.find((x) => x.code === code);
      if (!n) continue;
      total += n.amount;
      inc = inc || n.isIncomplete;
      const key = it.menuId != null ? `M${it.menuId}` : `F${it.foodId}`;
      let item = map.get(key);
      if (!item) {
        item = {
          type: it.menuId != null ? 'MENU' : 'FOOD',
          id: (it.menuId ?? it.foodId) as number,
          name: it.menuId != null ? (getState().menus.find((m) => m.id === it.menuId)?.name ?? '') : (foodDef(it.foodId as number)?.name ?? ''),
          amount: 0, sharePct: 0, children: [],
        };
        map.set(key, item);
      }
      item.amount += n.amount;
      if (it.menuId != null && it.portionRatio != null) {
        const menu = getState().menus.find((m) => m.id === it.menuId);
        for (const mi of menu?.items ?? []) {
          const amt = (foodNutrition(mi.foodId, mi.amountG)[code]?.amount ?? 0) * it.portionRatio;
          const child = item.children.find((c) => c.foodId === mi.foodId);
          if (child) child.amount += amt;
          else item.children.push({ foodId: mi.foodId, name: foodDef(mi.foodId)?.name ?? '', amount: amt });
        }
      }
    }
  }
  const items = [...map.values()]
    .filter((i) => i.amount > 0)
    .map((i) => ({
      ...i,
      amount: roundHalfUp(i.amount, 4),
      sharePct: total > 0 ? roundHalfUp((i.amount / total) * 100, 1) : 0,
      children: i.children.map((c) => ({ ...c, amount: roundHalfUp(c.amount, 4) })).sort((a, b) => b.amount - a.amount),
    }))
    .sort((a, b) => b.amount - a.amount);
  return { nutrientCode: code, from, to, totalAmount: roundHalfUp(total, 4), isIncomplete: inc, items };
}

// ---------- ルーティング ----------
export interface MockRequest {
  method: string;
  path: string;
  query: URLSearchParams;
  body: unknown;
}

export function handle(req: MockRequest): MockResponse {
  const { method, path, query } = req;
  const state = getState();
  let m: RegExpMatchArray | null;

  // 食材
  if (method === 'GET' && path === '/api/foods') {
    const q = (query.get('q') ?? '').trim();
    if (!q) validation([{ field: 'q', reason: '検索語を入力してください' }]);
    if (q.length > 50) validation([{ field: 'q', reason: '50文字以内で入力してください' }]);
    const limit = Math.min(Number(query.get('limit') ?? 20), 50);
    const offset = Number(query.get('offset') ?? 0);
    const all = searchFoods(q);
    const res: FoodSearchResult = { items: all.slice(offset, offset + limit).map(foodSummary), total: all.length };
    return ok(res);
  }
  if (method === 'GET' && (m = path.match(/^\/api\/foods\/(\d+)$/))) {
    const def = foodDef(Number(m[1]));
    if (!def) fail(404, 'NOT_FOUND', '対象が見つかりません');
    return ok(foodDetail(def));
  }
  if (method === 'GET' && path === '/api/nutrients') return ok(NUTRIENTS);
  if (method === 'GET' && (m = path.match(/^\/api\/nutrients\/([A-Z0-9_]+)\/contributions$/)))
    return ok(contributions(m[1] as string, query.get('from') ?? '', query.get('to') ?? ''));

  // プロフィール
  if (method === 'GET' && path === '/api/profile') return ok(requireProfile());
  if (method === 'PUT' && path === '/api/profile') {
    const b = req.body as ProfileInput;
    const d: ApiError['details'] = [];
    if (b.sex !== 'male' && b.sex !== 'female') d.push({ field: 'sex', reason: '性別を選択してください' });
    if (!b.birthDate || parseDate(b.birthDate) >= parseDate(todayString())) d.push({ field: 'birthDate', reason: '過去の日付を指定してください' });
    if (!(b.heightCm >= 50 && b.heightCm <= 250)) d.push({ field: 'heightCm', reason: '50.0〜250.0 の範囲で入力してください' });
    if (!(b.weightKg >= 20 && b.weightKg <= 300)) d.push({ field: 'weightKg', reason: '20.0〜300.0 の範囲で入力してください' });
    if (![1, 2, 3, 4].includes(b.activityLevel)) d.push({ field: 'activityLevel', reason: '1〜4 を選択してください' });
    if (!['skin', 'muscle', 'both'].includes(b.goal)) d.push({ field: 'goal', reason: '目的を選択してください' });
    if (b.energyTargetKcal != null && !(Number.isInteger(b.energyTargetKcal) && b.energyTargetKcal >= 500 && b.energyTargetKcal <= 10000))
      d.push({ field: 'energyTargetKcal', reason: '500〜10000 の整数で入力してください' });
    if (d.length) validation(d);
    state.profile = { ...b };
    persist();
    return ok(profileView(state.profile, todayString()));
  }

  // メニュー
  if (method === 'GET' && path === '/api/menus')
    return ok(state.menus.filter((x) => !x.deletedAt).sort((a, b) => b.id - a.id).map(menuSummary));
  if (method === 'POST' && path === '/api/menus/preview') return ok(preview(req.body as Partial<MenuInput>));
  if (method === 'POST' && path === '/api/menus') {
    const b = req.body as MenuInput;
    validateMenu(b);
    const menu: StoredMenu = {
      id: nextId('menu'), name: '', cookedOn: null, memo: null, cookedTotalWeightG: 1, servings: 1, servingLabel: 'お椀', items: [], deletedAt: null,
    };
    applyMenu(menu, b);
    state.menus.push(menu);
    persist();
    return ok(menuDetail(menu), 201);
  }
  if ((m = path.match(/^\/api\/menus\/(\d+)\/duplicate$/)) && method === 'POST') {
    const src = activeMenu(Number(m[1]));
    const b = (req.body ?? {}) as { name?: string; cookedOn?: string };
    const copy: StoredMenu = {
      ...src, id: nextId('menu'), name: b.name || `${src.name}のコピー`, cookedOn: b.cookedOn || todayString(), deletedAt: null,
      items: src.items.map((it, i) => ({ ...it, id: nextId('menuItem'), sortOrder: i })),
    };
    state.menus.push(copy);
    persist();
    return ok(menuDetail(copy), 201);
  }
  if ((m = path.match(/^\/api\/menus\/(\d+)$/))) {
    const id = Number(m[1]);
    if (method === 'GET') return ok(menuDetail(activeMenu(id)));
    if (method === 'PUT') {
      const menu = activeMenu(id);
      const b = req.body as MenuInput;
      validateMenu(b);
      applyMenu(menu, b);
      persist();
      return ok(menuDetail(menu));
    }
    if (method === 'DELETE') {
      const menu = activeMenu(id);
      menu.deletedAt = new Date().toISOString();
      persist();
      return noContent();
    }
  }

  // 食事記録
  if (method === 'GET' && path === '/api/meal-records') {
    const date = query.get('date');
    if (!date) validation([{ field: 'date', reason: '日付を指定してください' }]);
    const records = state.records
      .filter((r) => r.eatenOn === date)
      .sort((a, b) => MEAL_ORDER.indexOf(a.mealType) - MEAL_ORDER.indexOf(b.mealType) || a.id - b.id)
      .map(recordView);
    const res: MealRecordsOfDay = { date, records };
    return ok(res);
  }
  if (method === 'POST' && path === '/api/meal-records/copy') {
    const b = req.body as { fromDate: string; toDate: string; allowExisting?: boolean };
    if (!b.fromDate || !b.toDate || b.fromDate === b.toDate) validation([{ field: 'toDate', reason: 'コピー元と異なる日付を指定してください' }]);
    const src = state.records.filter((r) => r.eatenOn === b.fromDate);
    if (!src.length) validation([{ field: 'fromDate', reason: 'コピー元に記録がありません' }]);
    if (state.records.some((r) => r.eatenOn === b.toDate) && !b.allowExisting)
      fail(409, 'CONFLICT', 'コピー先に既に記録があります', [{ field: 'toDate', reason: 'TARGET_DATE_HAS_RECORDS' }]);
    const records: MealRecordView[] = [];
    const skipped: CopyResult['skipped'] = [];
    for (const r of src) {
      const usable = r.items.filter((it) => {
        if (it.menuId == null) return true;
        const ok2 = !!state.menus.find((x) => x.id === it.menuId && !x.deletedAt);
        if (!ok2) skipped.push({ menuId: it.menuId, reason: 'MENU_DELETED' });
        return ok2;
      });
      if (!usable.length) continue;
      const input: MealRecordInput = {
        eatenOn: b.toDate, mealType: r.mealType, memo: r.memo,
        items: usable.map((it) => ({
          ...(it.menuId != null ? { menuId: it.menuId } : { foodId: it.foodId as number, unitId: it.unitId }),
          inputMode: it.inputMode, inputValue: it.inputValue,
        })),
      };
      const rec: StoredRecord = { id: nextId('record'), eatenOn: b.toDate, mealType: r.mealType, memo: r.memo, items: buildRecordItems(input) };
      state.records.push(rec);
      records.push(recordView(rec));
    }
    persist();
    const res: CopyResult = { records, skipped };
    return ok(res, 201);
  }
  if (method === 'POST' && path === '/api/meal-records') {
    const b = req.body as MealRecordInput;
    const items = buildRecordItems(b);
    const rec: StoredRecord = { id: nextId('record'), eatenOn: b.eatenOn, mealType: b.mealType, memo: b.memo || null, items };
    state.records.push(rec);
    persist();
    const res: MealRecordSaved = { ...recordView(rec), warnings: warningsFor(items) };
    return ok(res, 201);
  }
  if ((m = path.match(/^\/api\/meal-records\/(\d+)$/))) {
    const id = Number(m[1]);
    const rec = state.records.find((r) => r.id === id);
    if (!rec) fail(404, 'NOT_FOUND', '対象が見つかりません');
    if (method === 'PUT') {
      const b = req.body as MealRecordInput;
      const existingMenus = rec.items.map((i) => i.menuId).filter((x): x is number => x != null);
      const items = buildRecordItems(b, existingMenus);
      rec.eatenOn = b.eatenOn;
      rec.mealType = b.mealType;
      rec.memo = b.memo || null;
      rec.items = items;
      persist();
      const res: MealRecordSaved = { ...recordView(rec), warnings: warningsFor(items) };
      return ok(res);
    }
    if (method === 'DELETE') {
      state.records = state.records.filter((r) => r.id !== id);
      persist();
      return noContent();
    }
  }
  if (method === 'GET' && path === '/api/recent-items') {
    const limit = Math.min(Number(query.get('limit') ?? 10), 30);
    const res: RecentItems = { menus: [], foods: [] };
    const recs = [...state.records].sort((a, b) => (a.eatenOn === b.eatenOn ? b.id - a.id : a.eatenOn < b.eatenOn ? 1 : -1));
    for (const r of recs) {
      for (const it of r.items) {
        if (it.menuId != null) {
          const menu = state.menus.find((x) => x.id === it.menuId && !x.deletedAt);
          if (menu && !res.menus.some((x) => x.menuId === menu.id) && res.menus.length < limit)
            res.menus.push({ menuId: menu.id, name: menu.name, lastInputMode: it.inputMode, lastInputValue: it.inputValue, lastUsedOn: r.eatenOn });
        } else if (it.foodId != null) {
          const def = foodDef(it.foodId);
          if (def && !res.foods.some((x) => x.foodId === def.id) && res.foods.length < limit)
            res.foods.push({ foodId: def.id, name: def.name, lastInputMode: it.inputMode, lastInputValue: it.inputValue, unitId: it.unitId, lastUsedOn: r.eatenOn });
        }
      }
    }
    return ok(res);
  }

  // ダッシュボード
  if (method === 'GET' && path === '/api/dashboard/daily') {
    const date = query.get('date');
    if (!date) validation([{ field: 'date', reason: '日付を指定してください' }]);
    return ok(daily(date));
  }
  if (method === 'GET' && path === '/api/dashboard/weekly') {
    const end = query.get('endDate');
    if (!end) validation([{ field: 'endDate', reason: '日付を指定してください' }]);
    if (end > todayString()) validation([{ field: 'endDate', reason: '未来の日付は指定できません' }]);
    return ok(weekly(end));
  }

  // プロトタイプ専用（モックの初期化）
  if (method === 'POST' && path === '/api/__mock__/reset') {
    const b = (req.body ?? {}) as { withProfile?: boolean };
    resetState(b.withProfile !== false);
    return noContent();
  }

  fail(404, 'NOT_FOUND', '対象が見つかりません');
}
