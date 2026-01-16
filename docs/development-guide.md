# 開発者ガイド

このドキュメントでは、SREAgent_Testerの開発環境のセットアップ方法、カスタマイズ方法、デバッグ方法について説明します。

## 目次

- [開発環境のセットアップ](#開発環境のセットアップ)
- [プロジェクト構造の理解](#プロジェクト構造の理解)
- [ビルドと実行](#ビルドと実行)
- [新しいシナリオの追加](#新しいシナリオの追加)
- [デバッグとトラブルシューティング](#デバッグとトラブルシューティング)
- [Docker化](#docker化)
- [ベストプラクティス](#ベストプラクティス)

---

## 開発環境のセットアップ

### 必要なツール

1. **.NET 8 SDK**
   - [公式サイト](https://dotnet.microsoft.com/download/dotnet/8.0)からダウンロード
   - インストール確認:
     ```bash
     dotnet --version
     # 8.0.x が表示されること
     ```

2. **IDE（任意）**
   - Visual Studio 2022（Windows）
   - Visual Studio Code（全OS）
   - JetBrains Rider（全OS）

3. **Git**
   - リポジトリのクローンに使用

4. **Docker**（オプション）
   - コンテナでの実行・デプロイに使用

### リポジトリのクローン

```bash
git clone https://github.com/tokawa-ms/SREAgent_Tester.git
cd SREAgent_Tester
```

### 依存関係の復元

```bash
dotnet restore
```

これにより、以下のNuGetパッケージが復元されます:
- Microsoft.AspNetCore.App
- Microsoft.AspNetCore.Mvc.NewtonsoftJson
- その他必要な依存関係

---

## プロジェクト構造の理解

### ディレクトリ構成

```
SREAgent_Tester/
├── DiagnosticScenarios/              # メインプロジェクト
│   ├── Controllers/                  # MVCコントローラー
│   │   ├── DiagnosticScenarios.cs          # DiagScenarioController - 即座実行型API
│   │   ├── ScenarioToggleController.cs    # トグル型API
│   │   ├── HomeController.cs              # UIコントローラー
│   │   └── ValuesController.cs            # サンプルAPI
│   ├── Services/                     # ビジネスロジック
│   │   └── ScenarioToggleService.cs       # シナリオ管理
│   ├── Models/                       # データモデル
│   │   └── ScenarioToggleModels.cs        # リクエスト/レスポンスモデル
│   ├── Views/                        # Razorビュー
│   │   ├── Home/
│   │   │   ├── Index.cshtml               # 即座実行型UI
│   │   │   └── ToggleScenarios.cshtml     # トグル型UI
│   │   └── Shared/                        # 共通レイアウト
│   ├── wwwroot/                      # 静的ファイル
│   │   ├── css/
│   │   ├── js/
│   │   └── lib/                           # クライアントライブラリ
│   ├── Properties/
│   │   └── launchSettings.json            # 起動設定
│   ├── appsettings.json              # 本番設定
│   ├── appsettings.Development.json  # 開発環境設定
│   ├── appsettings.Production.json   # 本番環境設定
│   ├── Program.cs                    # エントリポイント
│   ├── Startup.cs                    # アプリケーション構成
│   └── DiagnosticScenarios.csproj    # プロジェクトファイル
├── docs/                             # ドキュメント
├── Dockerfile                        # Dockerイメージ定義
├── docker-compose.yml                # Docker Compose設定
└── SREAgent_Tester.sln               # ソリューションファイル
```

### 主要ファイルの役割

**Program.cs**
- アプリケーションのエントリポイント
- Webホストの構築と起動

**Startup.cs**
- 依存性注入の設定（ConfigureServices）
- HTTPリクエストパイプラインの構成（Configure）

**Controllers/**
- HTTPリクエストを受け付け、レスポンスを返す
- ビジネスロジックはServiceレイヤーに委譲

**Services/**
- ビジネスロジックの実装
- 状態管理や複雑な処理を担当

**Models/**
- データ構造の定義
- リクエスト/レスポンスのスキーマ

---

## ビルドと実行

### ローカル実行

#### コマンドライン

```bash
# プロジェクトディレクトリに移動
cd DiagnosticScenarios

# ビルドと実行
dotnet run

# または明示的に
dotnet build
dotnet run --project DiagnosticScenarios.csproj
```

アプリケーションは以下のURLで起動します:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`（開発環境のみ）

#### Visual Studio

1. `SREAgent_Tester.sln`を開く
2. F5キーを押してデバッグ実行
3. またはCtrl+F5で非デバッグ実行

#### Visual Studio Code

1. フォルダを開く
2. F5キーを押してデバッグ実行
3. `.vscode/launch.json`が自動生成される

### 環境変数の設定

#### 開発環境

```bash
# Windows (PowerShell)
$env:ASPNETCORE_ENVIRONMENT="Development"

# Linux / macOS
export ASPNETCORE_ENVIRONMENT=Development
```

#### 本番環境

```bash
# Windows (PowerShell)
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:ASPNETCORE_URLS="http://+:8080"

# Linux / macOS
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://+:8080
```

### 設定ファイルの優先順位

1. 環境変数
2. `appsettings.{Environment}.json`
3. `appsettings.json`

---

## 新しいシナリオの追加

### 即座実行型シナリオの追加

**手順:**

1. **DiagScenarioController（DiagnosticScenarios.cs）にメソッドを追加**

```csharp
/// <summary>
/// ネットワーク遅延をシミュレートするシナリオ
/// </summary>
/// <param name="delayMs">遅延時間（ミリ秒）</param>
/// <returns>成功メッセージ</returns>
[HttpGet]
[Route("networkdelay/{delayMs:int}")]
public async Task<ActionResult<string>> NetworkDelay(int delayMs)
{
    if (delayMs < 0 || delayMs > 10000)
    {
        return BadRequest("delayMs must be between 0 and 10000.");
    }

    await Task.Delay(delayMs);
    
    return $"success:networkdelay ({delayMs}ms)";
}
```

2. **ドキュメントを更新**

`docs/api-reference.md`と`docs/scenarios.md`に新しいエンドポイントを追加

3. **テスト**

```bash
curl http://localhost:5000/api/DiagScenario/networkdelay/1000
```

### トグル型シナリオの追加

より複雑なため、段階的に説明します。

#### ステップ1: 列挙型に追加

**Models/ScenarioToggleModels.cs:**

```csharp
public enum ScenarioToggleType
{
    ProbabilisticFailure,
    CpuSpike,
    MemoryLeak,
    ProbabilisticLatency,
    NetworkInstability  // 新しいシナリオ
}
```

#### ステップ2: リクエストモデルの作成

**Models/ScenarioToggleModels.cs:**

```csharp
/// <summary>
/// ネットワーク不安定性シナリオの設定
/// </summary>
public sealed class NetworkInstabilityRequest
{
    /// <summary>シナリオの実行時間（分）</summary>
    [Range(1, 180)]
    public int DurationMinutes { get; set; }

    /// <summary>1秒あたりのリクエスト数</summary>
    [Range(1, 1000)]
    public int RequestsPerSecond { get; set; }

    /// <summary>タイムアウト発生率（%）</summary>
    [Range(0, 100)]
    public int TimeoutPercentage { get; set; }

    /// <summary>タイムアウト時間（ミリ秒）</summary>
    [Range(1000, 30000)]
    public int TimeoutMilliseconds { get; set; }
}
```

#### ステップ3: サービスインターフェースに追加

**Services/ScenarioToggleService.cs:**

```csharp
public interface IScenarioToggleService
{
    // 既存のメソッド...
    
    /// <summary>
    /// ネットワーク不安定性シナリオを開始します
    /// </summary>
    Task<ScenarioStatus> StartNetworkInstabilityAsync(
        NetworkInstabilityRequest request, 
        CancellationToken cancellationToken);
}
```

#### ステップ4: サービス実装に追加

**Services/ScenarioToggleService.cs:**

```csharp
internal sealed class ScenarioToggleService : IScenarioToggleService
{
    // 既存のコード...

    public Task<ScenarioStatus> StartNetworkInstabilityAsync(
        NetworkInstabilityRequest request, 
        CancellationToken cancellationToken)
    {
        return StartScenarioAsync(
            ScenarioToggleType.NetworkInstability, 
            request.DurationMinutes, 
            request, 
            cancellationToken, 
            token => RunNetworkInstabilityAsync(request, token));
    }

    private async Task RunNetworkInstabilityAsync(
        NetworkInstabilityRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var windowStart = DateTime.UtcNow;
                var batch = new List<Task>(request.RequestsPerSecond);
                
                for (int i = 0; i < request.RequestsPerSecond; i++)
                {
                    batch.Add(SimulateNetworkRequestAsync(
                        request.TimeoutPercentage, 
                        request.TimeoutMilliseconds, 
                        cancellationToken));
                }

                await Task.WhenAll(batch).ConfigureAwait(false);

                var remaining = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - windowStart);
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 予期されたキャンセル
        }
    }

    private async Task SimulateNetworkRequestAsync(
        int timeoutPercentage, 
        int timeoutMs, 
        CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        
        if (Random.Shared.Next(0, 100) < timeoutPercentage)
        {
            await Task.Delay(timeoutMs, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

#### ステップ5: コントローラーにエンドポイントを追加

**Controllers/ScenarioToggleController.cs:**

```csharp
[HttpPost("network-instability/start")]
public Task<ActionResult<ScenarioStatus>> StartNetworkInstability(
    [FromBody] NetworkInstabilityRequest request,
    CancellationToken cancellationToken) =>
    ExecuteStartAsync(() => _service.StartNetworkInstabilityAsync(request, cancellationToken));

[HttpPost("network-instability/stop")]
public ActionResult<ScenarioStatus> StopNetworkInstability()
{
    return Ok(_service.StopScenario(ScenarioToggleType.NetworkInstability));
}
```

#### ステップ6: テスト

```bash
# 開始
curl -X POST http://localhost:5000/api/ScenarioToggle/network-instability/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":10,"requestsPerSecond":50,"timeoutPercentage":20,"timeoutMilliseconds":5000}'

# 状態確認
curl http://localhost:5000/api/ScenarioToggle/status

# 停止
curl -X POST http://localhost:5000/api/ScenarioToggle/network-instability/stop
```

---

## デバッグとトラブルシューティング

### ログの有効化

**appsettings.Development.json:**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "DiagnosticScenarios": "Debug"
    }
  }
}
```

### Visual Studioでのデバッグ

1. ブレークポイントを設定
2. F5でデバッグ実行
3. リクエストを送信してブレークポイントで停止

### ログの確認

```bash
# コンソールにログが出力される
dotnet run
```

### よくある問題

#### ポートが既に使用されている

**エラー:**
```
Unable to bind to http://localhost:5000
```

**解決策:**
```bash
# 異なるポートを指定
dotnet run --urls "http://localhost:5001"
```

#### メモリ不足

**症状:** メモリリークシナリオでアプリケーションがクラッシュ

**解決策:**
- パラメータを小さくする
- Dockerの場合、メモリ制限を増やす

```bash
docker run -m 2g sre-agent-tester
```

#### タイムアウト

**症状:** 長時間実行されるシナリオでタイムアウト

**解決策:**
- HTTPクライアントのタイムアウトを増やす
- リバースプロキシのタイムアウトを調整

---

## Docker化

### Dockerイメージのビルド

```bash
# プロジェクトルートで
docker build -t sre-agent-tester:latest .
```

### コンテナの実行

```bash
docker run -d \
  --name sre-tester \
  -p 8080:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  sre-agent-tester:latest
```

### Docker Composeの使用

```bash
docker-compose up -d
```

### マルチステージビルドの理解

`Dockerfile`はマルチステージビルドを使用:

1. **ビルドステージ** - SDKイメージで.NETアプリをビルド
2. **ランタイムステージ** - 軽量なランタイムイメージで実行

これにより、最終的なイメージサイズが小さくなります。

---

## ベストプラクティス

### コーディング規約

1. **XML ドキュメントコメント**
   - 全てのpublicメソッドに追加
   - パラメータと戻り値を説明

2. **非同期プログラミング**
   - I/O処理は必ず`async/await`を使用
   - `Task.Result`や`Task.Wait()`を避ける

3. **エラーハンドリング**
   - 予期される例外は適切にキャッチ
   - 予期しない例外はログに記録

4. **依存性注入**
   - コンストラクタインジェクションを使用
   - インターフェースに依存

### パフォーマンス

1. **メモリ管理**
   - 大きなオブジェクトは`using`で確実に破棄
   - 不要なオブジェクトの参照を保持しない

2. **スレッドプール**
   - CPU集中処理は`Task.Run`で実行
   - I/O処理はスレッドプールを使わない

3. **並行性**
   - `ConcurrentDictionary`などのスレッドセーフなコレクションを使用
   - `lock`は必要最小限に

### セキュリティ

1. **入力検証**
   - 全ての入力パラメータを検証
   - `[Range]`属性を使用

2. **アクセス制御**
   - テスト環境でのみ使用
   - ネットワークレベルで制限

3. **ログ**
   - 機密情報をログに出力しない
   - 適切なログレベルを使用

### テスト

#### 手動テスト

```bash
# 健全性チェック
curl http://localhost:5000/api/DiagScenario/taskasyncwait

# 状態確認
curl http://localhost:5000/api/ScenarioToggle/status
```

#### 自動テスト（今後の拡張）

将来的にはxUnitなどを使用したユニットテストの追加を推奨:

```csharp
[Fact]
public async Task StartCpuSpike_ShouldReturnOk()
{
    // Arrange
    var service = new ScenarioToggleService(logger);
    var request = new CpuSpikeRequest { /* ... */ };
    
    // Act
    var result = await service.StartCpuSpikeAsync(request, CancellationToken.None);
    
    // Assert
    Assert.True(result.IsActive);
}
```

---

## 次のステップ

開発を始める準備ができました！

- 既存のシナリオを確認: [scenarios.md](scenarios.md)
- APIの使い方を学ぶ: [api-reference.md](api-reference.md)
- アーキテクチャを理解: [architecture.md](architecture.md)

質問や問題があれば、GitHubのIssueで報告してください。
