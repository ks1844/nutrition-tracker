// 代表シナリオ S-1〜S-5（docs/01.要件定義.md §3）を、実 API につないだ画面で通す。
//
// 前提: DB・バックエンドを起動し、動作確認用のダミーのマスタ CSV（backend/samples/master/）を取り込んであること。
// 実行: npm run e2e（フロントの開発サーバーは、起動していなければ自動で起動する）。
//
// 開発用のデータベースを使うので、次のように動く。
//   ・メニューは、名前に実行ごとの印（E2E-xxxx）を付けて作り、最後に削除する（論理削除）。
//   ・食事の記録は、今日の「間食」だけを使い、最後に、このテストで作った記録を削除する。
//   ・プロフィールは、S-1 で保存する内容に更新する（1 件だけなので上書き）。
import { APIRequestContext, expect, Page, test } from '@playwright/test';

const RUN = Date.now().toString(36);
const MENU = `豚汁E2E-${RUN}`;

/** 日本時間の今日（YYYY-MM-DD） */
const today = (): string => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Tokyo' }).format(new Date());

interface RecordItem { menuName: string | null }
interface Record { id: number; mealType: string; items: RecordItem[] }

/** このテストで作った食事の記録を削除する。 */
async function cleanupRecords(request: APIRequestContext): Promise<void> {
  const day = await (await request.get(`/api/meal-records?date=${today()}`)).json() as { records: Record[] };
  for (const r of day.records) {
    const ours = (i: RecordItem): boolean => i.menuName?.startsWith('豚汁E2E-') === true;
    // 間食は、このテストが使う枠（単品の食材も含めて削除する）。それ以外の食事は、E2E のメニューだけの記録（残量カードの「1杯」で作られる）を削除する。
    if (r.items.some(ours) && (r.mealType === 'snack' || r.items.every(ours))) await request.delete(`/api/meal-records/${r.id}`);
  }
}

/** 今日の栄養の合計（エネルギー kcal）が、画面上の「○ / 2,155 kcal」から読める。 */
async function homeEnergy(page: Page): Promise<number> {
  const text = await page.getByRole('region', { name: 'エネルギーとPFC' }).innerText();
  return Number(/([\d,]+)\s*\/\s*[\d,]+\s*kcal/.exec(text)?.[1]?.replace(/,/g, ''));
}

