// 【使い捨て】Mock API 用のダミーデータ（docs/13.開発手順.md フェーズ 1）。
// ここにある食材・栄養値・目標値は画面確認用に作ったダミーで、実際の成分表・食事摂取基準の値ではない。
// 例外として、にんじん／豚こま肉／みそは docs/03.栄養計算仕様.md §9 の正解データ（テスト用ダミー）と同じ値にしてある。

import { NutrientDef } from '../../types/api';

export const NUTRIENTS: NutrientDef[] = [
  { code: 'ENERGY', nameJa: 'エネルギー', unit: 'kcal', category: 'macro' },
  { code: 'PROT', nameJa: 'タンパク質', unit: 'g', category: 'macro' },
  { code: 'FAT', nameJa: '脂質', unit: 'g', category: 'macro' },
  { code: 'CHO', nameJa: '炭水化物', unit: 'g', category: 'macro' },
  { code: 'FIBTG', nameJa: '食物繊維総量', unit: 'g', category: 'macro' },
  { code: 'NACL_EQ', nameJa: '食塩相当量', unit: 'g', category: 'mineral' },
  { code: 'K', nameJa: 'カリウム', unit: 'mg', category: 'mineral' },
  { code: 'CA', nameJa: 'カルシウム', unit: 'mg', category: 'mineral' },
  { code: 'MG', nameJa: 'マグネシウム', unit: 'mg', category: 'mineral' },
  { code: 'FE', nameJa: '鉄', unit: 'mg', category: 'mineral' },
  { code: 'ZN', nameJa: '亜鉛', unit: 'mg', category: 'mineral' },
  { code: 'VITA_RAE', nameJa: 'ビタミンA', unit: 'μg', category: 'vitamin' },
  { code: 'CARTB_EQ', nameJa: 'β-カロテン当量', unit: 'μg', category: 'vitamin' },
  { code: 'VITD', nameJa: 'ビタミンD', unit: 'μg', category: 'vitamin' },
  { code: 'VITE_ATOC', nameJa: 'ビタミンE', unit: 'mg', category: 'vitamin' },
  { code: 'VITK', nameJa: 'ビタミンK', unit: 'μg', category: 'vitamin' },
  { code: 'VITB1', nameJa: 'ビタミンB1', unit: 'mg', category: 'vitamin' },
  { code: 'VITB2', nameJa: 'ビタミンB2', unit: 'mg', category: 'vitamin' },
  { code: 'NIA', nameJa: 'ナイアシン', unit: 'mg', category: 'vitamin' },
  { code: 'VITB6', nameJa: 'ビタミンB6', unit: 'mg', category: 'vitamin' },
  { code: 'VITB12', nameJa: 'ビタミンB12', unit: 'μg', category: 'vitamin' },
  { code: 'FOL', nameJa: '葉酸', unit: 'μg', category: 'vitamin' },
  { code: 'VITC', nameJa: 'ビタミンC', unit: 'mg', category: 'vitamin' },
  { code: 'ASTX', nameJa: 'アスタキサンチン', unit: 'mg', category: 'antioxidant' },
].map((n, i) => ({
  ...n,
  isReference: n.code === 'ASTX',
  displayOrder: i + 1,
})) as NutrientDef[];

export const NUTRIENT_CODES = NUTRIENTS.map((n) => n.code);

type Partial100 = Record<string, number | null>;

