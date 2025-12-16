# ビルド用の SDK イメージ
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 依存関係のリストだけを先にコピーしてキャッシュを確保
COPY BlazorOrderApp/BlazorOrderApp.csproj BlazorOrderApp/
RUN dotnet restore "BlazorOrderApp/BlazorOrderApp.csproj"

# 残りのソースをコピーしてビルド
COPY . .
RUN dotnet publish "BlazorOrderApp/BlazorOrderApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 実行用の ASP.NET ランタイム イメージ
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Kestrel を 8080/8443 で待ち受けるように設定
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=blazor_order_app;Username=postgres;Password=postgres"
EXPOSE 8080

COPY --from=build /app/publish ./
RUN chown -R app:app /app

ENTRYPOINT ["dotnet", "BlazorOrderApp.dll"]
