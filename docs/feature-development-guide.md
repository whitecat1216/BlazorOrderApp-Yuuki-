# BlazorOrderApp 画面構成と開発フロー

## 画面構成概要

```
BlazorOrderApp/
├── Components/
│   ├── App.razor                # HTML テンプレートとエントリーポイント
│   ├── Routes.razor             # ルーティング定義
│   ├── Layout/
│   │   ├── MainLayout.razor     # 共通レイアウト
│   │   └── NavMenu.razor        # ナビゲーションメニュー
│   └── Pages/
│       ├── Dashboard/
│       │   └── Dashboard.razor  # 分析ダッシュボード
│       ├── Orders/
│       │   ├── Orders.razor     # 受注一覧
│       │   ├── OrderEdit.razor  # 受注明細編集
│       │   └── OrderHistory.razor
│       ├── Products/
│       │   └── Products.razor
│       └── Customers/
│           └── Customers.razor
├── Models/                      # データモデル
├── Repositories/                # データアクセス (Dapper + PostgreSQL)
├── Services/
│   ├── DependencyInjection.cs   # DI 設定
│   └── *.cs                     # 認証・状態管理サービス
├── Pages/
│   ├── Login.cshtml             # 認証ページ
│   └── Logout.cshtml
├── Database/init.sql            # 初期データ投入 SQL
├── appsettings*.json            # 接続文字列など環境設定
└── wwwroot/                     # 静的アセット (CSS/JS など)
```

- **ルート/レイアウト**
  - `Components/App.razor`: HTML テンプレート、`Routes` コンポーネントのエントリーポイント。
  - `Components/Routes.razor`: アプリ全体のルーティング定義。既定レイアウトは `Layout/MainLayout.razor`。
  - `Components/Layout/MainLayout.razor`: 共通ヘッダー・ナビ・コンテンツ領域を管理。
- **ナビゲーション**
  - `Components/Layout/NavMenu.razor`: メインメニュー。「分析」「受注」「受注履歴」「商品マスタ」「得意先マスタ」「ログアウト」を表示。ロール別表示もここで制御。
- **主要ページ**
  - 分析: `Components/Pages/Dashboard/Dashboard.razor`
  - 受注: `Components/Pages/Orders`（一覧、詳細/編集、履歴）
  - 商品/得意先: `Components/Pages/Products`、`Components/Pages/Customers`
  - 認証関係: `Pages/Login.cshtml`、`Pages/Logout.cshtml`
- **バックエンド/サービス**
  - リポジトリ: `Repositories` フォルダ（Dapper + PostgreSQL）
  - モデル: `Models` フォルダ
  - DI 設定: `Services/DependencyInjection.cs`
  - 認証・状態管理: `Services` 配下の各クラス

## 新機能開発フロー

1. **要件整理**
   - 画面の目的、URL、必要データと操作を確認。
   - 既存モデル (`Models` フォルダ) と DTO/ビューモデルの流用可否を判断。

2. **データ層の準備**
   - 必要なクエリ・処理をリポジトリ (`Repositories`) に実装。
   - 追加したインターフェース実装は `Services/DependencyInjection.cs` で `AddScoped` 登録を確認。
   - DB 更新が必要な場合は `Database/init.sql` など初期化 SQL も更新。

3. **UI 実装**
   - `Components/Pages` 下の適切なフォルダに `.razor` を新規作成。`@page` でルート定義。
   - 必要なサービスを `[Inject]` し、マークアップとバインディングを実装。
   - レイアウトや既存コンポーネント (`Components/Commons`) を活用して UI を統一。

4. **ナビゲーション更新**
   - 新画面をメニューに載せる場合は `Components/Layout/NavMenu.razor` にリンクを追加。
   - 権限制御が必要なら `<AuthorizeView Roles="...">` を利用。

5. **スタイル/スクリプト**
   - 共通 CSS/JS は `wwwroot` 配下に配置。`wwwroot/app.css` など既存ファイルに追記。
   - ページ固有スタイルは `MainLayout.razor.css` など CSS 隣接ファイルを利用可能。

6. **設定調整**
   - 接続文字列や可変設定があれば `appsettings*.json` を更新。
   - 認証・認可要件を `Program.cs`、`Services` で確認。

7. **動作確認**
   - ローカル: `.NET 9` ランタイムがある環境で `dotnet watch run --project BlazorOrderApp/BlazorOrderApp.csproj`。
   - コンテナ: `docker compose up --build` で app/postgres を起動し、ブラウザーで確認。
   - ブラウザ: `https://localhost:port` で動作検証。必要に応じ DevTools でログ/Network を確認。

8. **ビルド/テスト**
   - `dotnet build` でエラー・警告を確認（`CS0414` など未使用フィールド警告にも注意）。
   - ユニットテストがあれば `dotnet test`。現状は手動検証が中心。

9. **ドキュメント/共有**
   - README などに起動手順や変更内容を反映。
   - 関連チームに画面の URL や操作手順を共有。

## メンテナンスのポイント

- **データベース**: PostgreSQL を使用。ローカル環境は `docker-compose.yml` の `postgres` サービスを活用。
- **接続文字列**: `appsettings.json` などで環境ごとに設定。Docker 実行時は環境変数で上書き。
- **エラーハンドリング**: サーバー側ログを `Logging` 設定で詳細化。`ProductEdit.razor` などでは `PostgresException` を捕捉。
- **ホットリロード**: `dotnet watch` 実行中はコード変更の自動反映が可能。ただし一部構成変更は再起動が必要。
- **ビルド警告**: 既知の `CS0414` 警告（未使用フィールド）が残っているため、随時リファクタリングを検討。