// 100g あたりのダミー値（栄養素コード→値。未指定は 0、null は未測定）。
const ARCH: Record<string, Partial100> = {
  vegGreen: {
    ENERGY: 30, PROT: 1.5, FAT: 0.2, CHO: 6, FIBTG: 2.5, NACL_EQ: 0.01, K: 300, CA: 40, MG: 18, FE: 0.6, ZN: 0.3,
    VITA_RAE: 250, CARTB_EQ: 1500, VITE_ATOC: 1.2, VITK: 120, VITB1: 0.06, VITB2: 0.1, NIA: 0.8, VITB6: 0.12, FOL: 90, VITC: 30,
  },
  vegLight: {
    ENERGY: 25, PROT: 1, FAT: 0.1, CHO: 5.5, FIBTG: 1.5, NACL_EQ: 0.01, K: 200, CA: 20, MG: 10, FE: 0.3, ZN: 0.2,
    VITA_RAE: 5, CARTB_EQ: 50, VITE_ATOC: 0.3, VITK: 10, VITB1: 0.04, VITB2: 0.03, NIA: 0.3, VITB6: 0.1, FOL: 40, VITC: 20,
  },
  meat: {
    ENERGY: 200, PROT: 20, FAT: 12, CHO: 0.1, NACL_EQ: 0.1, K: 330, CA: 5, MG: 22, FE: 0.7, ZN: 2.5,
    VITA_RAE: 10, VITD: 0.5, VITE_ATOC: 0.3, VITK: 5, VITB1: 0.8, VITB2: 0.2, NIA: 6, VITB6: 0.3, VITB12: 0.5, FOL: 5, VITC: 1,
  },
  chicken: {
    ENERGY: 120, PROT: 24, FAT: 2, CHO: 0, NACL_EQ: 0.1, K: 370, CA: 4, MG: 28, FE: 0.4, ZN: 0.6,
    VITA_RAE: 8, VITD: 0.1, VITE_ATOC: 0.2, VITK: 18, VITB1: 0.1, VITB2: 0.1, NIA: 11, VITB6: 0.6, VITB12: 0.2, FOL: 10, VITC: 2,
  },
  fish: {
    ENERGY: 140, PROT: 20, FAT: 6, CHO: 0, NACL_EQ: 0.1, K: 380, CA: 15, MG: 28, FE: 0.5, ZN: 0.5,
    VITA_RAE: 30, VITD: 8, VITE_ATOC: 1.2, VITB1: 0.15, VITB2: 0.15, NIA: 8, VITB6: 0.5, VITB12: 5, FOL: 20, VITC: 1,
  },
  grain: {
    ENERGY: 155, PROT: 2.5, FAT: 0.3, CHO: 35, FIBTG: 0.5, K: 30, CA: 3, MG: 7, FE: 0.1, ZN: 0.6,
    VITB1: 0.02, VITB2: 0.01, NIA: 0.2, VITB6: 0.02, FOL: 3,
  },
  bread: {
    ENERGY: 260, PROT: 9, FAT: 4, CHO: 46, FIBTG: 4, NACL_EQ: 1.2, K: 100, CA: 25, MG: 20, FE: 0.6, ZN: 0.7,
    VITE_ATOC: 0.2, VITB1: 0.07, VITB2: 0.05, NIA: 1.5, VITB6: 0.05, FOL: 30,
  },
  egg: {
    ENERGY: 140, PROT: 12, FAT: 10, CHO: 0.3, NACL_EQ: 0.4, K: 130, CA: 50, MG: 10, FE: 1.5, ZN: 1.3,
    VITA_RAE: 180, VITD: 3.8, VITE_ATOC: 1.3, VITK: 12, VITB1: 0.06, VITB2: 0.4, NIA: 0.1, VITB6: 0.08, VITB12: 1.1, FOL: 45,
  },
  tofu: {
    ENERGY: 80, PROT: 7, FAT: 4.5, CHO: 2, FIBTG: 0.4, K: 140, CA: 90, MG: 30, FE: 0.9, ZN: 0.6,
    VITE_ATOC: 0.2, VITK: 10, VITB1: 0.08, VITB2: 0.03, NIA: 0.1, VITB6: 0.05, FOL: 12,
  },
  natto: {
    ENERGY: 190, PROT: 16, FAT: 10, CHO: 12, FIBTG: 6, K: 660, CA: 90, MG: 100, FE: 3.3, ZN: 1.9,
    VITE_ATOC: 0.5, VITK: 600, VITB1: 0.07, VITB2: 0.56, NIA: 1.1, VITB6: 0.24, VITB12: 0, FOL: 120,
  },
  milk: {
    ENERGY: 67, PROT: 3.3, FAT: 3.8, CHO: 4.8, NACL_EQ: 0.1, K: 150, CA: 110, MG: 10, FE: 0, ZN: 0.4,
    VITA_RAE: 38, VITD: 0.3, VITE_ATOC: 0.1, VITB1: 0.04, VITB2: 0.15, NIA: 0.1, VITB6: 0.03, VITB12: 0.3, FOL: 5, VITC: 1,
  },
  yogurt: {
    ENERGY: 62, PROT: 3.6, FAT: 3, CHO: 4.9, NACL_EQ: 0.1, K: 170, CA: 120, MG: 12, FE: 0, ZN: 0.4,
    VITA_RAE: 33, VITD: 0, VITE_ATOC: 0.1, VITB1: 0.03, VITB2: 0.14, NIA: 0.1, VITB6: 0.04, VITB12: 0.1, FOL: 11, VITC: 1,
  },
  fruit: {
    ENERGY: 90, PROT: 1, FAT: 0.2, CHO: 22, FIBTG: 1.1, K: 360, CA: 6, MG: 32, FE: 0.3, ZN: 0.2,
    VITA_RAE: 5, VITE_ATOC: 0.5, VITB1: 0.05, VITB2: 0.04, NIA: 0.7, VITB6: 0.38, FOL: 26, VITC: 16,
  },
  miso: {
    ENERGY: 190, PROT: 12, FAT: 6, CHO: 21, FIBTG: 4, NACL_EQ: 12, K: 380, CA: 100, MG: 80, FE: 4, ZN: 2,
    VITA_RAE: 0, VITE_ATOC: 0.4, VITK: 10, VITB1: 0.05, VITB2: 0.1, NIA: 1.5, VITB6: 0.1, VITB12: 0.1, FOL: 70, VITC: null,
  },
  soy: {
    ENERGY: 70, PROT: 7, FAT: 0, CHO: 8, NACL_EQ: 14.5, K: 390, CA: 30, MG: 65, FE: 1.7, ZN: 0.8,
    VITB1: 0.05, VITB2: 0.1, NIA: 1.3, VITB6: 0.17, FOL: 33,
  },
  oil: { ENERGY: 920, FAT: 100, VITE_ATOC: 10, VITK: 100 },
};

