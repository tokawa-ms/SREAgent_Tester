# DirectTest API - 外部負荷ツール向けエンドポイント

このドキュメントでは、JMeterなどの外部負荷テストツールから直接呼び出すことを想定したDirectTest APIの詳細を説明します。

## 概要

DirectTest APIは、負荷テストツールとの統合を容易にするために設計されたシンプルなHTTP APIです。全てのエンドポイントはGETメソッドで、クエリパラメーターを使用して動作を制御します。

**ベースURL:** `/api/DirectTest`

**特徴:**
- シンプルなGET + クエリパラメーター
- 即座にレスポンスを返す（指定時間の処理後）
- XMLコメントによる日本語ドキュメント
- OpenAPI/Swagger完全対応

## OpenAPI/Swagger ドキュメント

アプリケーション実行時、以下のURLでインタラクティブなAPIドキュメントにアクセスできます：

- **Swagger UI:** `http://localhost:5000/swagger`
- **OpenAPI JSON:** `http://localhost:5000/swagger/v1/swagger.json`

Swagger UIでは、ブラウザから直接APIをテストすることもできます。

## エンドポイント詳細

### 1. RandomLatency - ランダムな応答遅延

レスポンス時間のばらつきをシミュレートします。

**エンドポイント:**
```
GET /api/DirectTest/RandomLatency
```

**パラメーター:**
| 名前 | 型 | 範囲 | 説明 |
|------|-----|------|------|
| maxLatencyInMilliSeconds | int | 0～30000 | 最大遅延時間（ミリ秒） |

**レスポンス例:**
```
success:randomlatency (max=1000ms, actual=536ms)
```

**JMeter設定例:**
```
HTTP Request Sampler
  Method: GET
  Path: /api/DirectTest/RandomLatency
  Parameters:
    Name: maxLatencyInMilliSeconds
    Value: ${__Random(100,2000)}  # 100～2000msのランダム値
```

**用途:**
- レイテンシ分布の観察
- パーセンタイル計測の検証
- タイムアウト設定のテスト

---

### 2. RandomException - ランダムな例外

指定確率で500エラーを返します。

**エンドポイント:**
```
GET /api/DirectTest/RandomException
```

**パラメーター:**
| 名前 | 型 | 範囲 | 説明 |
|------|-----|------|------|
| exceptionPercentage | int | 0～100 | 例外発生確率（パーセント） |

**レスポンス例（成功時）:**
```
success:randomexception (exceptionPercentage=10%, no exception)
```

**レスポンス例（例外時）:**
```
500 Internal Server Error
Random exception triggered (exceptionPercentage=10%)
```

**JMeter設定例:**
```
HTTP Request Sampler
  Method: GET
  Path: /api/DirectTest/RandomException
  Parameters:
    Name: exceptionPercentage
    Value: 5  # 5%のエラー率

Response Assertion (Optional)
  Response Code: 200|500  # 両方のコードを許可
```

**用途:**
- エラー率監視の検証
- SLO/SLI計算のテスト
- エラーハンドリングのテスト

---

### 3. HighMem - メモリ確保

指定サイズのメモリを指定時間保持します。

**エンドポイント:**
```
GET /api/DirectTest/HighMem
```

**パラメーター:**
| 名前 | 型 | 範囲 | 説明 |
|------|-----|------|------|
| secondsToKeepMem | int | 1～300 | メモリ保持時間（秒） |
| keepMemSize | int | 1～2048 | メモリサイズ（MB） |

**レスポンス例:**
```
success:highmem (kept 100MB for 10 seconds)
```

**JMeter設定例:**
```
HTTP Request Sampler
  Method: GET
  Path: /api/DirectTest/HighMem
  Parameters:
    Name: secondsToKeepMem, Value: 10
    Name: keepMemSize, Value: 100
  
  Timeouts:
    Connect: 5000
    Response: 15000  # secondsToKeepMem + バッファ
```

**用途:**
- メモリ使用量監視の検証
- メモリアラートのテスト
- OOMキラーの動作確認

**注意事項:**
- アプリケーションのメモリ制限を考慮してください
- Docker環境では`--memory`オプションで制限を設定することを推奨

---

### 4. HighCPU - CPU高負荷

CPUビジーループでCPU使用率を上げます。

**エンドポイント:**
```
GET /api/DirectTest/HighCPU
```

**パラメーター:**
| 名前 | 型 | 範囲 | 説明 |
|------|-----|------|------|
| millisecondsToKeepHighCPU | int | 100～60000 | CPU高負荷時間（ミリ秒） |

**レスポンス例:**
```
success:highcpu (ran for 5000ms)
```

**JMeter設定例:**
```
HTTP Request Sampler
  Method: GET
  Path: /api/DirectTest/HighCPU
  Parameters:
    Name: millisecondsToKeepHighCPU
    Value: 5000  # 5秒間
  
  Timeouts:
    Response: 10000  # millisecondsToKeepHighCPU + バッファ
```

**用途:**
- CPU使用率監視の検証
- CPUアラートのテスト
- スロットリング動作の確認

---

## 実践的な負荷テストシナリオ

### シナリオ1: 通常トラフィック + 障害混入

現実的なトラフィックをシミュレート：
- 95%: 正常リクエスト（低レイテンシ）
- 5%: エラーまたは高レイテンシ

**JMeter Thread Group:**
```
Thread Group "Normal Traffic"
  Threads: 80
  Ramp-up: 10
  Loop: Infinite
  
  HTTP Request "Normal"
    Path: /api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=500
    Weight: 95%
  
  HTTP Request "Error"
    Path: /api/DirectTest/RandomException?exceptionPercentage=100
    Weight: 5%
```

