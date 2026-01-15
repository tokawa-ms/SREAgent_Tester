# シナリオ一覧

このドキュメントでは、SREAgent_Testerで利用可能な全てのシナリオの詳細を説明します。

## 目次

- [即時実行型シナリオ](#即時実行型シナリオ)
- [トグル型シナリオ](#トグル型シナリオ)
- [実行時の注意事項](#実行時の注意事項)
- [ユースケース別推奨シナリオ](#ユースケース別推奨シナリオ)

---

## 即時実行型シナリオ

ベースURL: `/api/DiagScenario`

これらのシナリオは、HTTPリクエストを受けると即座に実行され、完了するまでレスポンスを返しません。単発のテストや特定の問題の再現に最適です。

### シナリオ一覧表

| エンドポイント | 説明 | 主なパラメーター | 実行時間の目安 |
| --- | --- | --- | --- |
| deadlock | 相互にロックを取り合うスレッドを大量生成してハングを再現 | なし | 永続的（ハング） |
| highcpu/{milliseconds} | 指定時間 CPU をビジーループで専有 | milliseconds (100-30000) | パラメータ通り |
| memspike/{seconds} | 5秒間隔でメモリを大量確保→解放する波形を繰り返す | seconds (1-1800) | パラメータ通り |
| memleak/{kilobytes} | 指定サイズのオブジェクトを保持し続け意図的にメモリをリーク | kilobytes (1-10240) | 瞬時 |
| exception | 即時に例外を投げる | なし | 瞬時 |
| exceptionburst/{durationSeconds}/{exceptionsPerSecond} | 高頻度で例外を投げ続け、ログ/メトリックの飽和を再現 | durationSeconds (1-1800), exceptionsPerSecond (1-1000) | パラメータ通り |
| probabilisticload/{durationSeconds}/{requestsPerSecond}/{exceptionPercentage} | 疑似バックエンド呼び出しを行い確率的に例外を発生 | durationSeconds, requestsPerSecond, exceptionPercentage | パラメータ通り |
| taskwait | 非推奨同期待ちパターンでスレッドプール枯渇を再現 | なし | 約0.5秒 |
| tasksleepwait | 非推奨スピン待ちパターンでスレッドプール枯渇を再現 | なし | 約0.5秒 |
| taskasyncwait | 正しい await パターンの比較用サンプル | なし | 約0.5秒 |

詳細な使用方法とサンプルについては、[API リファレンス](api-reference.md)を参照してください。

---

## トグル型シナリオ

ベースURL: `/api/ScenarioToggle`

これらのシナリオは、バックグラウンドタスクとして長時間実行され、開始/停止を個別に制御できます。継続的な負荷テストや長期的な障害注入に最適です。

### シナリオ一覧表

| シナリオ | Start / Stop | 主な設定フィールド | 用途 |
| --- | --- | --- | --- |
| ProbabilisticFailure | /probabilistic-failure/start / stop | durationMinutes, requestsPerSecond, failurePercentage | 確率的な障害注入 |
| CpuSpike | /cpu-spike/start / stop | durationMinutes, intervalSeconds, triggerPercentage, spikeSeconds | 不定期なCPUスパイク |
| MemoryLeak | /memory-leak/start / stop | durationMinutes, intervalSeconds, triggerPercentage, memoryMegabytes, holdSeconds | 徐々に進行するメモリリーク |
| ProbabilisticLatency | /probabilistic-latency/start / stop | durationMinutes, requestsPerSecond, triggerPercentage, delayMilliseconds | 確率的なレイテンシ注入 |

### UI（Web インターフェース）

`/Home/ToggleScenarios` では、各トグル型シナリオをGUIで制御できます。

**機能:**
- 各シナリオの設定フィールドを入力
- トグルスイッチでオン/オフ
- ステータスが5秒ごとに自動更新
- 終了予定時刻や最後のメッセージを表示

**使い方:**
1. ブラウザで http://localhost:5000/Home/ToggleScenarios を開く
2. シナリオのパラメータを入力
3. トグルスイッチをONにする
4. ステータスが「Running」になることを確認
5. 必要に応じてトグルスイッチをOFFにして停止

---

## 実行時の注意事項

### 環境に関する注意

⚠️ **重要:**
- **すべて自己責任で使用してください**
- **テスト/検証環境でのみ実行してください**
- **本番環境では絶対に実行しないでください**

### リソース管理

1. **段階的に負荷を上げる**
   - 値を上げすぎるとホストOSやコンテナのリソースを使い切ります
   - 段階的に増やして監視メトリックを確認してください

2. **監視メトリクスを確認**
   - CPU使用率
   - メモリ使用量
   - ディスクI/O
   - ネットワーク帯域

3. **リソース制限を設定**
   ```bash
   # Dockerの場合
   docker run --memory="2g" --cpus="2" sre-agent-tester
   ```

### クールダウン時間

シナリオ停止後、ガベージコレクションが完了するまで数秒〜数十秒かかる場合があります。十分にクールダウン時間を設けてください。

- **CPU スパイク**: 1-5秒
- **メモリスパイク**: 5-30秒（GC完了まで）
- **メモリリーク**: 10-60秒（複数のGCサイクル）

---

## ユースケース別推奨シナリオ

### ケース1: APMツールの検証

**目標:** アプリケーション監視ツールが正しく動作することを確認

**推奨シナリオ:**
```bash
# CPU監視
curl http://localhost:5000/api/DiagScenario/highcpu/10000

# メモリ監視
curl http://localhost:5000/api/DiagScenario/memspike/120

# エラー率監視（トグル型）
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-failure/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":15,"requestsPerSecond":50,"failurePercentage":10}'
```

### ケース2: SLO/SLI計算の検証

**目標:** SLOダッシュボードやエラーバジェット計算が正確か確認

**推奨シナリオ:**
```bash
# 既知のエラー率でテスト（期待: 5%のエラー率、95% SLI）
curl "http://localhost:5000/api/DiagScenario/probabilisticload/300/100/5"
```

### ケース3: アラートルールのテスト

**目標:** 閾値ベースのアラートが正しく発火・解消されることを確認

**推奨シナリオ:**
```bash
# CPUアラートのテスト（アラートが発火→解消を繰り返すはず）
curl -X POST http://localhost:5000/api/ScenarioToggle/cpu-spike/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":30,"intervalSeconds":60,"triggerPercentage":80,"spikeSeconds":15}'
```

### ケース4: デバッグツールのトレーニング

**目標:** 開発者がデバッグツールの使い方を学ぶ

**推奨シナリオ:**
```bash
# デッドロック検出の練習（dotnet-dumpやjstackでスレッドダンプを取得）
curl http://localhost:5000/api/DiagScenario/deadlock

# メモリリーク検出の練習（dotnet-gcdumpでヒープダンプを取得）
curl http://localhost:5000/api/DiagScenario/memleak/5120
curl http://localhost:5000/api/DiagScenario/memleak/5120
```

---

## 次のステップ

- APIの詳細: [api-reference.md](api-reference.md)
- アーキテクチャ理解: [architecture.md](architecture.md)
- カスタマイズ方法: [development-guide.md](development-guide.md)

実際にシナリオを実行して、監視ツールやSREプラクティスの検証を始めましょう！