test.describe.serial('代表シナリオ（実 API）', () => {
  test.afterAll(async ({ request }) => {
    await cleanupRecords(request);
    const menus = await (await request.get('/api/menus')).json() as { id: number; name: string }[];
    for (const m of menus.filter((x) => x.name.startsWith('豚汁E2E-'))) await request.delete(`/api/menus/${m.id}`);
  });

  test('S-1 初回設定: プロフィールを入力すると目標が算出され、エネルギー目標を手動で上書きできる', async ({ page, request }) => {
    await page.goto('/profile');
    await page.getByRole('radio', { name: '男性' }).click();
    await page.getByLabel('生年月日').fill('1996-04-01');
    await page.getByLabel('身長 (cm)').fill('170');
    await page.getByLabel('体重 (kg)').fill('65');
    await page.getByRole('radio', { name: '2：軽い活動（立ち仕事・通勤など）' }).click();
    await page.getByRole('radio', { name: '美肌と筋肉' }).click();

    // 入力途中でも、算出した目標が見える（65kg × 1.4 = 91.0g）
    await expect(page.getByText('2,155')).toBeVisible();
    await expect(page.getByText('91.0')).toBeVisible();

    // 手動で上書きして保存すると、上書きした値が実際に使う目標になる
    await page.getByRole('checkbox', { name: /手動で上書き/ }).check();
    await page.getByRole('spinbutton', { name: /エネルギー/ }).fill('1800');
    await page.getByRole('button', { name: '保存する' }).click();
    await expect(page).toHaveURL('/');
    const overridden = await (await request.get('/api/profile')).json();
    expect(overridden.targets.energyKcal).toEqual({ auto: 2155, override: 1800, effective: 1800 });
    await expect(page.getByRole('region', { name: 'エネルギーとPFC' })).toContainText('1,800 kcal');

    // 上書きを外すと、自動算出に戻る
    await page.goto('/profile');
    await page.getByRole('checkbox', { name: /手動で上書き/ }).uncheck();
    await page.getByRole('button', { name: '保存する' }).click();
    await expect(page).toHaveURL('/');
    const auto = await (await request.get('/api/profile')).json();
    expect(auto.targets.energyKcal).toEqual({ auto: 2155, override: null, effective: 2155 });
    expect(auto.targets.proteinG).toBe(91);
  });

  test('S-2 作り置きを登録: 食材を検索して追加し、リアルタイムに栄養を確認、完成後重量と杯数を決めて保存する', async ({ page }) => {
    await page.goto('/menus/new');
    await page.getByPlaceholder('例：豚汁').fill(MENU);
    await page.getByRole('button', { name: '次へ' }).click();

    const add = async (query: string, name: string, quantity: string): Promise<void> => {
      await page.getByRole('button', { name: '食材を追加' }).click();
      await page.getByPlaceholder('例：にんじん／ニンジン／人参').fill(query);
      await page.getByRole('button', { name: '選ぶ' }).first().click();
      await page.getByRole('spinbutton', { name: `${name}の数量` }).fill(quantity);
    };
    await add('ニンジン', 'にんじん', '2'); // カタカナで検索できる。単位は既定の「本」
    await add('豚こま', '豚こま肉', '400'); // g 入力
    await add('味噌', 'みそ', '4'); // 別名（漢字）で検索できる。単位は既定の「大さじ」

    // 入力するたびに栄養が計算される: 豚汁の正解データ（タンパク質 91.34g）
    const preview = page.getByRole('region', { name: '栄養プレビュー' });
    await expect(preview).toContainText('タンパク質');
    await expect(preview).toContainText('91.3 g');

    await page.getByRole('button', { name: '次へ' }).click();
    await page.getByLabel('鍋ごとの重さ (g)').fill('3000');
    await page.getByLabel('空の鍋の重さ (g)').fill('600');
    await expect(page.getByText(/完成後重量\s*2,400(\.0)?\s*g/)).toBeVisible();

    await page.getByRole('button', { name: '次へ' }).click();
    await page.getByRole('spinbutton', { name: '何杯分か' }).fill('8');
    await expect(page.getByText(/1杯の重さ\s*300(\.0)?\s*g/)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'お椀1杯あたり' })).toBeVisible();

    await page.getByRole('button', { name: '保存する' }).click();
    await expect(page).toHaveURL('/menus');
    const card = page.getByRole('listitem').filter({ hasText: MENU });
    await expect(card).toContainText('8.0杯分（お椀）');
    await expect(card).toContainText('1杯 300g');
    await expect(card).toContainText('8.0杯'); // 残量: 記録がないので全量
  });

  test('S-3 食べて記録: 豚汁を 1.5 杯と単品のご飯を g で記録し、残量が減る。ホームの残量カードから「1杯」を追加できる', async ({ page, request }) => {
    await page.goto('/menus');
    await page.getByRole('listitem').filter({ hasText: MENU }).getByRole('link', { name: '記録する' }).click();
    await page.getByRole('radio', { name: '間食' }).click();
    await page.getByRole('button', { name: '1.5杯' }).click();
    // 単品の食材（ご飯）を g で追加
    await page.getByRole('button', { name: '食材から' }).click();
    await page.getByPlaceholder('例：にんじん／ニンジン／人参').fill('ごはん');
    await page.getByRole('button', { name: '選ぶ' }).first().click();
    await page.getByRole('radio', { name: 'g', exact: true }).click();
    await page.getByRole('spinbutton', { name: 'ご飯（白米）の量' }).fill('150');
    // 画面を離れると自動で保存される（保存ボタンはない）
    await page.getByRole('link', { name: 'ホーム' }).click();
    await expect(page).toHaveURL('/');
    await expect(page.getByText('保存しました').first()).toBeVisible();

    const day = await (await request.get(`/api/meal-records?date=${today()}`)).json() as { records: { mealType: string; items: { menuName: string | null; foodName: string | null; portionRatio: number | null; amountG: number | null }[] }[] };
    const snack = day.records.find((r) => r.mealType === 'snack' && r.items.some((i) => i.menuName === MENU))!;
    const soup = snack.items.find((i) => i.menuName === MENU)!;
    const rice = snack.items.find((i) => i.foodName === 'ご飯（白米）')!;
    expect(soup.portionRatio).toBe(0.1875); // 1.5 杯 ÷ 8 杯
    expect(rice.amountG).toBe(150);
    expect(rice.portionRatio).toBeNull(); // 単品の食材は食べた割合を使わない

    // メニュー一覧の残量: 8 杯 − 1.5 杯 = 6.5 杯
    await page.goto('/menus');
    await expect(page.getByRole('listitem').filter({ hasText: MENU })).toContainText('6.5杯');

    // ホームの残量カードから、ワンタップで 1 杯分を追加する（AC-REC-011）
    await page.goto('/');
    const card = page.getByRole('region', { name: '作り置き残量' }).getByRole('listitem').filter({ hasText: MENU });
    await expect(card).toContainText('残り 6.5杯');
    await card.getByRole('button', { name: '1杯' }).click();
    await expect(card).toContainText('残り 5.5杯'); // 1 杯分（= 全体の 1/8）減る
  });

  test('S-4 1 日の確認: kcal・PFC・3 スコア・栄養素ごとの達成状況が、数値と文言つきで見られる。未測定の注記と免責が出る', async ({ page }) => {
    await page.goto('/');
    const energy = page.getByRole('region', { name: 'エネルギーとPFC' });
    await expect(energy).toContainText('/ 2,155 kcal');
    expect(await homeEnergy(page)).toBeGreaterThan(0);
    await expect(energy.getByRole('img', { name: /^PFCバランス/ })).toBeVisible();
    const scores = page.getByRole('region', { name: '今日の進捗' });
    for (const name of ['美肌', '筋肉', '基礎']) {
      await expect(scores.getByRole('img', { name: new RegExp(`^${name} \\d+点 (良好|もう少し|不足気味)`) })).toBeVisible();
    }

    await page.goto('/dashboard');
    await expect(page.getByRole('heading', { name: '今日の進捗' })).toBeVisible();
    const bars = page.locator('app-nutrient-bar');
    await expect(bars.filter({ hasText: 'タンパク質' }).first()).toContainText('目標 91.0g'); // プロフィールから算出した目標
    await expect(bars.filter({ hasText: 'タンパク質' }).first()).toContainText('不足'); // 色だけでなく文言も併記
    await expect(bars.filter({ hasText: '食塩相当量' })).toContainText('上限 7.0g'); // 上限だけの目標
    await expect(bars.filter({ hasText: 'ビタミンC' }).getByTitle('未測定の食材を含みます')).toBeVisible(); // みその未測定
    await expect(page.getByText('医療的助言ではありません').first()).toBeVisible();
  });

  test('S-5 週の振り返り: 直近 7 日平均に切り替え、栄養素の寄与食材を確認し、メニューを複製・削除できる', async ({ page }) => {
    await page.goto('/dashboard');
    await page.getByRole('button', { name: '直近7日平均' }).click();
    await expect(page.getByText(/直近7日（\d+\/\d+〜\d+\/\d+）/)).toBeVisible();
    // 記録が 3 日以上あればスコア、足りなければ「データ不足」。どちらでも 0 点などを出さない
    await expect(page.getByText(/データ不足|7日平均のスコア/).first()).toBeVisible();

    // 栄養素をタップすると、何から摂れているかが分かる
    await page.goto('/dashboard');
    await page.locator('app-nutrient-bar').filter({ hasText: 'タンパク質' }).first().click();
    await expect(page).toHaveURL(/\/nutrients\/PROT/);
    await expect(page.getByText('何から摂れているか')).toBeVisible();
    await expect(page.getByText(MENU).first()).toBeVisible();

    // 次の調理の参考に、メニューを複製して作り直す。不要になったら削除する
    await page.goto('/menus');
    await page.getByRole('listitem').filter({ hasText: MENU }).first().getByRole('button', { name: '複製' }).click();
    await expect(page).toHaveURL(/\/menus\/\d+\/edit/); // 複製すると、内容を確認できるよう編集画面が開く
    await expect(page.getByRole('textbox', { name: 'メニュー名' })).toHaveValue(`${MENU}のコピー`);

    await page.goto('/menus');
    const copy = page.getByRole('listitem').filter({ hasText: `${MENU}のコピー` });
    await expect(copy).toBeVisible();
    await copy.getByRole('button', { name: '削除' }).click();
    await page.getByRole('button', { name: '削除する' }).click();
    await expect(copy).toHaveCount(0);
  });
});
