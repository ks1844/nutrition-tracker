# NutritionTracker — Claude Code 向けガイド

自炊（作り置き）向けの食事記録・栄養管理 Web アプリ。**仕様の正本は [docs/](docs/README.md)**。実装・修正の前に、該当する設計書を読む。本ファイルは目次であり、仕様は書かない。

## 言語

- 返答・質問・計画書・Issue/PR: **日本語**
- git コミットメッセージ: **日本語・体言止め**（「〜を追加」。「〜する」で終えない）
- コード内コメント:
  - **C# のクラス・メソッド・プロパティ・定数・テストには、日本語の XML ドキュメントコメントを付ける。** `<summary>` に「何をするのか」を具体的に書く（「API-07」のような番号だけにしない）。引数があれば `<param>`、戻り値があれば `<returns>` も書く。
  - テストは、何をしたらどうなることを確認するのかを `<summary>` に書く（期待値の具体例を含める）。
  - 処理の途中のコメント（`//`）は、理由が自明でない場合だけ書く。

## docs が正本

- 仕様は `docs/` が唯一の根拠。実装と食い違ったら、**文書を先に直してから**コードを合わせる。
- 暫定値は「暫定」、未決は [01.要件定義.md §9](docs/01.要件定義.md) の Q-n に集約。確証のない公式値（食事摂取基準・成分表の数値）を書かない。
- 栄養計算・スコアは正解データ（[10.受け入れ基準.md](docs/10.受け入れ基準.md)）で自動テストする。
- ID（FR / SCR / API / AC / ADR）で文書同士を参照する。トレースは FR → SCR → API → AC。

## 何をするときに何を読むか

| やること | 読む文書 |
|----------|----------|
| 全体像・文書の読む順・ID 体系 | [docs/README.md](docs/README.md) |
| 機能の要否・優先度・スコープ外・決定/未決事項 | [docs/01.要件定義.md](docs/01.要件定義.md) |
| 用語の意味・コード識別子 | [docs/02.用語集.md](docs/02.用語集.md) |
| 栄養の計算（式・丸め・欠測・廃棄率・単位換算） | [docs/03.栄養計算仕様.md](docs/03.栄養計算仕様.md) |
| テーブル構成・ER 図・栄養素コード・マイグレーション分割 | [docs/04.データモデル.md](docs/04.データモデル.md) |
| テーブル・列の定義、DDL、区分値（DB 物理名は日本語、テーブルは `T`＋日本語名） | [docs/05.テーブル定義書.md](docs/05.テーブル定義書.md) |
| スコア・目標値の算出 | [docs/06.スコアリング仕様.md](docs/06.スコアリング仕様.md) |
| 食材 CSV の取り込み・成分表記号の変換 | [docs/07.マスタデータ仕様.md](docs/07.マスタデータ仕様.md) |
| 画面（UI・遷移・項目・バリデーション） | [docs/08.画面設計.md](docs/08.画面設計.md) |
| API（パス・リクエスト/レスポンス・エラー形式） | [docs/09.API仕様.md](docs/09.API仕様.md) |
| 受け入れ基準・テストの期待値・FR との対応 | [docs/10.受け入れ基準.md](docs/10.受け入れ基準.md) |
| 使う技術（Angular / C# / MySQL）・バージョン・ポート | [docs/11.技術スタック.md](docs/11.技術スタック.md) |
| プロジェクト構成・レイヤ規約・命名規則 | [docs/12.アーキテクチャ.md](docs/12.アーキテクチャ.md) |
| 作る順序・各段階の完了条件 | [docs/13.開発手順.md](docs/13.開発手順.md) |
| AWS へのデプロイ・Terraform・費用管理・破棄手順 | [docs/14.デプロイ設計.md](docs/14.デプロイ設計.md) |
| なぜその設計にしたか | [docs/adr/](docs/adr/) |

## Git ルール

### コミットと PR の粒度

- **コミットは機能ごとに細かく分ける。** 更新機能の追加、削除機能の追加など、**1 つの機能（1 つの意図）につき 1 コミット**。機能の実装・テスト・関連する設計書の更新を同じコミットにし、各コミットでビルドとテストが通る状態にする。
- **PR は開発のまとまりごとに 1 つ**（例: バックエンド開発）。1 つの PR に、機能ごとのコミットを多数積む。PR は **Merge commit** でマージする（Squash すると機能ごとの履歴が失われる）。
- 作業ブランチ上では、機能 1 つが完了するたびにコミットしてよい（指示不要）。**プッシュ・PR 作成は、指示があったときだけ**行う。指示された範囲を超えない。
- コミットメッセージは **日本語・体言止め**（「〜を追加」で終え、「〜する」にしない）。`feat:` `fix:` `refactor:` `docs:` `test:` `chore:` などの接頭辞を付け、本文に理由と FR / API / AC の ID を書く。
- 詳細（粒度の基準、コミット列の例）→ [docs/13.開発手順.md §6](docs/13.開発手順.md)

