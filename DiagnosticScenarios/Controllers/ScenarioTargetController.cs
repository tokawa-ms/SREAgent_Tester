using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticScenarios.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DiagnosticScenarios.Controllers
{
    /// <summary>
    /// トグルシナリオ用のHTTPバックエンドとして負荷や障害を注入するターゲットエンドポイント
    /// ScenarioToggleServiceから呼び出され、実際のシナリオ処理を実行します
    /// </summary>
    /// <remarks>
    /// このコントローラーは、ScenarioToggleControllerが開始したバックグラウンドシナリオの
    /// 実行先として機能します。確率的障害、レイテンシ、CPU負荷、メモリリークなどを注入します。
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    public sealed class ScenarioTargetController : ControllerBase
    {
        private static readonly ConcurrentDictionary<Guid, byte[]> MemoryLeases = new();
        private readonly ILogger<ScenarioTargetController> _logger;

        /// <summary>
        /// ScenarioTargetControllerのコンストラクタ
        /// </summary>
        /// <param name="logger">ロガー（DIコンテナから注入）</param>
        public ScenarioTargetController(ILogger<ScenarioTargetController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 確率的障害エンドポイント
        /// 指定された確率で500エラーを返します
        /// </summary>
        /// <param name="request">障害発生率を含むリクエスト設定</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功時: 200 OK、障害発生時: 500 Internal Server Error</returns>
        [HttpPost("probabilistic-failure")]
        public async Task<IActionResult> ProbabilisticFailure([FromBody] ProbabilisticFailureRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);

            if (ShouldTrigger(request.FailurePercentage))
            {
                try
                {
                    throw new InvalidOperationException("Simulated probabilistic failure.");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Scenario target probabilistic failure triggered.");
                }

                return StatusCode(StatusCodes.Status500InternalServerError, "Simulated probabilistic failure.");
            }

            return Ok();
        }

        /// <summary>
        /// 確率的レイテンシエンドポイント
        /// 指定された確率でレスポンスに遅延を注入します
        /// </summary>
        /// <param name="request">遅延発生率と遅延時間を含むリクエスト設定</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功メッセージ（遅延後）</returns>
        [HttpPost("probabilistic-latency")]
        public async Task<IActionResult> ProbabilisticLatency([FromBody] ProbabilisticLatencyRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);

            if (ShouldTrigger(request.TriggerPercentage))
            {
                var delay = TimeSpan.FromMilliseconds(request.DelayMilliseconds);
                _logger.LogInformation("Injecting latency of {Delay} for probabilistic latency endpoint.", delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            return Ok();
        }

        /// <summary>
        /// CPUスパイクエンドポイント
        /// 指定された確率でCPUビジーループを実行します
        /// </summary>
        /// <param name="request">スパイク発生率と持続時間を含むリクエスト設定</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>スパイクが発生したかどうかを示すレスポンス</returns>
        [HttpPost("cpu-spike")]
        public IActionResult CpuSpike([FromBody] CpuSpikeRequest request, CancellationToken cancellationToken)
        {
            if (!ShouldTrigger(request.TriggerPercentage))
            {
                return Ok(new { triggered = false });
            }

            _logger.LogInformation("CPU spike triggered for {Seconds} seconds.", request.SpikeSeconds);
            try
            {
                BusyWait(TimeSpan.FromSeconds(request.SpikeSeconds), cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "CPU spike request was canceled by the client.");
                return StatusCode(499, new { triggered = false, canceled = true });
            }

            return Ok(new { triggered = true });
        }

        /// <summary>
        /// メモリリークエンドポイント
        /// 指定された確率でメモリを確保し、一定時間保持します
        /// </summary>
        /// <param name="request">メモリ確保率、サイズ、保持時間を含むリクエスト設定</param>
        /// <returns>メモリが確保されたかどうかとリースIDを含むレスポンス</returns>
        [HttpPost("memory-leak")]
        public IActionResult MemoryLeak([FromBody] MemoryLeakRequest request)
        {
            if (!ShouldTrigger(request.TriggerPercentage))
            {
                return Ok(new { triggered = false });
            }

            var leaseId = Guid.NewGuid();
            var allocation = new byte[request.MemoryMegabytes * 1024 * 1024];
            MemoryLeases[leaseId] = allocation;

            _logger.LogInformation(
                "Allocated {Megabytes} MB for {Seconds} seconds (lease {LeaseId}).",
                request.MemoryMegabytes,
                request.HoldSeconds,
                leaseId);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(request.HoldSeconds), CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    MemoryLeases.TryRemove(leaseId, out _);
                }
            });

            return Ok(new { triggered = true, leaseId });
        }

        /// <summary>
        /// メモリリーク解放エンドポイント
        /// 確保された全てのメモリリースを解放します
        /// </summary>
        /// <returns>成功メッセージ</returns>
        /// <remarks>
        /// メモリリークシナリオ終了時に自動的に呼び出されます
        /// </remarks>
        [HttpPost("memory-leak/release")]
        public IActionResult ReleaseAllMemory()
        {
            foreach (var key in MemoryLeases.Keys)
            {
                MemoryLeases.TryRemove(key, out _);
            }

            _logger.LogInformation("Released all memory leak leases via reset endpoint.");
            return Ok();
        }

        /// <summary>
        /// 指定された確率で処理を実行するかどうかを判定します
        /// </summary>
        /// <param name="percentage">実行確率（0～100）</param>
        /// <returns>実行すべき場合はtrue、そうでない場合はfalse</returns>
        private static bool ShouldTrigger(int percentage)
        {
            return percentage > 0 && Random.Shared.Next(0, 100) < percentage;
        }

        /// <summary>
        /// 指定された時間、CPUビジーループを実行します
        /// </summary>
        /// <param name="duration">ビジーループの実行時間</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <remarks>
        /// SpinWaitを使用してCPU使用率を最大化します
        /// </remarks>
        private static void BusyWait(TimeSpan duration, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var spinner = new SpinWait();
            while (watch.Elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                spinner.SpinOnce();
            }
        }
    }
}
