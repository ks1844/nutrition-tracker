# NutritionTracker フロントエンド

Angular 22（standalone・signals）+ Tailwind CSS 4。仕様の正本は [docs/](../docs/README.md)。

```bash
npm install
npm start      # http://localhost:4200
npm test       # 単体テスト（Vitest）
npm run build  # 本番ビルド
```

- 現在は **Mock API**（`src/app/api/mock/`、`src/environments/environment.ts` の `useMockApi`）で動作します。データはダミーです。
- 構成と規約: [docs/12.アーキテクチャ.md](../docs/12.アーキテクチャ.md) §3 / 開発の段階: [docs/13.開発手順.md](../docs/13.開発手順.md)
