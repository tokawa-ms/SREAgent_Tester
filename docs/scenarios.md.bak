# シナリオ一覧

## 即時実行 API (`/api/DiagScenario`)
| エンドポイント | 説明 | 主なパラメーター |
| --- | --- | --- |
| `deadlock` | 相互にロックを取り合うスレッドを大量生成してハングを再現します。 | なし |
| `highcpu/{milliseconds}` | 指定時間 CPU をビジーループで専有します。 | `milliseconds` (100-30000) |
| `memspike/{seconds}` | 5 秒間隔でメモリを大量確保→解放する波形を繰り返します。 | `seconds` (1-1800) |
| `memleak/{kilobytes}` | 指定サイズのオブジェクトを保持し続け意図的にメモリをリークさせます。 | `kilobytes` (1-10240) |
| `exception` | 即時に例外を投げます。 | なし |
| `exceptionburst/{durationSeconds}/{exceptionsPerSecond}` | 高頻度で例外を投げ続け、ログ/メトリックの飽和を再現します。 | `durationSeconds` (1-1800), `exceptionsPerSecond` (1-1000) |
| `probabilisticload/{durationSeconds}/{requestsPerSecond}/{exceptionPercentage}` | 指定期間、疑似バックエンド呼び出しを行い確率的に例外を発生させます。 | `durationSeconds`, `requestsPerSecond`, `exceptionPercentage` |
| `taskwait` / `tasksleepwait` | 非推奨同期待ちパターンでスレッドプール枯渇を再現します。 | なし |
| `taskasyncwait` | 正しい `await` パターンの比較用サンプルです。 | なし |

## トグル型シナリオ (`/api/ScenarioToggle`)
| シナリオ | Start / Stop | 主な設定フィールド | 内容 |
| --- | --- | --- | --- |
| ProbabilisticFailure | `/probabilistic-failure/start` / `stop` | `durationMinutes`, `requestsPerSecond`, `failurePercentage` | 秒間リクエストを送り、指定確率で例外を投げます。 |
| CpuSpike | `/cpu-spike/start` / `stop` | `durationMinutes`, `intervalSeconds`, `triggerPercentage`, `spikeSeconds` | 抽選で CPU ビジーループを挟みます。 |
| MemoryLeak | `/memory-leak/start` / `stop` | `durationMinutes`, `intervalSeconds`, `triggerPercentage`, `memoryMegabytes`, `holdSeconds` | 一定間隔で大きな配列を確保し保持します。 |
| ProbabilisticLatency | `/probabilistic-latency/start` / `stop` | `durationMinutes`, `requestsPerSecond`, `triggerPercentage`, `delayMilliseconds` | リクエストの一部に遅延を注入します。 |

UI (`/Home/ToggleScenarios`) では各フィールドを入力後、トグルをオン/オフするだけで開始・停止できます。ステータスは 5 秒毎に自動更新され、終了予定時刻や最後のメッセージが表示されます。

## 実行時の注意
- すべて自己責任で使用し、テスト/検証環境でのみ実行してください。
- 値を上げすぎるとホスト OS やコンテナのリソースを使い切るため、段階的に増やして監視メトリックを確認してください。
- シナリオ停止後もガベージコレクションが完了するまで数秒～数十秒かかる場合があります。十分にクールダウン時間を設けてください。