export interface MockFoodDef {
  id: number;
  foodCode: string;
  name: string;
  nameKana: string;
  group: string;
  refuseRatePct: number;
  arch: keyof typeof ARCH;
  override?: Partial100;
  trace?: string[];
  units: { unitName: string; gramsPerUnit: number; isDefault?: boolean }[];
  aliases: string[];
}

export const FOODS: MockFoodDef[] = [
  { id: 1, foodCode: 'D0001', name: 'にんじん', nameKana: 'にんじん', group: '野菜類', refuseRatePct: 10, arch: 'vegGreen',
    override: { PROT: 1.0, VITC: 10, VITA_RAE: 700 }, units: [{ unitName: '本', gramsPerUnit: 150, isDefault: true }], aliases: ['人参', 'ニンジン'] },
  { id: 2, foodCode: 'D0002', name: '豚こま肉', nameKana: 'ぶたこまにく', group: '肉類', refuseRatePct: 0, arch: 'meat',
    override: { PROT: 20, VITC: 1, VITA_RAE: 10 }, units: [], aliases: ['豚肉', 'ぶた肉', '豚こま'] },
  { id: 3, foodCode: 'D0003', name: 'みそ', nameKana: 'みそ', group: '調味料類', refuseRatePct: 0, arch: 'miso',
    units: [{ unitName: '大さじ', gramsPerUnit: 18, isDefault: true }, { unitName: '小さじ', gramsPerUnit: 6 }], aliases: ['味噌', 'ミソ'] },
  { id: 4, foodCode: 'D0004', name: 'ご飯（白米）', nameKana: 'ごはん', group: '穀類', refuseRatePct: 0, arch: 'grain',
    units: [{ unitName: '茶碗1杯', gramsPerUnit: 150, isDefault: true }], aliases: ['ごはん', 'ライス', '白飯', '米'] },
  { id: 5, foodCode: 'D0005', name: '鶏むね肉（皮なし）', nameKana: 'とりむねにく', group: '肉類', refuseRatePct: 0, arch: 'chicken',
    units: [{ unitName: '1枚', gramsPerUnit: 250, isDefault: true }], aliases: ['鶏胸肉', 'とりむね', 'チキン'] },
  { id: 6, foodCode: 'D0006', name: 'さけ', nameKana: 'さけ', group: '魚介類', refuseRatePct: 0, arch: 'fish',
    override: { ASTX: 3 }, units: [{ unitName: '1切れ', gramsPerUnit: 80, isDefault: true }], aliases: ['鮭', 'サケ', 'シャケ'] },
  { id: 7, foodCode: 'D0007', name: '卵', nameKana: 'たまご', group: '卵類', refuseRatePct: 15, arch: 'egg',
    units: [{ unitName: '1個', gramsPerUnit: 60, isDefault: true }], aliases: ['玉子', '鶏卵', 'タマゴ', 'エッグ'] },
  { id: 8, foodCode: 'D0008', name: 'キャベツ', nameKana: 'きゃべつ', group: '野菜類', refuseRatePct: 15, arch: 'vegLight',
    units: [{ unitName: '1枚', gramsPerUnit: 50, isDefault: true }, { unitName: '1/4個', gramsPerUnit: 300 }], aliases: ['甘藍'] },
  { id: 9, foodCode: 'D0009', name: 'たまねぎ', nameKana: 'たまねぎ', group: '野菜類', refuseRatePct: 6, arch: 'vegLight',
    units: [{ unitName: '1個', gramsPerUnit: 200, isDefault: true }], aliases: ['玉ねぎ', '玉葱', 'タマネギ', 'オニオン'] },
  { id: 10, foodCode: 'D0010', name: 'ほうれんそう', nameKana: 'ほうれんそう', group: '野菜類', refuseRatePct: 10, arch: 'vegGreen',
    override: { VITC: 35, FOL: 210 }, units: [{ unitName: '1束', gramsPerUnit: 200, isDefault: true }], aliases: ['ほうれん草', 'ホウレンソウ'] },
  { id: 11, foodCode: 'D0011', name: 'ブロッコリー', nameKana: 'ぶろっこりー', group: '野菜類', refuseRatePct: 50, arch: 'vegGreen',
    override: { VITC: 120, VITA_RAE: 67 }, units: [{ unitName: '1株', gramsPerUnit: 250, isDefault: true }], aliases: ['ブロッコリ'] },
  { id: 12, foodCode: 'D0012', name: 'じゃがいも', nameKana: 'じゃがいも', group: 'いも類', refuseRatePct: 10, arch: 'vegLight',
    override: { ENERGY: 76, CHO: 17, K: 410, VITC: 28 }, units: [{ unitName: '1個', gramsPerUnit: 150, isDefault: true }], aliases: ['ジャガイモ', '馬鈴薯'] },
  { id: 13, foodCode: 'D0013', name: 'だいこん', nameKana: 'だいこん', group: '野菜類', refuseRatePct: 10, arch: 'vegLight',
    units: [{ unitName: '1/4本', gramsPerUnit: 250, isDefault: true }], aliases: ['大根', 'ダイコン'] },
  { id: 14, foodCode: 'D0014', name: '木綿豆腐', nameKana: 'もめんどうふ', group: '豆類', refuseRatePct: 0, arch: 'tofu',
    units: [{ unitName: '1丁', gramsPerUnit: 300, isDefault: true }], aliases: ['豆腐', 'とうふ', 'トウフ'] },
  { id: 15, foodCode: 'D0015', name: '食パン', nameKana: 'しょくぱん', group: '穀類', refuseRatePct: 0, arch: 'bread',
    trace: ['CARTB_EQ'], units: [{ unitName: '6枚切り1枚', gramsPerUnit: 60, isDefault: true }], aliases: ['パン', 'トースト'] },
  { id: 16, foodCode: 'D0016', name: '牛乳', nameKana: 'ぎゅうにゅう', group: '乳類', refuseRatePct: 0, arch: 'milk',
    units: [{ unitName: 'コップ1杯', gramsPerUnit: 200, isDefault: true }], aliases: ['ミルク'] },
  { id: 17, foodCode: 'D0017', name: '納豆', nameKana: 'なっとう', group: '豆類', refuseRatePct: 0, arch: 'natto',
    units: [{ unitName: '1パック', gramsPerUnit: 45, isDefault: true }], aliases: ['ナットウ'] },
  { id: 18, foodCode: 'D0018', name: 'しょうゆ', nameKana: 'しょうゆ', group: '調味料類', refuseRatePct: 0, arch: 'soy',
    units: [{ unitName: '大さじ', gramsPerUnit: 18, isDefault: true }, { unitName: '小さじ', gramsPerUnit: 6 }], aliases: ['醤油', 'ショウユ'] },
  { id: 19, foodCode: 'D0019', name: 'サラダ油', nameKana: 'さらだあぶら', group: '油脂類', refuseRatePct: 0, arch: 'oil',
    units: [{ unitName: '大さじ', gramsPerUnit: 12, isDefault: true }], aliases: ['油', 'オイル'] },
  { id: 20, foodCode: 'D0020', name: 'バナナ', nameKana: 'ばなな', group: '果実類', refuseRatePct: 40, arch: 'fruit',
    units: [{ unitName: '1本', gramsPerUnit: 150, isDefault: true }], aliases: ['ばなな'] },
  { id: 21, foodCode: 'D0021', name: 'ヨーグルト（無糖）', nameKana: 'よーぐると', group: '乳類', refuseRatePct: 0, arch: 'yogurt',
    units: [{ unitName: '1カップ', gramsPerUnit: 100, isDefault: true }], aliases: ['ヨーグルト'] },
  { id: 22, foodCode: 'D0022', name: 'えび', nameKana: 'えび', group: '魚介類', refuseRatePct: 15, arch: 'fish',
    override: { ENERGY: 90, FAT: 0.6, ASTX: 1.2, VITD: 0, VITB12: 1.5 }, units: [{ unitName: '1尾', gramsPerUnit: 30, isDefault: true }], aliases: ['海老', 'エビ'] },
  { id: 23, foodCode: 'D0023', name: 'トマト', nameKana: 'とまと', group: '野菜類', refuseRatePct: 3, arch: 'vegLight',
    override: { VITA_RAE: 45, CARTB_EQ: 540, VITC: 15 }, units: [{ unitName: '1個', gramsPerUnit: 150, isDefault: true }], aliases: ['蕃茄'] },
];