### ブランチと禁止事項

- `dev` と `main` には**直接コミット・プッシュしない**。作業ブランチ（`feature/` `fix/` `refactor/` `docs/` で始める）を `dev` から、開発のまとまりごとに作る。
- `main` へのマージは `dev` 経由のみ。
- 強制プッシュ、`--no-verify`、`--amend`、プッシュ済みコミットの書き換えはしない（直すときは新しいコミットを作る）。
- `deploy` ブランチは成果物（ビルド出力）専用で、ソースとは履歴が独立。更新はビルド用スクリプト経由のみ、かつ**指示があったときだけ**行う（[docs/14.デプロイ設計.md §5](docs/14.デプロイ設計.md)）。
- 公開リポジトリのため、シークレット・個人情報をコミットしない。

## ポート（固定）

| サービス | ポート |
|----------|--------|
| バックエンド（ASP.NET Core） | 8080 |
| フロントエンド（Angular 開発サーバー） | 4200 |
| DB（MySQL） | 3306 |

競合時は別ポートで逃げず、占有プロセスを止めて固定ポートで起動する。

## バックエンド（C# / ASP.NET Core。`backend/`）

```bash
cd backend
cp .env.example .env && $EDITOR .env   # 初回のみ。パスワードを決める（.env はコミットしない）
docker compose up -d                   # MySQL 8.4（3306）
dotnet tool restore                    # 初回のみ（dotnet-ef）
# マイグレーションの適用（パスワードは .env の MYSQL_PASSWORD）
ConnectionStrings__Default="Server=localhost;Port=3306;Database=nutritiontracker;User ID=nutrition;Password=<パスワード>" \
  dotnet ef database update --project src/NutritionTracker.Api
# 起動（http://localhost:8080 ・ GET /health）。パスワードは環境変数で渡す（設定ファイルに書かない）
Database__Password=<パスワード> dotnet run --project src/NutritionTracker.Api
dotnet test                            # Domain のテスト + API のテスト（Docker が必要。Testcontainers が MySQL を自動で起動する）
```

- アプリはマイグレーションを自動では適用しない。起動時に `src/NutritionTracker.Api/Master/` の CSV があれば取り込む（無ければスキップ）→ [Master/README.md](backend/src/NutritionTracker.Api/Master/README.md)。成分表の実データはコミットしない（`.gitignore` で除外）。
- 係数・重み・閾値（スコア・目標）は `appsettings.json` の `Nutrition` に置く（すべて暫定）。

## フロントエンド（Angular。`frontend/`）

```bash
cd frontend
npm install        # 初回のみ（Node は LTS の 24 系）
npm start          # http://localhost:4200 （`/api` は proxy.conf.json で localhost:8080 へ）
npm test           # 単体テスト（Vitest）。`npx ng test --watch=false` で 1 回だけ実行
npm run build      # 本番ビルド（dist/）
```

- バックエンド（8080）の実 API につながる（`proxy.conf.json` が `/api` を転送）。先に DB とバックエンドを起動する。動作確認用のダミーの食材・目標は `backend/samples/master/` を `Master/` にコピーして取り込む → [backend/samples/master/README.md](backend/samples/master/README.md)
- デザインはグラスモーフィズム（トークンとクラスは `src/styles.css`）。状態は色だけで表現しない（アイコン・文言・数値を併記）。

## AWS の操作

- AWS のリソースは**すべて Terraform**で作る（コンソールで手作業しない）。`terraform plan` の差分を示し、`apply` / `destroy` は利用者が確認して実行する。
- 作成前に費用と無料枠を確認し、動作確認後はすぐ破棄する → [docs/14.デプロイ設計.md](docs/14.デプロイ設計.md)

## 実装時の要点（詳細は各文書）

- 栄養計算は `decimal`（HALF_UP = `MidpointRounding.AwayFromZero`）。`double`/`float` 禁止。計算ロジックは外部参照を持たない `NutritionTracker.Domain` プロジェクトに置く → [12.アーキテクチャ.md](docs/12.アーキテクチャ.md)
- DB のテーブルは `T`＋日本語名、列は日本語（主キー `ID`）。区分値・API の JSON は英語のまま → [docs/05.テーブル定義書.md](docs/05.テーブル定義書.md)
- フロント（Angular）は `any` 禁止。栄養・スコアの確定値はサーバーが返す。
- 状態（不足/達成/超過）は色だけで表現しない → [08.画面設計.md](docs/08.画面設計.md)
