// API の型。docs/09.API仕様.md と 1 対 1 で対応させる。`any` は使わない。

export type Sex = 'male' | 'female';
export type Goal = 'skin' | 'muscle' | 'both';
export type MealType = 'breakfast' | 'lunch' | 'dinner' | 'snack';
export type MenuInputMode = 'RATIO' | 'SERVING' | 'GRAM';
export type FoodInputMode = 'GRAM' | 'UNIT';
export type InputMode = MenuInputMode | FoodInputMode;
export type NutrientStatus = 'DEFICIENT' | 'ACHIEVED' | 'OVER_UL' | 'NO_TARGET';
export type ScoreBand = 'GOOD' | 'FAIR' | 'LOW';
export type TargetKind = 'RDA' | 'AI' | 'DG_MIN' | 'DG_MAX' | 'UL';

export interface ApiErrorDetail {
  field: string;
  reason: string;
}

export interface ApiError {
  code: 'VALIDATION_ERROR' | 'NOT_FOUND' | 'CONFLICT' | 'INTERNAL_ERROR';
  message: string;
  details: ApiErrorDetail[];
}

// ---- 栄養素 ----
export interface NutrientDef {
  code: string;
  nameJa: string;
  unit: string;
  category: 'macro' | 'vitamin' | 'mineral' | 'antioxidant';
  isReference: boolean;
  displayOrder: number;
}

export interface NutrientAmount {
  code: string;
  amount: number;
  isIncomplete: boolean;
}

// ---- 食材 ----
export interface FoodUnit {
  id: number;
  unitName: string;
  gramsPerUnit: number;
  isDefault: boolean;
}

export interface FoodSummary {
  id: number;
  foodCode: string;
  name: string;
  nameKana: string;
  foodGroup: string | null;
  refuseRatePct: number;
  units: FoodUnit[];
}

export interface FoodNutrientDetail {
  code: string;
  per100g: number | null;
  isTrace: boolean;
  sourceNote: string | null;
}

export interface FoodDetail extends FoodSummary {
  officialName: string | null;
  sourceVersion: string;
  isActive: boolean;
  aliases: string[];
  nutrients: FoodNutrientDetail[];
}

export interface FoodSearchResult {
  items: FoodSummary[];
  total: number;
}

// ---- プロフィール ----
export interface ProfileInput {
  sex: Sex;
  birthDate: string;
  heightCm: number;
  weightKg: number;
  activityLevel: number;
  goal: Goal;
  energyTargetKcal: number | null;
}

export interface Profile extends ProfileInput {
  targets: {
    energyKcal: { auto: number; override: number | null; effective: number };
    proteinG: number;
  };
}

// ---- メニュー ----
export interface MenuItemInput {
  foodId: number;
  unitId: number | null;
  quantity: number;
}

export interface MenuInput {
  name: string;
  cookedOn: string | null;
  memo: string | null;
  cookedTotalWeightG: number;
  servings: number;
  servingLabel: string;
  items: MenuItemInput[];
}

export interface Remaining {
  ratio: number;
  servings: number;
  isOver: boolean;
}

export interface MenuItemView {
  id: number;
  foodId: number;
  foodName: string;
  unitId: number | null;
  unitName: string | null;
  quantity: number;
  amountG: number;
  sortOrder: number;
}

export interface MenuSummary {
  id: number;
  name: string;
  cookedOn: string | null;
  cookedTotalWeightG: number;
  servings: number;
  servingLabel: string;
  servingWeightG: number;
  remaining: Remaining;
}

export interface MenuDetail extends MenuSummary {
  memo: string | null;
  items: MenuItemView[];
  nutrients: NutrientAmount[];
  perServing: NutrientAmount[];
}

export interface MenuPreviewItem {
  foodId: number;
  foodName: string;
  amountG: number;
  edibleG: number;
  nutrients: NutrientAmount[];
}

export interface MenuPreview {
  items: MenuPreviewItem[];
  nutrients: NutrientAmount[];
  servingWeightG: number | null;
  perServing: NutrientAmount[] | null;
  warnings: { code: 'FOOD_INACTIVE'; foodId: number; message: string }[];
}

// ---- 食事記録 ----
export interface MealRecordItemInput {
  menuId?: number;
  foodId?: number;
  unitId?: number | null;
  inputMode: InputMode;
  inputValue: number;
}

