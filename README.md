Qiita 記事
https://qiita.com/masayahak/items/bfbf5dea084a055f06c4

## ローカルでの実行手順

### 前提
- .NET SDK 9.0 以上
- PostgreSQL 16 以降

### データベース準備
1. PostgreSQL にデータベースを作成

   ```bash
   createdb blazor_order_app
   ```

2. 初期データを投入

   ```bash
   psql blazor_order_app -f Database/init.sql
   ```

### アプリのビルドと起動
1. 依存関係を復元してビルド

   ```bash
   dotnet build BlazorOrderApp/BlazorOrderApp.sln
   ```

2. 接続文字列を環境変数で設定して起動

   ```bash
   export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=blazor_order_app;Username=postgres;Password=postgres"
   dotnet run --project BlazorOrderApp/BlazorOrderApp.csproj
   ```

3. ブラウザーでアクセス

   - https://localhost:5001 (または http://localhost:5000)

### よくあるエラー
- "You must install or update .NET to run this application" が表示される場合は .NET 9.0 ランタイム/SDK をインストールしてください。
- "Npgsql.NpgsqlException (0x80004005): 28P01" は接続文字列内の認証情報が誤っている可能性があります。
- "3D000" が表示された場合は、対象データベースが存在しないため `createdb` と `psql -f Database/init.sql` の実行を確認してください。

## Docker Compose での実行

1. イメージをビルドしてサービスを起動

   ```bash
   docker compose up --build -d
   ```

2. ブラウザーでアクセス

   - http://localhost:8888

3. 停止する場合

   ```bash
   docker compose down
   ```

### ポート競合時の対処
- `Bind for 0.0.0.0:8080 failed: port is already allocated` が出る場合は `docker-compose.yml` のポート設定 (例: `8888:8080`) の左側を空きポートに変更してください。

## 単体コンテナーでの実行 (PostgreSQL を別途用意している場合)

1. アプリ用イメージをビルド

   ```bash
   docker build -t blazor-order-app .
   ```

2. 接続先を環境変数で指定して起動 (ホスト側ポートは任意の空きポート)

   ```bash
   docker run --rm \
     -e ConnectionStrings__DefaultConnection="Host=<host>;Port=5432;Database=blazor_order_app;Username=postgres;Password=postgres" \
     -p 8888:8080 \
     --name blazor-order-app \
     blazor-order-app
   ```

3. ブラウザーでアクセス

   - http://localhost:8888

4. コンテナー停止は `Ctrl+C` または別ターミナルで `docker stop blazor-order-app`
