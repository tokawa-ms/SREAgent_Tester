# API リファレンス

このドキュメントでは、SREAgent_Testerが提供する全てのAPIエンドポイントの詳細を説明します。

## 目次

- [即座実行型シナリオAPI](#即座実行型シナリオapi)
- [外部負荷ツール用API](#外部負荷ツール用api)
- [トグル型シナリオAPI](#トグル型シナリオapi)
- [共通仕様](#共通仕様)

---

## 即座実行型シナリオAPI

ベースURL: `/api/DiagScenario`

これらのエンドポイントは、リクエストを受けると即座にシナリオを実行し、完了するまでレスポンスを返しません。

### デッドロック

意図的にデッドロックを発生させます。

**エンドポイント:** `GET /api/DiagScenario/deadlock`

**パラメータ:** なし

**レスポンス:**
- 成功時: `200 OK` - `"success:deadlock"`
- 注意: デッドロックが発生すると、リクエストがハングする可能性があります

**使用例:**
```bash
curl http://localhost:5000/api/DiagScenario/deadlock
```

**説明:**
- 複数のスレッドが互いにロックを待ち合うことでデッドロックを引き起こします
- デッドロック検出ツールやスレッドダンプ分析のテストに使用

---

### 高CPU使用率

指定された時間、CPUビジーループを実行します。

**エンドポイント:** `GET /api/DiagScenario/highcpu/{milliseconds}`

**パスパラメータ:**
- `milliseconds` (整数): ビジーループの実行時間（ミリ秒）
  - 推奨範囲: 100～30000

**レスポンス:**
- 成功時: `200 OK` - `"success:highcpu"`

**使用例:**
```bash
# 5秒間CPU使用率を上げる
curl http://localhost:5000/api/DiagScenario/highcpu/5000
```

**説明:**
- CPUを集中的に使用するビジーループを実行
- CPU監視ツールやパフォーマンスプロファイラーのテストに最適

---

### メモリスパイク

定期的にメモリを大量確保・解放してメモリ使用量を急激に変動させます。

**エンドポイント:** `GET /api/DiagScenario/memspike/{seconds}`

**パスパラメータ:**
- `seconds` (整数): シナリオの実行時間（秒）
  - 範囲: 1～1800

**レスポンス:**
- 成功時: `200 OK` - `"success:memspike"`
- エラー時: `400 Bad Request` - `"seconds must be between 1 and 1800."`

**使用例:**
```bash
# 60秒間メモリスパイクを発生させる
curl http://localhost:5000/api/DiagScenario/memspike/60
```

**説明:**
- 5秒間隔でメモリを確保し、その後GCで解放を繰り返す
- メモリプロファイラーやGC監視ツールのテストに使用

---

### メモリリーク

指定されたサイズのメモリを確保し、解放せずに保持します。

**エンドポイント:** `GET /api/DiagScenario/memleak/{kilobytes}`

**パスパラメータ:**
- `kilobytes` (整数): 確保するメモリサイズ（KB）
  - 範囲: 1～10240

**レスポンス:**
- 成功時: `200 OK` - `"success:memleak ({kilobytes}KB retained)"`
- エラー時: `400 Bad Request` - `"kilobytes must be between 1 and 10240."`

**使用例:**
```bash
# 1MBのメモリリークを発生させる
curl http://localhost:5000/api/DiagScenario/memleak/1024
```

**説明:**
- 確保されたメモリは静的フィールドで保持され、アプリケーション再起動まで解放されない
- 複数回呼び出すとメモリリークが累積する
- メモリリーク検出ツールのテストに使用

---

### 例外

単純な例外を投げます。

**エンドポイント:** `GET /api/DiagScenario/exception`

**パラメータ:** なし

**レスポンス:**
- 常に: `500 Internal Server Error` - 例外メッセージ

**使用例:**
```bash
curl http://localhost:5000/api/DiagScenario/exception
```

**説明:**
- エラーハンドリング、ログ記録、トレーシングシステムのテストに使用

---

### 例外バースト

高頻度で例外を発生させます。

**エンドポイント:** `GET /api/DiagScenario/exceptionburst/{durationSeconds}/{exceptionsPerSecond}`

**パスパラメータ:**
- `durationSeconds` (整数): 実行時間（秒）
  - 範囲: 1～1800
- `exceptionsPerSecond` (整数): 1秒あたりの例外発生数
  - 範囲: 1～1000

**レスポンス:**
- 成功時: `200 OK` - `"success:exceptionburst ({total} exceptions generated)"`
- エラー時: `400 Bad Request` - パラメータエラーメッセージ

**使用例:**
```bash
# 30秒間、毎秒10個の例外を発生させる
curl http://localhost:5000/api/DiagScenario/exceptionburst/30/10
```

**説明:**
- 全ての例外はキャッチされ、ログに記録される
- エラーログやトレースの負荷テストに使用

---

### 確率的負荷

バックエンドリクエストをシミュレートし、確率的に例外を発生させます。

**エンドポイント:** `GET /api/DiagScenario/probabilisticload/{durationSeconds}/{requestsPerSecond}/{exceptionPercentage}`

**パスパラメータ:**
- `durationSeconds` (整数): 実行時間（秒）
  - 範囲: 1～1800
- `requestsPerSecond` (整数): 1秒あたりのリクエスト数
  - 範囲: 1～1000
- `exceptionPercentage` (整数): 例外発生率（%）
  - 範囲: 0～100

**レスポンス:**
- 成功時: `200 OK` - `"success:probabilisticload (durationSeconds=X, totalRequests=Y, successes=Z, failures=W)"`
- エラー時: `400 Bad Request` - パラメータエラーメッセージ

**使用例:**
```bash
# 60秒間、毎秒50リクエスト、20%の失敗率
curl http://localhost:5000/api/DiagScenario/probabilisticload/60/50/20
```

**説明:**
- 各リクエストは500msの遅延をシミュレート
- 指定された確率で例外を発生させる
- 負荷テストとエラー率の監視テストに使用

---

### タスク待機パターン（Task.Wait）

**非推奨パターン** - Task.Result/Task.Wait()を使用したブロッキング待機

**エンドポイント:** `GET /api/DiagScenario/taskwait`

**パラメータ:** なし

**レスポンス:**
- 成功時: `200 OK` - `"success:taskwait"`

**使用例:**
```bash
curl http://localhost:5000/api/DiagScenario/taskwait
```

**説明:**
- スレッドプール枯渇を引き起こす可能性がある問題のあるパターン
- パフォーマンス問題の診断教材として使用

---

### タスク待機パターン（Thread.Sleep）

**非推奨パターン** - Thread.Sleep()でタスク完了を待つスピンループ

**エンドポイント:** `GET /api/DiagScenario/tasksleepwait`

**パラメータ:** なし

**レスポンス:**
- 成功時: `200 OK` - `"success:tasksleepwait"`

**使用例:**
```bash
curl http://localhost:5000/api/DiagScenario/tasksleepwait
```

**説明:**
- スレッドを無駄に消費する問題のあるパターン
- パフォーマンス問題の診断教材として使用

---

### タスク待機パターン（async/await）

**推奨パターン** - async/awaitを使用した非ブロッキング待機

**エンドポイント:** `GET /api/DiagScenario/taskasyncwait`

**パラメータ:** なし

**レスポンス:**
- 成功時: `200 OK` - `"success:taskasyncwait"`

**使用例:**
```bash
curl http://localhost:5000/api/DiagScenario/taskasyncwait
```

**説明:**
- スレッドをブロックしない正しいパターン
- 他のtaskwaitエンドポイントとの比較に使用

---

## 外部負荷ツール用API

ベースURL: `/api/DirectTest`

これらのエンドポイントは、JMeterやApache Benchなどの外部負荷テストツールから直接呼び出すことを想定して設計されています。全てGETメソッドでクエリパラメーターを使用するため、負荷テストツールとの統合が容易です。

### OpenAPI/Swagger ドキュメント

詳細なAPIスキーマは、アプリケーション実行時に以下のURLで確認できます：
- Swagger UI: `http://localhost:5000/swagger`
- OpenAPI JSON: `http://localhost:5000/swagger/v1/swagger.json`

### ランダムな応答遅延

0から指定値までのランダムな遅延を発生させます。レイテンシのばらつきをシミュレートするのに最適です。

**エンドポイント:** `GET /api/DirectTest/RandomLatency`

**クエリパラメータ:**
- `maxLatencyInMilliSeconds` (整数): 最大遅延時間（ミリ秒）
  - 範囲: 0～30000

**レスポンス:**
- 成功時: `200 OK` - `"success:randomlatency (max=XXXms, actual=YYYms)"`
- エラー時: `400 Bad Request` - `"maxLatencyInMilliSeconds must be between 0 and 30000."`

**使用例:**
```bash
# 最大1秒のランダム遅延
curl "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000"

# JMeterの場合
# HTTP Request Sampler
# Protocol: http
# Server Name: localhost
# Port: 5000
# Path: /api/DirectTest/RandomLatency
# Parameters:
#   - Name: maxLatencyInMilliSeconds, Value: 1000
```

**説明:**
- 実際の遅延時間は0からmaxLatencyInMilliSecondsの間でランダムに決定
- レスポンスに実際の遅延時間が含まれる
- レイテンシの分布を観察する負荷テストに有用

---

### ランダムな例外

指定された確率で例外を発生させ、500エラーを返します。エラー率のシミュレーションに最適です。

**エンドポイント:** `GET /api/DirectTest/RandomException`

**クエリパラメータ:**
- `exceptionPercentage` (整数): 例外発生確率（パーセント）
  - 範囲: 0～100

**レスポンス:**
- 成功時: `200 OK` - `"success:randomexception (exceptionPercentage=XX%, no exception)"`
- 例外発生時: `500 Internal Server Error` - エラーメッセージ
- エラー時: `400 Bad Request` - `"exceptionPercentage must be between 0 and 100."`

**使用例:**
```bash
# 10%の確率で例外を発生
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=10"

# 必ず成功（エラーなし）
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=0"

# 必ず失敗（500エラー）
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=100"
```

**説明:**
- 乱数生成により確率的に例外を発生させる
- エラー率監視やSLO計算の検証に使用
- 例外が発生した場合、500 Internal Server Errorが返される

---

### メモリ確保

指定サイズのメモリを指定時間保持します。メモリ使用量の増加をシミュレートします。

**エンドポイント:** `GET /api/DirectTest/HighMem`

**クエリパラメータ:**
- `secondsToKeepMem` (整数): メモリを保持する秒数
  - 範囲: 1～300
- `keepMemSize` (整数): 確保するメモリサイズ（MB）
  - 範囲: 1～2048

**レスポンス:**
- 成功時: `200 OK` - `"success:highmem (kept XXXMBfor YYY seconds)"`
- エラー時: `400 Bad Request` - パラメータエラーメッセージ

**使用例:**
```bash
# 100MBを10秒間保持
curl "http://localhost:5000/api/DirectTest/HighMem?secondsToKeepMem=10&keepMemSize=100"

# 1GBを30秒間保持（負荷が高い）
curl "http://localhost:5000/api/DirectTest/HighMem?secondsToKeepMem=30&keepMemSize=1024"
```

**説明:**
- byte配列を使用してメモリを確保
- 指定時間経過後、自動的にメモリが解放される
- メモリ監視ツールやアラートのテストに使用
- 注意: 環境のメモリ容量を超えないよう注意してください

---

### CPU高負荷

指定時間、CPUビジーループを実行してCPU使用率を上げます。

**エンドポイント:** `GET /api/DirectTest/HighCPU`

**クエリパラメータ:**
- `millisecondsToKeepHighCPU` (整数): CPU高負荷を維持するミリ秒数
  - 範囲: 100～60000

**レスポンス:**
- 成功時: `200 OK` - `"success:highcpu (ran for XXXms)"`
- エラー時: `400 Bad Request` - `"millisecondsToKeepHighCPU must be between 100 and 60000."`

**使用例:**
```bash
# 5秒間CPU高負荷
curl "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=5000"

# 30秒間CPU高負荷
curl "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=30000"
```

**説明:**
- CPUビジーループを実行してCPUを占有
- CPU使用率監視ツールやパフォーマンスアラートのテストに使用
- 実際の実行時間がレスポンスに含まれる

---

### 負荷テストツールでの活用例

#### JMeterでの使用

**Thread Group設定:**
```
Number of Threads: 100
Ramp-Up Period: 10
Loop Count: 100
```

**HTTP Request Sampler 1 - ランダムレイテンシ:**
```
Protocol: http
Server Name: localhost
Port: 5000
Path: /api/DirectTest/RandomLatency
Parameters:
  - maxLatencyInMilliSeconds: 2000
```

**HTTP Request Sampler 2 - ランダム例外:**
```
Protocol: http
Server Name: localhost
Port: 5000
Path: /api/DirectTest/RandomException
Parameters:
  - exceptionPercentage: 5
```

#### Apache Benchでの使用

```bash
# 100並列、1000リクエスト
ab -n 1000 -c 100 "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=500"

# エラー率5%でテスト
ab -n 1000 -c 50 "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=5"
```

#### curlでの簡易負荷テスト

```bash
# 10回並列実行
for i in {1..10}; do
  curl "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000" &
done
wait
```

---

## トグル型シナリオAPI

ベースURL: `/api/ScenarioToggle`

これらのエンドポイントは、バックグラウンドタスクとして長時間実行されるシナリオを制御します。

### 全シナリオの状態取得

全てのトグル型シナリオの現在の状態を取得します。

**エンドポイント:** `GET /api/ScenarioToggle/status`

**パラメータ:** なし

**レスポンス:**
- 成功時: `200 OK` - シナリオ状態の配列

**レスポンス例:**
```json
[
  {
    "scenario": "ProbabilisticFailure",
    "isActive": true,
    "endsAtUtc": "2024-01-15T12:30:00Z",
    "lastMessage": "Running",
    "activeConfig": {
      "durationMinutes": 30,
      "requestsPerSecond": 100,
      "failurePercentage": 10
    }
  },
  {
    "scenario": "CpuSpike",
    "isActive": false,
    "endsAtUtc": null,
    "lastMessage": "Scenario finished",
    "activeConfig": null
  }
]
```

**使用例:**
```bash
curl http://localhost:5000/api/ScenarioToggle/status
```

---

### 確率的障害シナリオ

#### 開始

**エンドポイント:** `POST /api/ScenarioToggle/probabilistic-failure/start`

**リクエストボディ:**
```json
{
  "durationMinutes": 30,
  "requestsPerSecond": 100,
  "failurePercentage": 10
}
```

**パラメータ:**
- `durationMinutes` (整数): 実行時間（分）
  - 範囲: 1～180
- `requestsPerSecond` (整数): 1秒あたりのリクエスト数
  - 範囲: 1～1000
- `failurePercentage` (整数): 失敗率（%）
  - 範囲: 0～100

**レスポンス:**
- 成功時: `200 OK` - シナリオ状態
- 既に実行中: `409 Conflict` - エラーメッセージ

**使用例:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-failure/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":30,"requestsPerSecond":100,"failurePercentage":10}'
```

#### 停止

**エンドポイント:** `POST /api/ScenarioToggle/probabilistic-failure/stop`

**パラメータ:** なし

**レスポンス:**
- 成功時: `200 OK` - シナリオ状態

**使用例:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-failure/stop
```

---

### CPUスパイクシナリオ

#### 開始

**エンドポイント:** `POST /api/ScenarioToggle/cpu-spike/start`

**リクエストボディ:**
```json
{
  "durationMinutes": 60,
  "intervalSeconds": 30,
  "triggerPercentage": 50,
  "spikeSeconds": 10
}
```

**パラメータ:**
- `durationMinutes` (整数): 実行時間（分）
  - 範囲: 1～180
- `intervalSeconds` (整数): チェック間隔（秒）
  - 範囲: 1～300
- `triggerPercentage` (整数): スパイク発生確率（%）
  - 範囲: 0～100
- `spikeSeconds` (整数): スパイク持続時間（秒）
  - 範囲: 1～30

**レスポンス:**
- 成功時: `200 OK` - シナリオ状態
- 既に実行中: `409 Conflict` - エラーメッセージ

**使用例:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/cpu-spike/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":60,"intervalSeconds":30,"triggerPercentage":50,"spikeSeconds":10}'
```

#### 停止

**エンドポイント:** `POST /api/ScenarioToggle/cpu-spike/stop`

**パラメータ:** なし

**レスポンス:**
- 成功時: `200 OK` - シナリオ状態

**使用例:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/cpu-spike/stop
```

---

### メモリリークシナリオ

#### 開始

**エンドポイント:** `POST /api/ScenarioToggle/memory-leak/start`

**リクエストボディ:**
```json
{
  "durationMinutes": 60,
  "intervalSeconds": 60,
  "triggerPercentage": 80,
  "memoryMegabytes": 100,
  "holdSeconds": 30
}
```

**パラメータ:**
- `durationMinutes` (整数): 実行時間（分）
  - 範囲: 1～180
- `intervalSeconds` (整数): チェック間隔（秒）
  - 範囲: 1～300
- `triggerPercentage` (整数): メモリ確保確率（%）
  - 範囲: 0～100
- `memoryMegabytes` (整数): 確保サイズ（MB）
  - 範囲: 1～1024
- `holdSeconds` (整数): 保持時間（秒）
  - 範囲: 1～60

**レスポンス:**
- 成功時: `200 OK` - シナリオ状態
- 既に実行中: `409 Conflict` - エラーメッセージ

**使用例:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/memory-leak/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":60,"intervalSeconds":60,"triggerPercentage":80,"memoryMegabytes":100,"holdSeconds":30}'
```

#### 停止

**エンドポイント:** `POST /api/ScenarioToggle/memory-leak/stop`

**パラメータ:** なし

**レスポンス:**
- 成功時: `200 OK` - シナリオ状態

**説明:**
- 停止時に確保された全メモリが自動的に解放されます

**使用例:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/memory-leak/stop
```

---

### 確率的レイテンシシナリオ

#### 開始

**エンドポイント:** `POST /api/ScenarioToggle/probabilistic-latency/start`

**リクエストボディ:**
```json
{
  "durationMinutes": 30,
  "requestsPerSecond": 50,
  "triggerPercentage": 20,
  "delayMilliseconds": 1000
}
```

**パラメータ:**
- `durationMinutes` (整数): 実行時間（分）
  - 範囲: 1～180
- `requestsPerSecond` (整数): 1秒あたりのリクエスト数
  - 範囲: 1～1000
- `triggerPercentage` (整数): 遅延発生確率（%）
  - 範囲: 0～100
- `delayMilliseconds` (整数): 遅延時間（ミリ秒）
  - 範囲: 1～10000

**レスポンス:**
- 成功時: `200 OK` - シナリオ状態
- 既に実行中: `409 Conflict` - エラーメッセージ

**使用例:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-latency/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":30,"requestsPerSecond":50,"triggerPercentage":20,"delayMilliseconds":1000}'
```

#### 停止

**エンドポイント:** `POST /api/ScenarioToggle/probabilistic-latency/stop`

**パラメータ:** なし

**レスポンス:**
- 成功時: `200 OK` - シナリオ状態

**使用例:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-latency/stop
```

---

## 共通仕様

### Content-Type

- GET リクエスト: パラメータなし、またはURLパラメータ
- POST リクエスト: `Content-Type: application/json`

### レスポンス形式

全てのレスポンスはJSON形式またはプレーンテキスト形式です。

### エラーレスポンス

**400 Bad Request:**
```json
"パラメータが範囲外です"
```

**409 Conflict:**
```json
"Scenario CpuSpike is already running."
```

**500 Internal Server Error:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "traceId": "00-xxxxx-xxxxx-00"
}
```

### CORS

デフォルトではCORSは有効化されていません。必要に応じて`Startup.cs`で設定してください。

### 認証・認可

このアプリケーションには認証・認可の仕組みはありません。テスト環境専用として使用し、ネットワークレベルでアクセス制限を行ってください。

---

## 次のステップ

- [アーキテクチャ概要](architecture.md) - システムの全体像
- [開発者ガイド](development-guide.md) - カスタマイズ方法
- [シナリオ一覧](scenarios.md) - シナリオの詳細説明
