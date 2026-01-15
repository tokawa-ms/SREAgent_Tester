using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DiagnosticScenarios.Models
{
    /// <summary>
    /// シナリオの種類を表す列挙型
    /// 各シナリオは異なる診断テストパターンを実行します
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ScenarioToggleType
    {
        /// <summary>確率的障害シナリオ - ランダムに例外を発生させます</summary>
        ProbabilisticFailure,
        /// <summary>CPUスパイクシナリオ - 定期的にCPU使用率を急上昇させます</summary>
        CpuSpike,
        /// <summary>メモリリークシナリオ - メモリを徐々に確保して解放しません</summary>
        MemoryLeak,
        /// <summary>確率的レイテンシシナリオ - ランダムにレスポンスを遅延させます</summary>
        ProbabilisticLatency
    }

    /// <summary>
    /// シナリオの実行状態を表すモデルクラス
    /// 現在の実行状況、設定、終了予定時刻などの情報を含みます
    /// </summary>
    public sealed class ScenarioStatus
    {
        /// <summary>シナリオの種類</summary>
        public ScenarioToggleType Scenario { get; init; }
        
        /// <summary>シナリオが現在実行中かどうか</summary>
        public bool IsActive { get; init; }
        
        /// <summary>シナリオの終了予定時刻（UTC）。実行中でない場合はnull</summary>
        public DateTimeOffset? EndsAtUtc { get; init; }
        
        /// <summary>最後のステータスメッセージ（例: "Running", "Scenario finished"）</summary>
        public string? LastMessage { get; init; }
        
        /// <summary>現在アクティブなシナリオの設定のスナップショット。実行中でない場合はnull</summary>
        public IReadOnlyDictionary<string, object?>? ActiveConfig { get; init; }
    }

    /// <summary>
    /// 確率的障害シナリオの設定
    /// 指定された頻度でリクエストを送信し、一定確率で例外を発生させます
    /// </summary>
    public sealed class ProbabilisticFailureRequest
    {
        /// <summary>シナリオの実行時間（分）。1～180の範囲で指定</summary>
        [Range(1, 180)]
        public int DurationMinutes { get; set; }

        /// <summary>1秒あたりのリクエスト数。1～1000の範囲で指定</summary>
        [Range(1, 1000)]
        public int RequestsPerSecond { get; set; }

        /// <summary>失敗率（パーセンテージ）。0～100の範囲で指定</summary>
        [Range(0, 100)]
        public int FailurePercentage { get; set; }
    }

    /// <summary>
    /// CPUスパイクシナリオの設定
    /// 定期的にCPUを高負荷状態にして、CPU使用率の急上昇をシミュレートします
    /// </summary>
    public sealed class CpuSpikeRequest
    {
        /// <summary>シナリオの実行時間（分）。1～180の範囲で指定</summary>
        [Range(1, 180)]
        public int DurationMinutes { get; set; }

        /// <summary>スパイクのチェック間隔（秒）。1～300の範囲で指定</summary>
        [Range(1, 300)]
        public int IntervalSeconds { get; set; }

        /// <summary>スパイクを発生させる確率（パーセンテージ）。0～100の範囲で指定</summary>
        [Range(0, 100)]
        public int TriggerPercentage { get; set; }

        /// <summary>1回のスパイク持続時間（秒）。1～30の範囲で指定</summary>
        [Range(1, 30)]
        public int SpikeSeconds { get; set; }
    }

    /// <summary>
    /// メモリリークシナリオの設定
    /// 定期的に大きなメモリブロックを確保し、一定時間保持してメモリリークをシミュレートします
    /// </summary>
    public sealed class MemoryLeakRequest
    {
        /// <summary>シナリオの実行時間（分）。1～180の範囲で指定</summary>
        [Range(1, 180)]
        public int DurationMinutes { get; set; }

        /// <summary>メモリ確保のチェック間隔（秒）。1～300の範囲で指定</summary>
        [Range(1, 300)]
        public int IntervalSeconds { get; set; }

        /// <summary>メモリを確保する確率（パーセンテージ）。0～100の範囲で指定</summary>
        [Range(0, 100)]
        public int TriggerPercentage { get; set; }

        /// <summary>1回に確保するメモリサイズ（MB）。1～1024の範囲で指定</summary>
        [Range(1, 1024)]
        public int MemoryMegabytes { get; set; }

        /// <summary>確保したメモリを保持する時間（秒）。1～60の範囲で指定</summary>
        [Range(1, 60)]
        public int HoldSeconds { get; set; }
    }

    /// <summary>
    /// 確率的レイテンシシナリオの設定
    /// 指定された頻度でリクエストを処理し、一定確率で遅延を注入します
    /// </summary>
    public sealed class ProbabilisticLatencyRequest
    {
        /// <summary>シナリオの実行時間（分）。1～180の範囲で指定</summary>
        [Range(1, 180)]
        public int DurationMinutes { get; set; }

        /// <summary>1秒あたりのリクエスト数。1～1000の範囲で指定</summary>
        [Range(1, 1000)]
        public int RequestsPerSecond { get; set; }

        /// <summary>遅延を発生させる確率（パーセンテージ）。0～100の範囲で指定</summary>
        [Range(0, 100)]
        public int TriggerPercentage { get; set; }

        /// <summary>遅延時間（ミリ秒）。1～10000の範囲で指定</summary>
        [Range(1, 10000)]
        public int DelayMilliseconds { get; set; }
    }
}
