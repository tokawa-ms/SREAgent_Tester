# SRE Agent Tester

.NET 8 / ASP.NET Core で構築した診断シナリオ集です。`/Home/Index` から即時実行シナリオ、`/Home/ToggleScenarios` からバックグラウンドで動き続ける継続的なテストシナリオを制御し、SRE Agent やモニタリングパイプラインを安全に検証できます。

## セットアップ

1. リポジトリをクローンし、プロジェクトルート (`SREAgent_Tester`) へ移動します。
2. 依存関係を復元しビルドします。
   ```bash
   dotnet restore
   dotnet build
   ```
3. アプリを起動します。
   ```bash
   dotnet run --project DiagnosticScenarios/DiagnosticScenarios.csproj
   ```
4. ブラウザーで `http://localhost:5000/` を開き、目的のページにアクセスします。

Docker / Azure App Service などのホスティングも従来通り利用可能です。

### Docker コンテナでの実行

1. ルートで Docker イメージをビルドします。
   ```bash
   docker build -t sre-agent-tester .
   ```
2. ポートをホストに公開して起動します (例: 8080)。
   ```bash
   docker run --rm -p 8080:8080 -e ASPNETCORE_URLS=http://+:8080 sre-agent-tester
   ```
   - HTTPS が不要なため `ASPNETCORE_URLS` で HTTP ポートのみ公開。
   - 既定の `appsettings.Development.json` を使う場合は `--env ASPNETCORE_ENVIRONMENT=Development` を付与します。
3. 複数コンテナや依存サービスがあるなら `docker-compose up -d` で同梱の Compose 定義を利用できます。

停止する際は `Ctrl+C` で終了するか、別ターミナルから `docker stop <CONTAINER_ID>` を実行してください。

## UI とシナリオ

- `Home/Index` : 例外バースト、メモリスパイク、CPU 高負荷など即時発火する API を呼び出すデモカード。
- `Home/ToggleScenarios` : ProbabilisticFailure / CpuSpike / MemoryLeak / ProbabilisticLatency をトグルで開始し、終了予定時刻や実行状態を確認できます。

各シナリオの API、パラメーター、利用上の注意は `docs/scenarios.md` に一覧化しています。運用前に必ず確認してください。

## 主なコード

- `DiagnosticScenarios/Controllers/DiagScenarioController.cs`
  - 即時実行シナリオ API。メモリリークやスパイク処理をスレッド安全にリファクタリング済み。
- `DiagnosticScenarios/Controllers/ScenarioToggleController.cs`
  - バックグラウンド実行シナリオの開始 / 停止エンドポイント。
- `DiagnosticScenarios/Services/ScenarioToggleService.cs`
  - 各トグルシナリオのワーカーループ・状態管理を実装。

## 注意事項

- すべてテスト / 検証環境専用です。**本番環境では絶対に実行しないでください。**
- 想定以上の負荷を避けるため、パラメーターは環境の CPU / メモリに応じて調整してください。
- 実行中は `dotnet-counters`, `dotnet-trace` などでランタイム指標を採取すると効果的です。

## 参考

このアプリは [Diagnostic scenarios sample debug target](https://github.com/dotnet/samples/tree/main/core/diagnostics/DiagnosticScenarios) をベースにカスタマイズしています。

## ライセンス / 帰属

- dotnet/samples 由来のコードは Creative Commons Attribution 4.0 International (CC BY 4.0) に従います。詳細は [LICENSE](LICENSE) を参照してください。
- 本リポジトリのオリジナル部分は MIT License を適用しています。詳細は [LICENSE-MIT](LICENSE-MIT) を参照してください。
