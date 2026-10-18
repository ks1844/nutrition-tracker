// E2E テスト（Playwright）。代表シナリオ S-1〜S-5（docs/01.要件定義.md §3）を、スマホ幅（375px）の Chrome で通す。
// 事前に DB（3306）とバックエンド（8080）を起動しておく（フロントの開発サーバー 4200 は、起動していなければ自動で起動する）。
import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: 'e2e',
  testMatch: '**/*.e2e.ts',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:4200',
    channel: 'chrome', // インストール済みの Google Chrome を使う（ブラウザのダウンロードを避ける）
    viewport: { width: 375, height: 812 },
    hasTouch: true,
    locale: 'ja-JP',
    timezoneId: 'Asia/Tokyo',
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'npm start',
    url: 'http://localhost:4200',
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
