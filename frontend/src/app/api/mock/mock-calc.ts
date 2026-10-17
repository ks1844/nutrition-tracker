// 【使い捨て】Mock API 内の簡易計算。確定した計算の正本はバックエンドの Domain（docs/03.栄養計算仕様.md）。
// ここは画面の見た目確認用で、decimal ではなく number を使う。結線時にサーバー計算へ置き換える。

import { FoodDetail, FoodSummary, NutrientAmount, Profile, ProfileInput, ScoreBand, ScoreView, Scores } from '../../types/api';
import {
  ACTIVITY_FACTOR,
  BASE_NUTRIENTS,
  BEAUTY_WEIGHTS,
  DUMMY_LOWER_TARGETS,
  DUMMY_SALT_MAX_G,
  FOODS,
  MockFoodDef,
  NUTRIENTS,
  NUTRIENT_CODES,
  PROTEIN_PER_KG,
  foodPer100g,
} from './mock-data';
import { ageOn } from '../../services/date-util';

export function roundHalfUp(x: number, digits: number): number {
  const f = 10 ** digits;
  // 2 進浮動小数のノイズ（17.12625 → 17.126249999…）を取り除いてから四捨五入する。
  // 本番の計算は decimal で行う（NFR-001）。これは Mock 用の簡易処理。
  return Math.round(Number((x * f).toPrecision(15))) / f;
}

export type Totals = Record<string, { amount: number; inc: boolean }>;

export function emptyTotals(): Totals {
  const t: Totals = {};
  for (const c of NUTRIENT_CODES) t[c] = { amount: 0, inc: false };
  return t;
}

export function unitIdOf(foodId: number, index: number): number {
  return foodId * 100 + index + 1;
}

export function foodDef(foodId: number): MockFoodDef | undefined {
  return FOODS.find((f) => f.id === foodId);
}

export function findUnit(foodId: number, unitId: number): { unitName: string; gramsPerUnit: number } | undefined {
  const def = foodDef(foodId);
  if (!def) return undefined;
  const idx = def.units.findIndex((_, i) => unitIdOf(foodId, i) === unitId);
  const u = idx >= 0 ? def.units[idx] : undefined;
  return u ? { unitName: u.unitName, gramsPerUnit: u.gramsPerUnit } : undefined;
}

/** 数量→重量g（廃棄前）。単位が無ければ g 入力。 */
export function toAmountG(foodId: number, unitId: number | null, quantity: number): number {
  if (unitId == null) return roundHalfUp(quantity, 2);
  const u = findUnit(foodId, unitId);
  return roundHalfUp(quantity * (u?.gramsPerUnit ?? 1), 2);
}

/** 食材の栄養量（丸めない）。未測定は 0 + inc。 */
export function foodNutrition(foodId: number, amountG: number): Totals {
  const def = foodDef(foodId);
  const out = emptyTotals();
  if (!def) return out;
  const per = foodPer100g(def);
  const edible = amountG * (1 - def.refuseRatePct / 100);
  for (const code of NUTRIENT_CODES) {
    const v = per[code];
    if (v === undefined) continue; // 参考値など行なし
    if (v === null) out[code] = { amount: 0, inc: true };
    else out[code] = { amount: (edible / 100) * v, inc: false };
  }
  return out;
}

export function addTotals(a: Totals, b: Totals): Totals {
  const out = emptyTotals();
  for (const c of NUTRIENT_CODES) {
    out[c] = { amount: (a[c]?.amount ?? 0) + (b[c]?.amount ?? 0), inc: !!(a[c]?.inc || b[c]?.inc) };
  }
  return out;
}

export function scaleTotals(t: Totals, ratio: number, digits = 4): NutrientAmount[] {
  return NUTRIENT_CODES.map((code) => ({
    code,
    amount: roundHalfUp((t[code]?.amount ?? 0) * ratio, digits),
    isIncomplete: !!t[code]?.inc,
  }));
}

export function totalsFromAmounts(list: NutrientAmount[]): Totals {
  const t = emptyTotals();
  for (const n of list) t[n.code] = { amount: n.amount, inc: n.isIncomplete };
  return t;
}

export function toAmounts(t: Totals, digits = 4): NutrientAmount[] {
  return NUTRIENT_CODES.map((code) => ({
    code,
    amount: roundHalfUp(t[code]?.amount ?? 0, digits),
    isIncomplete: !!t[code]?.inc,
  }));
}

// ---- 食材ビュー ----
export function foodSummary(def: MockFoodDef): FoodSummary {
  return {
    id: def.id,
    foodCode: def.foodCode,
    name: def.name,
    nameKana: def.nameKana,
    foodGroup: def.group,
    refuseRatePct: def.refuseRatePct,
    units: def.units.map((u, i) => ({
      id: unitIdOf(def.id, i),
      unitName: u.unitName,
      gramsPerUnit: u.gramsPerUnit,
      isDefault: !!u.isDefault,
    })),
  };
}

export function foodDetail(def: MockFoodDef): FoodDetail {
  const per = foodPer100g(def);
  return {
    ...foodSummary(def),
    officialName: null,
    sourceVersion: 'ダミー（実際の成分表の値ではありません）',
    isActive: true,
    aliases: def.aliases,
    nutrients: NUTRIENTS.filter((n) => per[n.code] !== undefined).map((n) => ({
      code: n.code,
      per100g: per[n.code] ?? null,
      isTrace: !!def.trace?.includes(n.code),
      sourceNote: n.code === 'ASTX' ? '参考値（成分表外・ダミー）' : null,
    })),
  };
}

