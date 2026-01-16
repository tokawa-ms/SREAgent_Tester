using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DiagnosticScenarios.Controllers
{
    /// <summary>
    /// 外部負荷ツール（JMeterなど）向けの直接負荷テストAPIコントローラー
    /// 各エンドポイントは特定の負荷パターンをシミュレートします
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class DirectTestController : ControllerBase
    {
        private readonly ILogger<DirectTestController> _logger;

        /// <summary>
        /// DirectTestControllerのコンストラクタ
        /// </summary>
        /// <param name="logger">ロガー（DIコンテナから注入）</param>
        public DirectTestController(ILogger<DirectTestController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// ランダムな応答遅延を発生させるエンドポイント
        /// 指定されたミリ秒以下のランダムな遅延を付加してレスポンスを返します
        /// </summary>
        /// <param name="maxLatencyInMilliSeconds">最大遅延時間（ミリ秒）。0～30000の範囲で指定</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功メッセージと実際の遅延時間</returns>
        /// <response code="200">成功。実際の遅延時間を含むメッセージを返します</response>
        /// <response code="400">パラメータが範囲外の場合</response>
        /// <remarks>
        /// 負荷テストツールでレイテンシのばらつきをシミュレートする際に使用します。
        /// 実際の遅延時間は0からmaxLatencyInMilliSecondsの間でランダムに決定されます。
        /// 
        /// 使用例:
        /// 
        ///     GET /api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000
        ///     
        /// </remarks>
        [HttpGet]
        [Route("RandomLatency")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<string>> RandomLatency(
            [FromQuery] int maxLatencyInMilliSeconds,
            CancellationToken cancellationToken)
        {
            // パラメータ検証
            if (maxLatencyInMilliSeconds < 0 || maxLatencyInMilliSeconds > 30000)
            {
                return BadRequest("maxLatencyInMilliSeconds must be between 0 and 30000.");
            }

            // ランダムな遅延時間を生成
            var actualLatency = Random.Shared.Next(0, maxLatencyInMilliSeconds + 1);

            // 遅延を発生させる
            await Task.Delay(actualLatency, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "RandomLatency executed with max={MaxLatency}ms, actual={ActualLatency}ms",
                maxLatencyInMilliSeconds,
                actualLatency);

            return Ok($"success:randomlatency (max={maxLatencyInMilliSeconds}ms, actual={actualLatency}ms)");
        }

        /// <summary>
        /// ランダムに例外を発生させるエンドポイント
        /// 指定された確率で例外をスローし500エラーを返します。それ以外は200を返します
        /// </summary>
        /// <param name="exceptionPercentage">例外発生確率（パーセント）。0～100の範囲で指定</param>
        /// <returns>成功メッセージまたは500エラー</returns>
        /// <response code="200">成功。例外が発生しなかった場合</response>
        /// <response code="400">パラメータが範囲外の場合</response>
        /// <response code="500">例外が発生した場合</response>
        /// <remarks>
        /// 負荷テストツールでエラー率をシミュレートする際に使用します。
        /// 例外は意図的に発生させられ、500 Internal Server Errorとして返されます。
        /// 
        /// 使用例:
        /// 
        ///     GET /api/DirectTest/RandomException?exceptionPercentage=10
        ///     
        /// 上記の例では、10%の確率で500エラーが返されます。
        /// </remarks>
        [HttpGet]
        [Route("RandomException")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public ActionResult<string> RandomException([FromQuery] int exceptionPercentage)
        {
            // パラメータ検証
            if (exceptionPercentage < 0 || exceptionPercentage > 100)
            {
                return BadRequest("exceptionPercentage must be between 0 and 100.");
            }

            // 確率判定
            var randomValue = Random.Shared.Next(0, 100);

            if (randomValue < exceptionPercentage)
            {
                // 例外を発生させる
                _logger.LogWarning(
                    "RandomException triggered (percentage={Percentage}%, roll={Roll})",
                    exceptionPercentage,
                    randomValue);

                throw new InvalidOperationException(
                    $"Random exception triggered (exceptionPercentage={exceptionPercentage}%)");
            }

            _logger.LogInformation(
                "RandomException succeeded (percentage={Percentage}%, roll={Roll})",
                exceptionPercentage,
                randomValue);

            return Ok($"success:randomexception (exceptionPercentage={exceptionPercentage}%, no exception)");
        }

        /// <summary>
        /// 指定サイズのメモリを指定時間保持するエンドポイント
        /// 指定された秒数の間、指定されたサイズのメモリを確保し続けた後、200を返します
        /// </summary>
        /// <param name="secondsToKeepMem">メモリを保持する秒数。1～300の範囲で指定</param>
        /// <param name="keepMemSize">確保するメモリサイズ（MB）。1～2048の範囲で指定</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功メッセージとメモリ保持時間</returns>
        /// <response code="200">成功。メモリを指定時間保持した後に返します</response>
        /// <response code="400">パラメータが範囲外の場合</response>
        /// <remarks>
        /// 負荷テストツールでメモリ使用量の増加をシミュレートする際に使用します。
        /// 確保されたメモリは指定時間が経過した後、自動的に解放されます。
        /// 
        /// 使用例:
        /// 
        ///     GET /api/DirectTest/HighMem?secondsToKeepMem=10&amp;keepMemSize=100
        ///     
        /// 上記の例では、100MBのメモリを10秒間保持します。
        /// </remarks>
        [HttpGet]
        [Route("HighMem")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<string>> HighMem(
            [FromQuery] int secondsToKeepMem,
            [FromQuery] int keepMemSize,
            CancellationToken cancellationToken)
        {
            // パラメータ検証
            if (secondsToKeepMem < 1 || secondsToKeepMem > 300)
            {
                return BadRequest("secondsToKeepMem must be between 1 and 300.");
            }

            if (keepMemSize < 1 || keepMemSize > 2048)
            {
                return BadRequest("keepMemSize must be between 1 and 2048.");
            }

            _logger.LogInformation(
                "HighMem started: allocating {MemSize}MB for {Seconds} seconds",
                keepMemSize,
                secondsToKeepMem);

            // メモリを確保（1MB = 1,048,576 バイト ≈ 1,000,000 バイト）
            // byte配列を使用してメモリを確保
            var memoryHolder = new byte[keepMemSize * 1024 * 1024];

            // メモリを初期化してコンパイラによる最適化を防ぐ
            for (int i = 0; i < memoryHolder.Length; i += 4096)
            {
                memoryHolder[i] = 1;
            }

            // 指定時間メモリを保持
            await Task.Delay(secondsToKeepMem * 1000, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "HighMem completed: released {MemSize}MB after {Seconds} seconds",
                keepMemSize,
                secondsToKeepMem);

            // メモリを明示的に解放（スコープを抜けることで自動的に解放される）
            return Ok($"success:highmem (kept {keepMemSize}MB for {secondsToKeepMem} seconds)");
        }

        /// <summary>
        /// CPUを高負荷状態にするエンドポイント
        /// 指定されたミリ秒の間、CPUビジーループを実行して高負荷状態を作り出します
        /// </summary>
        /// <param name="millisecondsToKeepHighCPU">CPU高負荷を維持するミリ秒数。100～60000の範囲で指定</param>
        /// <returns>成功メッセージと実行時間</returns>
        /// <response code="200">成功。CPU高負荷状態を維持した後に返します</response>
        /// <response code="400">パラメータが範囲外の場合</response>
        /// <remarks>
        /// 負荷テストツールでCPU使用率の増加をシミュレートする際に使用します。
        /// ビジーループによってCPUを占有し、指定時間の間高負荷状態を維持します。
        /// 
        /// 使用例:
        /// 
        ///     GET /api/DirectTest/HighCPU?millisecondsToKeepHighCPU=5000
        ///     
        /// 上記の例では、5秒間CPUを高負荷状態にします。
        /// </remarks>
        [HttpGet]
        [Route("HighCPU")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public ActionResult<string> HighCPU([FromQuery] int millisecondsToKeepHighCPU)
        {
            // パラメータ検証
            if (millisecondsToKeepHighCPU < 100 || millisecondsToKeepHighCPU > 60000)
            {
                return BadRequest("millisecondsToKeepHighCPU must be between 100 and 60000.");
            }

            _logger.LogInformation(
                "HighCPU started: running busy loop for {Milliseconds}ms",
                millisecondsToKeepHighCPU);

            var stopwatch = Stopwatch.StartNew();

            // CPUビジーループを実行
            while (stopwatch.ElapsedMilliseconds < millisecondsToKeepHighCPU)
            {
                // CPU集約的な計算を実行
                // 空のループだと最適化される可能性があるため、簡単な計算を入れる
                var _ = Math.Sqrt(Random.Shared.NextDouble());
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "HighCPU completed: ran for {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);

            return Ok($"success:highcpu (ran for {stopwatch.ElapsedMilliseconds}ms)");
        }
    }
}