export interface MealRecordInput {
  eatenOn: string;
  mealType: MealType;
  memo: string | null;
  items: MealRecordItemInput[];
}

export interface MealRecordItemView {
  id: number;
  menuId: number | null;
  menuName: string | null;
  foodId: number | null;
  foodName: string | null;
  unitId: number | null;
  inputMode: InputMode;
  inputValue: number;
  portionRatio: number | null;
  amountG: number | null;
  nutrients: NutrientAmount[];
}

export interface MealRecordView {
  id: number;
  eatenOn: string;
  mealType: MealType;
  memo: string | null;
  items: MealRecordItemView[];
}

export interface MealRecordsOfDay {
  date: string;
  records: MealRecordView[];
}

export interface RecordWarning {
  code: 'MENU_OVER_100PCT';
  menuId: number;
  totalPortionRatio: number;
  message: string;
}

export interface MealRecordSaved extends MealRecordView {
  warnings: RecordWarning[];
}

export interface RecentItems {
  menus: {
    menuId: number;
    name: string;
    lastInputMode: InputMode;
    lastInputValue: number;
    lastUsedOn: string;
  }[];
  foods: {
    foodId: number;
    name: string;
    lastInputMode: InputMode;
    lastInputValue: number;
    unitId: number | null;
    lastUsedOn: string;
  }[];
}

export interface CopyResult {
  records: MealRecordView[];
  skipped: { menuId: number | null; foodId: number | null; reason: 'MENU_DELETED' | 'FOOD_INACTIVE' }[];
}

// ---- ダッシュボード ----
export interface ScoreView {
  value: number | null;
  band: ScoreBand | null;
  insufficientData: boolean;
  hasIncomplete: boolean;
}

export interface Scores {
  beauty: ScoreView;
  muscle: ScoreView;
  base: ScoreView;
}

/**
 * 栄養素の目標（1 日あたり）。kind / amount は下限型の目標（エネルギー・タンパク質は CALCULATED）。
 * min / max / ul は目標量の下限・上限・耐容上限量。超えると警告する上限は ul（なければ max）。
 */
export interface NutrientTargetView {
  kind: TargetKind | 'CALCULATED' | null;
  amount: number | null;
  min: number | null;
  max: number | null;
  ul: number | null;
  basis: 'PER_DAY';
}

export interface NutrientStatusView {
  code: string;
  nameJa: string;
  unit: string;
  intake: number;
  target: NutrientTargetView;
  achievementRate: number | null;
  status: NutrientStatus;
  isIncomplete: boolean;
  isReference: boolean;
  ulExceededDays?: number;
}

export interface PfcView {
  proteinG: number;
  fatG: number;
  carbG: number;
  ratioPct: { p: number; f: number; c: number };
}

export interface RemainingMenu {
  menuId: number;
  name: string;
  remainingRatio: number;
  remainingServings: number;
  servingLabel: string;
}

export interface DashboardNotice {
  code: 'INCOMPLETE_NUTRIENT' | 'DUMMY_TARGETS';
  nutrientCodes: string[];
}

export interface DailyDashboard {
  date: string;
  hasRecords: boolean;
  energy: { intakeKcal: number; targetKcal: number; achievementRate: number };
  pfc: PfcView;
  scores: Scores;
  nutrients: NutrientStatusView[];
  remainingMenus: RemainingMenu[];
  notices: DashboardNotice[];
}

export interface SeriesPoint {
  date: string;
  hasRecords: boolean;
  intakeKcal: number | null;
  scores: { beauty: number | null; muscle: number | null; base: number | null } | null;
}

export interface WeeklyDashboard {
  endDate: string;
  windowStart: string;
  recordDays: number;
  insufficientData: boolean;
  averages: {
    energy: { intakeKcal: number; targetKcal: number } | null;
    pfc: PfcView | null;
    nutrients: NutrientStatusView[];
  };
  scores: Scores;
  series: SeriesPoint[];
  remainingMenus: RemainingMenu[];
  notices: DashboardNotice[];
}

export interface ContributionChild {
  foodId: number;
  name: string;
  amount: number;
}

export interface ContributionItem {
  type: 'MENU' | 'FOOD';
  id: number;
  name: string;
  amount: number;
  sharePct: number;
  children: ContributionChild[];
}

export interface Contributions {
  nutrientCode: string;
  from: string;
  to: string;
  totalAmount: number;
  isIncomplete: boolean;
  items: ContributionItem[];
}
