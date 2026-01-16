# SRE Agent Tester

.NET 8 / ASP.NET Core で構築した診断シナ� �� オ� ? です� � �`/Home/Index` から即時実行シナリオ、`/Home/ToggleScenarios` からバックグラウンドで動き続ける継続的な� ? ストシナ� �� オを制御し� � �S RE Agent � ? モニタリングパイプ� �� インを安� ? �に検証でき� �� す� �?

## セ� ? トア� ? � ?

1. リポジト� �� をクローンし� ���? �� �� ジェクト� �� ー� ? (`SREAgent_Tester`) へ移動します� �?
2. 依存関係を復� ? しビルドします� �?
   ```bash
   dotnet restore
   dotnet build
   ```
3. アプ� �� を起動します� �?
   ```bash
   dotnet run --project DiagnosticScenarios/DiagnosticScenarios.csproj
   ```
4. ブ� �� ウザーで `http://localhost:5000/` を開き� ��� ��� ? のペ� ? �ジにアクセスし� �� す� �?

Docker / Azure App Service などのホス� ? ィングも従来通り利用可能です� �?

### Docker コン� ? ナでの実�?

1. ルートで Docker イメージをビルドします� �?
   ```bash
   docker build -t sre-agent-tester .
   ```
2. ポ� ? �トをホスト� �� 公開して起動しま� ? (� ?: 8080)� ?
   ```bash
   docker run --rm -p 8080:8080 -e ASPNETCORE_URLS=http://+:8080 sre-agent-tester
   ```
   - HTTPS が不� �な ため `ASPNETCORE_URLS` で HTTP ポ� ? �ト� ? �み公開� �?
   - 既定� ? � `appsettings.Development.json` を使� ? 場合� ? � `--env ASPNETCORE_ENVIRONMENT=Development` を付与します� �?
3. � ? 数コン� ? ナや依存サービスが� �� る� �� � ? `docker-compose up -d` で同梱の Compose 定義を利用でき� �� す� �?

停止する際� ? � `Ctrl+C` で終� ? するか� ��� ��ターミナルから `docker stop <CONTAINER_ID>` を実行してく� �� さい� ?

## UI とシナ� �� オ

- `Home/Index` : 例外バースト� ��メ モリスパイク、CPU 高� ? 荷など即時発火する API を呼び出すデモカード� �?
- `Home/ToggleScenarios` : ProbabilisticFailure / CpuSpike / MemoryLeak / ProbabilisticLatency をトグルで開始し� �� ��� ? 予定時刻� ? 実行状態を確認でき� �� す� �?

� ? シナ� �� オの API � �� ��ラメーター� �� ��用上� ? �注意� ? � `docs/scenarios.md` に� � 覧化して� ? ます� � �� �� 用前� �� � ? ず確認してく� �� さい� ?

## 主なコー� ?

- `DiagnosticScenarios/Controllers/DiagnosticScenarios.cs`
  - DiagScenarioController - 即時実行シナ� �� オ API 。� �� モリリーク� ? スパイク処� ? をスレ� ? ド安� ? �にリファクタリング済� �� � ?
- `DiagnosticScenarios/Controllers/ScenarioToggleController.cs`
  - バックグラウンド実行シナ� �� オの開� ? / 停止エンド� ? �イント� �?
- `DiagnosticScenarios/Services/ScenarioToggleService.cs`
  - � ? トグルシナ� �� オのワーカーループ� ? �状態管� ? を実�? � ?

## 注意事� ??

- す� �� て� ? ス� ? / 検証環� ? 専用です� �?**本番環� ? では絶対に実行しな� ? でく� �� さい� ?**
- 想定以上� ? �� ? 荷を� �� けるため� �� ��ラメーターは環� ? の CPU / メモリに応じて調整してく� �� さい� ?
- 実行中は `dotnet-counters`, `dotnet-trace` などでランタイ� ? � ? 標を採取� �� る� �� 効果的です� �?

## 参� �?

こ� ? �アプ� �� は [Diagnostic scenarios sample debug target](https://github.com/dotnet/samples/tree/main/core/diagnostics/DiagnosticScenarios) を� ? �� ? �スにカスタマイズして� ? ます� �?
