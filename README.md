Qiita 記事
https://qiita.com/masayahak/items/bfbf5dea084a055f06c4

## ローカルでの実行手順

### 前提
- .NET SDK 9.0 以上がインストールされていること

### 手順
1. 依存関係を復元しビルド

   ```bash
   dotnet build BlazorOrderApp/BlazorOrderApp.sln
   ```

2. アプリを起動

   ```bash
   dotnet run --project BlazorOrderApp/BlazorOrderApp.csproj
   ```

3. ブラウザーでアクセス

   - https://localhost:5001 (または http://localhost:5000)

### よくあるエラー
- \"You must install or update .NET to run this application\" が表示される場合は .NET 9.0 ランタイム/SDK をインストールしてください。

## Docker での実行手順

1. イメージをビルド

   ```bash
   docker build -t blazor-order-app .
   ```

2. コンテナーを起動 (ホスト側のポートは空いている任意の番号を指定)

   ```bash
   docker run --rm -p 8888:8080 --name blazor-order-app blazor-order-app
   ```

3. ブラウザーでアクセス

   - http://localhost:8888

4. コンテナーを停止

   - `Ctrl+C` で停止するか、別ターミナルで `docker stop blazor-order-app` を実行します。

### ポート競合時の対処
- `Bind for 0.0.0.0:8080 failed: port is already allocated` が出る場合は `-p 8888:8080` の左側 (ホストポート) を別の空きポートに変更してください。

