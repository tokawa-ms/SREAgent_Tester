# アーキテクチャ概要

このドキュメントでは、SREAgent_Testerの全体的なアーキテクチャと設計思想について説明します。

## 目的

SREAgent_Testerは、SRE（Site Reliability Engineering）ツールやモニタリングシステムの検証を目的とした診断テストアプリケーションです。様々な障害シナリオや負荷パターンを意図的に発生させることで、監視ツール、トレーシングシステム、診断ツールが正しく動作することを確認できます。

## 技術スタック

- **フレームワーク**: ASP.NET Core 8.0
- **言語**: C# 12
- **アーキテクチャパターン**: MVC（Model-View-Controller）
- **依存性注入**: ASP.NET Core標準DIコンテナ
- **ログ**: Microsoft.Extensions.Logging
- **JSON シリアライズ**: Newtonsoft.Json

## プロジェクト構造

```
SREAgent_Tester/
├── DiagnosticScenarios/          # メインアプリケーション
│   ├── Controllers/              # API エンドポイント
│   │   ├── DiagScenarioController.cs      # 即座実行型シナリオ
│   │   ├── ScenarioToggleController.cs    # トグル型シナリオ
│   │   └── HomeController.cs              # UI コントローラー
│   ├── Services/                 # ビジネスロジック
│   │   └── ScenarioToggleService.cs       # シナリオ管理サービス
│   ├── Models/                   # データモデル
│   │   └── ScenarioToggleModels.cs        # シナリオ設定モデル
│   ├── Views/                    # Razor ビュー（UI）
│   ├── wwwroot/                  # 静的ファイル
│   ├── Program.cs                # エントリポイント
│   └── Startup.cs                # アプリケーション構成
├── docs/                         # ドキュメント
└── Docker関連ファイル
```

## アーキテクチャ図

```
┌─────────────────────────────────────────────────────────┐
│                    クライアント                          │
│  （ブラウザ / curl / テストツール / SREエージェント）     │
└────────────────┬────────────────────────────────────────┘
                 │ HTTP/HTTPS
                 ▼
┌─────────────────────────────────────────────────────────┐
│              ASP.NET Core ミドルウェア                   │
│  （ルーティング / 認証 / 静的ファイル / エラーハンドリング）│
└────────────────┬────────────────────────────────────────┘
                 │
        ┌────────┴────────┐
        │                 │
        ▼                 ▼
┌──────────────┐  ┌──────────────────┐
│ HomeController│  │ API Controllers  │
│  （Web UI）   │  │                  │
└──────────────┘  └────────┬─────────┘
                           │
              ┌────────────┴────────────┐
              │                         │
              ▼                         ▼
    ┌──────────────────┐    ┌─────────────────────┐
    │ DiagScenario     │    │ ScenarioToggle      │
    │ Controller       │    │ Controller          │
    │ （即座実行）      │    │ （バックグラウンド）  │
    └──────────────────┘    └──────────┬──────────┘
                                       │
                                       ▼
                            ┌──────────────────────┐
                            │ ScenarioToggle       │
                            │ Service              │
                            │ （状態管理・実行）     │
                            └──────────────────────┘
```

## 2つのシナリオタイプ

### 1. 即座実行型シナリオ（DiagScenarioController）

**特徴:**
- APIリクエストを受けたら即座に実行
- 完了するまでレスポンスを返さない
- 単発の診断テストに最適

**ユースケース:**
- デッドロック検出ツールのテスト
- CPU スパイクの即座発生
- メモリスパイクの短時間テスト
- 例外トレーシングのテスト
- タスク待機パターンの比較

**エンドポイント例:**
- `GET /api/DiagScenario/deadlock`
- `GET /api/DiagScenario/highcpu/5000`
- `GET /api/DiagScenario/exception`

### 2. トグル型シナリオ（ScenarioToggleController + Service）

**特徴:**
- 開始/停止を別々のAPIで制御
- バックグラウンドタスクとして長時間実行
- 複数のシナリオを並行実行可能
- リアルタイムで状態確認可能

**ユースケース:**
- 長時間の負荷テスト
- 継続的な障害注入
- 確率的な問題の再現
- SREエージェントの持続的なテスト

**エンドポイント例:**
- `POST /api/ScenarioToggle/cpu-spike/start`
- `POST /api/ScenarioToggle/cpu-spike/stop`
- `GET /api/ScenarioToggle/status`

## コア設計パターン

### 依存性注入（DI）

全てのサービスとコントローラーは依存性注入パターンを使用しています。

```csharp
// Startup.cs でサービスを登録
services.AddSingleton<IScenarioToggleService, ScenarioToggleService>();

// コントローラーでサービスを注入
public ScenarioToggleController(IScenarioToggleService service)
{
    _service = service;
}
```

**利点:**
- テストが容易（モックに置き換え可能）
- 疎結合なコード
- ライフタイム管理が自動化

### シングルトンサービス