### シナリオ2: リソース負荷テスト

CPU、メモリ、レイテンシを組み合わせた複合負荷：

**JMeter Thread Group:**
```
Thread Group "Resource Load"
  Threads: 50
  Ramp-up: 5
  Duration: 300 seconds
  
  HTTP Request "CPU Load"
    Path: /api/DirectTest/HighCPU?millisecondsToKeepHighCPU=3000
    
  HTTP Request "Memory Load"
    Path: /api/DirectTest/HighMem?secondsToKeepMem=5&keepMemSize=50
    
  HTTP Request "Latency"
    Path: /api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=2000
```

### シナリオ3: 段階的負荷上昇

負荷を徐々に上げてシステムの限界を見つける：

**JMeter Stepping Thread Group:**
```
Stepping Thread Group
  This group will start: 10 threads
  Next, add: 10 threads
  Every: 30 seconds
  Until reaching: 200 threads
  
  HTTP Request "Gradual Load"
    Path: /api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000
```

---

## Apache Benchでの使用例

### 基本的な負荷テスト

```bash
# 1000リクエスト、100並列
ab -n 1000 -c 100 \
  "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=500"
```

### エラー率を含むテスト

```bash
# 5%エラー率でテスト
ab -n 1000 -c 50 \
  "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=5"
```

### CSV出力付き

```bash
ab -n 1000 -c 100 -g results.tsv \
  "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000"

# gnuplotでグラフ化
gnuplot << EOF
set terminal png
set output "latency.png"
set datafile separator "\t"
plot "results.tsv" using 9 with lines title "Response Time"
EOF
```

---

## curlでの簡易テスト

### 単発テスト

```bash
# レイテンシテスト
curl "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000"

# 例外テスト（成功パターン）
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=0"

# 例外テスト（失敗パターン）
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=100"
```

### 並列実行

```bash
# 10リクエスト並列実行
for i in {1..10}; do
  curl -s "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=2000" &
done
wait
echo "All requests completed"
```

### 継続的負荷生成

```bash
# 60秒間、1秒あたり5リクエスト
for i in {1..60}; do
  for j in {1..5}; do
    curl -s "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=500" &
  done
  sleep 1
done
```

---

## モニタリングとの統合

### Prometheusメトリクス監視

DirectTest APIを使用した負荷テスト中に、以下のメトリクスを監視することを推奨します：

```promql
# リクエスト数
rate(http_requests_total{endpoint=~"/api/DirectTest.*"}[1m])

# レイテンシ（P50, P95, P99）
histogram_quantile(0.50, http_request_duration_seconds_bucket{endpoint=~"/api/DirectTest.*"})
histogram_quantile(0.95, http_request_duration_seconds_bucket{endpoint=~"/api/DirectTest.*"})
histogram_quantile(0.99, http_request_duration_seconds_bucket{endpoint=~"/api/DirectTest.*"})

# エラー率
rate(http_requests_total{endpoint=~"/api/DirectTest.*",status_code="500"}[1m])
/ rate(http_requests_total{endpoint=~"/api/DirectTest.*"}[1m])
```

### Application Insightsでの監視

Azure Application Insightsを使用している場合：

```kusto
// リクエスト統計
requests
| where url contains "DirectTest"
| summarize 
    count=count(),
    avg_duration=avg(duration),
    p95_duration=percentile(duration, 95),
    success_rate=100.0 * countif(success == true) / count()
  by bin(timestamp, 1m)
```

---

## トラブルシューティング

### タイムアウトエラーが発生する

**原因:** パラメーターで指定した時間より短いタイムアウトが設定されている

**解決策:**
```
# JMeterの場合
HTTP Request Sampler > Timeouts
  Connect Timeout: 5000
  Response Timeout: (指定時間 × 1.5) 以上

# curlの場合
curl --max-time 30 "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=20000"
```

### メモリ不足エラー

**原因:** keepMemSizeが大きすぎる、または並列数が多すぎる

**解決策:**
- keepMemSizeを減らす
- 並列数を減らす
- Dockerメモリ制限を増やす: `docker run --memory="4g" ...`

### 400 Bad Requestエラー

**原因:** パラメーターが範囲外

**解決策:**
```bash
# エラー例
curl "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=100000"
# → 400: millisecondsToKeepHighCPU must be between 100 and 60000.

# 正しい例
curl "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=60000"
```

---

## ベストプラクティス

### 1. 段階的に負荷を上げる

```
開始: 10 threads
  ↓ 1分後
中間: 50 threads
  ↓ 1分後
最大: 100 threads
```

### 2. タイムアウトは余裕を持たせる

```
Response Timeout = Parameter Value × 1.5 + Connection Time
```

### 3. リソース制限を設定する

```bash
# Dockerの場合
docker run \
  --memory="2g" \
  --cpus="2" \
  -p 5000:8080 \
  sre-agent-tester
```

### 4. 監視メトリクスを確認しながら実行

- CPU使用率
- メモリ使用量
- レスポンスタイム
- エラー率

### 5. クールダウン期間を設ける

シナリオ間で30～60秒の待機時間を設定し、システムを安定させる

---

## 次のステップ

- [API リファレンス](api-reference.md) - 全APIエンドポイントの詳細
- [シナリオ一覧](scenarios.md) - 他のテストシナリオ
- [アーキテクチャ](architecture.md) - システムの全体像
- [開発者ガイド](development-guide.md) - カスタマイズ方法

負荷テストを実行して、監視システムやSREプラクティスの検証を始めましょう！