/** 食材の 100g あたり含有量（栄養素コード→値。null は未測定、存在しないキーは行なし=未測定）。 */
export function foodPer100g(def: MockFoodDef): Record<string, number | null> {
  const base = ARCH[def.arch] ?? {};
  const out: Record<string, number | null> = {};
  for (const code of NUTRIENT_CODES) {
    if (code === 'ASTX') {
      if (def.override && 'ASTX' in def.override) out[code] = def.override['ASTX'] ?? null;
      continue;
    }
    if (def.override && code in def.override) out[code] = def.override[code] ?? null;
    else if (code in base) out[code] = base[code] ?? null;
    else out[code] = 0;
  }
  return out;
}

// ---- 目標（ダミー）。公式値ではない。docs/06.スコアリング仕様.md §6 のテスト用目標に合わせた一部を含む ----
export const DUMMY_LOWER_TARGETS: Record<string, number> = {
  VITC: 100,
  VITE_ATOC: 6,
  VITA_RAE: 700,
  ZN: 10,
  FIBTG: 20,
  CA: 600,
  FE: 8,
  MG: 300,
  K: 2500,
  VITB1: 1.2,
  VITB2: 1.4,
  VITB6: 1.2,
  VITB12: 2,
  FOL: 240,
  VITD: 8,
};

export const DUMMY_SALT_MAX_G = 7;

export const ACTIVITY_FACTOR: Record<number, number> = { 1: 1.2, 2: 1.375, 3: 1.55, 4: 1.725 };
export const PROTEIN_PER_KG: Record<string, number> = { skin: 1.0, both: 1.4, muscle: 1.6 };

export const BEAUTY_WEIGHTS: Record<string, number> = { VITC: 1, VITE_ATOC: 1, VITA_RAE: 1, ZN: 0.5 };
export const BASE_NUTRIENTS = ['FIBTG', 'CA', 'FE', 'MG', 'K', 'VITB1', 'VITB2', 'VITB6', 'VITB12', 'FOL', 'VITD'];