`ScenarioToggleService`はシングルトンとして登録されており、アプリケーション全体で単一のインスタンスが共有されます。これにより、複数のリクエスト間でシナリオの状態を維持できます。

### スレッドセーフな状態管理

バックグラウンドタスクの状態は`ConcurrentDictionary`と`lock`を使用してスレッドセーフに管理されています。

```csharp
private readonly ConcurrentDictionary<ScenarioToggleType, ScenarioState> _state;

lock (state.SyncRoot)
{
    // 状態の安全な更新
}
```

### 非同期プログラミング（async/await）

全てのI/O処理や長時間実行される処理は非同期パターンを使用しています。

```csharp
public async Task<ActionResult<string>> MemSpike(int seconds, CancellationToken cancellationToken)
{
    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
}
```

**利点:**
- スレッドプールの枯渇を防ぐ
- 高いスケーラビリティ
- より多くの並行リクエストを処理可能

### キャンセルトークンのサポート

全ての長時間実行される処理は`CancellationToken`をサポートしており、グレースフルなシャットダウンが可能です。

## データフロー

### トグル型シナリオの開始フロー

```
1. クライアントが POST /api/ScenarioToggle/cpu-spike/start を呼び出し
   ↓
2. ScenarioToggleController が受信
   ↓
3. リクエストボディから CpuSpikeRequest をデシリアライズ
   ↓
4. ScenarioToggleService.StartCpuSpikeAsync() を呼び出し
   ↓
5. サービスが状態を確認（既に実行中なら例外）
   ↓
6. バックグラウンドタスクを開始
   ↓
7. シナリオの状態を返す（200 OK）
   ↓
8. バックグラウンドタスクが指定時間まで実行
   ↓
9. タスクが完了し、状態を更新
```

### 状態確認フロー

```
1. クライアントが GET /api/ScenarioToggle/status を呼び出し
   ↓
2. ScenarioToggleController が受信
   ↓
3. ScenarioToggleService.GetStatuses() を呼び出し
   ↓
4. 全シナリオの現在状態を取得
   ↓
5. JSON形式で返す（実行中か、終了予定時刻、設定など）
```

## スケーラビリティの考慮事項

### 単一インスタンス設計

現在の実装は単一インスタンスでの実行を想定しています。複数インスタンスに水平スケールする場合、以下の点に注意が必要です：

- シナリオ状態は各インスタンスでローカル管理されている
- ロードバランサーを使用する場合、セッションアフィニティが必要
- 分散環境では外部状態ストア（Redis等）の導入を検討

### リソース管理

- メモリリークシナリオは意図的にメモリを保持するため、コンテナやVMのメモリ制限に注意
- CPUスパイクシナリオは全コアを使用する可能性がある
- 複数のシナリオを同時実行すると、リソース競合が発生する可能性がある

## セキュリティ考慮事項

**⚠️ 重要な警告:**

このアプリケーションは**テスト・検証環境専用**です。本番環境では絶対に実行しないでください。

**理由:**
- 意図的にシステムリソースを枯渇させる
- デッドロックや例外を発生させる
- メモリリークを引き起こす
- 認証・認可の仕組みがない

**推奨事項:**
- ネットワークレベルでアクセス制限
- 専用の隔離された環境で実行
- リソース制限（CPU、メモリ）を設定
- 定期的にアプリケーションを再起動

## 拡張性

### 新しいシナリオの追加

新しいトグル型シナリオを追加する手順：

1. `ScenarioToggleType`列挙型に新しい値を追加
2. リクエストモデルクラスを作成（範囲検証を含む）
3. `IScenarioToggleService`にメソッドシグネチャを追加
4. `ScenarioToggleService`に実装を追加
5. `ScenarioToggleController`にエンドポイントを追加
6. ドキュメントを更新

新しい即座実行型シナリオを追加する手順：

1. `DiagScenarioController`に新しいアクションメソッドを追加
2. ルート属性を設定
3. パラメータ検証を実装
4. ドキュメントを更新

## 監視とログ

### ログレベル

- **Information**: シナリオの開始・完了、重要な状態変更
- **Warning**: 予期された例外（確率的障害など）
- **Error**: 予期しない例外、シナリオの失敗

### 推奨される監視メトリクス

- CPU使用率（CPUスパイクシナリオの検証）
- メモリ使用量（メモリリークシナリオの検証）
- スレッドプール統計（タスク待機シナリオの検証）
- 例外レート（例外シナリオの検証）
- レスポンスタイム（レイテンシシナリオの検証）

## まとめ

SREAgent_Testerは、シンプルながら強力な診断ツールです。2つの異なるシナリオタイプにより、即座のテストと長時間の負荷テストの両方に対応しています。拡張性を考慮した設計により、新しいシナリオの追加も容易です。

次のステップ:
- [API リファレンス](api-reference.md) - 全エンドポイントの詳細
- [開発者ガイド](development-guide.md) - 開発・カスタマイズ方法
- [シナリオ一覧](scenarios.md) - 利用可能なシナリオの詳細