// ---- 検索の正規化 ----
export function kataToHira(s: string): string {
  return s.replace(/[ァ-ヶ]/g, (c) => String.fromCharCode(c.charCodeAt(0) - 0x60));
}
export function hiraToKata(s: string): string {
  return s.replace(/[ぁ-ゖ]/g, (c) => String.fromCharCode(c.charCodeAt(0) + 0x60));
}
export function searchFoods(q: string): MockFoodDef[] {
  const raw = q.trim().normalize('NFKC').toLowerCase();
  if (!raw) return [];
  const cands = [raw, kataToHira(raw), hiraToKata(raw)];
  const scored: { def: MockFoodDef; score: number }[] = [];
  for (const def of FOODS) {
    const names = [def.name, def.nameKana, ...def.aliases].map((s) => s.normalize('NFKC').toLowerCase());
    const hay = names.flatMap((n) => [n, kataToHira(n)]);
    let score = -1;
    for (const c of cands) {
      for (const h of hay) {
        const i = h.indexOf(c);
        if (i === 0) score = Math.max(score, 2);
        else if (i > 0) score = Math.max(score, 1);
      }
    }
    if (score >= 0) scored.push({ def, score });
  }
  return scored.sort((a, b) => b.score - a.score || a.def.id - b.def.id).map((s) => s.def);
}

// ---- 目標 ----
export function energyAuto(p: ProfileInput, on: string): number {
  const age = ageOn(p.birthDate, on);
  const bmr = 10 * p.weightKg + 6.25 * p.heightCm - 5 * age + (p.sex === 'male' ? 5 : -161);
  return Math.round(bmr * (ACTIVITY_FACTOR[p.activityLevel] ?? 1.2));
}

export function profileView(p: ProfileInput, on: string): Profile {
  const auto = energyAuto(p, on);
  return {
    ...p,
    targets: {
      energyKcal: { auto, override: p.energyTargetKcal, effective: p.energyTargetKcal ?? auto },
      proteinG: roundHalfUp(p.weightKg * (PROTEIN_PER_KG[p.goal] ?? 1), 1),
    },
  };
}

// ---- スコア（docs/06.スコアリング仕様.md の式を簡易実装。目標はダミー） ----
export function bandOf(v: number): ScoreBand {
  return v >= 80 ? 'GOOD' : v >= 60 ? 'FAIR' : 'LOW';
}

const clamp01 = (x: number): number => Math.min(Math.max(x, 0), 1);

export interface MealStat {
  /** 朝昼夕の食事（日付+区分で合算）ごとのタンパク質 g */
  mainMealProtein: number[];
}

export function computeScores(
  avg: Totals,
  meals: MealStat,
  profile: Profile,
  recordDays: number,
  minDays: number,
): Scores {
  const insufficient = recordDays < minDays;
  const none: ScoreView = { value: null, band: null, insufficientData: insufficient, hasIncomplete: false };
  if (insufficient || recordDays === 0) return { beauty: none, muscle: none, base: none };

  const rate = (code: string, target: number): number => clamp01((avg[code]?.amount ?? 0) / target);

  // 美肌
  let wsum = 0;
  let acc = 0;
  let inc = false;
  for (const [code, w] of Object.entries(BEAUTY_WEIGHTS)) {
    const t = DUMMY_LOWER_TARGETS[code];
    if (!t) continue;
    acc += w * rate(code, t);
    wsum += w;
    inc = inc || !!avg[code]?.inc;
  }
  const beauty = Math.round((acc / wsum) * 100);

  // 筋肉
  const proteinRate = clamp01((avg['PROT']?.amount ?? 0) / profile.targets.proteinG);
  const dist = meals.mainMealProtein.length
    ? meals.mainMealProtein.filter((p) => p >= 20).length / meals.mainMealProtein.length
    : null;
  const ratio = (avg['ENERGY']?.amount ?? 0) / profile.targets.energyKcal.effective;
  const d = Math.abs(ratio - 1);
  const energyFit = d <= 0.1 ? 1 : Math.max(0, 1 - (d - 0.1) / 0.4);
  const wP = 0.6;
  const wD = dist === null ? 0 : 0.2;
  const wE = 0.2;
  const muscleRaw = (wP * proteinRate + wD * (dist ?? 0) + wE * energyFit) / (wP + wD + wE);
  const muscle = Math.round(muscleRaw * 100);

  // 基礎
  let baseAcc = 0;
  let baseInc = false;
  for (const code of BASE_NUTRIENTS) {
    baseAcc += rate(code, DUMMY_LOWER_TARGETS[code] ?? 1);
    baseInc = baseInc || !!avg[code]?.inc;
  }
  const salt = avg['NACL_EQ']?.amount ?? 0;
  const penalty = salt > DUMMY_SALT_MAX_G ? Math.min(10, 20 * (salt / DUMMY_SALT_MAX_G - 1)) : 0;
  const base = Math.max(0, Math.round((baseAcc / BASE_NUTRIENTS.length) * 100 - penalty));

  const mk = (value: number, hasIncomplete: boolean): ScoreView => ({
    value,
    band: bandOf(value),
    insufficientData: false,
    hasIncomplete,
  });
  return {
    beauty: mk(beauty, inc),
    muscle: mk(muscle, !!avg['PROT']?.inc || !!avg['ENERGY']?.inc),
    base: mk(base, baseInc),
  };
}
