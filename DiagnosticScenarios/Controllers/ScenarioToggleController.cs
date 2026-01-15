using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticScenarios.Models;
using DiagnosticScenarios.Services;
using Microsoft.AspNetCore.Mvc;

namespace DiagnosticScenarios.Controllers
{
    /// <summary>
    /// バックグラウンドで実行される診断シナリオの開始/停止を制御するAPIコントローラー
    /// 長時間実行される負荷テストや障害シミュレーションを管理します
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ScenarioToggleController : ControllerBase
    {
        private readonly IScenarioToggleService _service;

        /// <summary>
        /// ScenarioToggleControllerのコンストラクタ
        /// </summary>
        /// <param name="service">シナリオ実行を管理するサービス（DIコンテナから注入）</param>
        public ScenarioToggleController(IScenarioToggleService service)
        {
            _service = service;
        }

        /// <summary>
        /// 全シナリオの現在の状態を取得します
        /// </summary>
        /// <returns>全シナリオの状態リスト</returns>
        [HttpGet("status")]
        public ActionResult<IEnumerable<ScenarioStatus>> GetStatuses()
        {
            return Ok(_service.GetStatuses());
        }

        /// <summary>
        /// 確率的障害シナリオを開始します
        /// 指定された頻度でリクエストを送信し、一定確率で例外を発生させます
        /// </summary>
        /// <param name="request">シナリオの設定（実行時間、リクエスト頻度、失敗率）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>開始したシナリオの状態</returns>
        [HttpPost("probabilistic-failure/start")]
        public Task<ActionResult<ScenarioStatus>> StartProbabilisticFailure(
            [FromBody] ProbabilisticFailureRequest request,
            CancellationToken cancellationToken) =>
            ExecuteStartAsync(() => _service.StartProbabilisticFailureAsync(request, cancellationToken));

        /// <summary>
        /// 確率的障害シナリオを停止します
        /// </summary>
        /// <returns>停止したシナリオの状態</returns>
        [HttpPost("probabilistic-failure/stop")]
        public ActionResult<ScenarioStatus> StopProbabilisticFailure()
        {
            return Ok(_service.StopScenario(ScenarioToggleType.ProbabilisticFailure));
        }

        /// <summary>
        /// CPUスパイクシナリオを開始します
        /// 定期的にCPUを高負荷状態にして、パフォーマンス監視ツールのテストに使用します
        /// </summary>
        /// <param name="request">シナリオの設定（実行時間、間隔、発生確率、スパイク持続時間）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>開始したシナリオの状態</returns>
        [HttpPost("cpu-spike/start")]
        public Task<ActionResult<ScenarioStatus>> StartCpuSpike(
            [FromBody] CpuSpikeRequest request,
            CancellationToken cancellationToken) =>
            ExecuteStartAsync(() => _service.StartCpuSpikeAsync(request, cancellationToken));

        /// <summary>
        /// CPUスパイクシナリオを停止します
        /// </summary>
        /// <returns>停止したシナリオの状態</returns>
        [HttpPost("cpu-spike/stop")]
        public ActionResult<ScenarioStatus> StopCpuSpike()
        {
            return Ok(_service.StopScenario(ScenarioToggleType.CpuSpike));
        }

        /// <summary>
        /// メモリリークシナリオを開始します
        /// 定期的にメモリを確保し、メモリ管理の問題を検出するためのテストに使用します
        /// </summary>
        /// <param name="request">シナリオの設定（実行時間、間隔、発生確率、メモリサイズ、保持時間）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>開始したシナリオの状態</returns>
        [HttpPost("memory-leak/start")]
        public Task<ActionResult<ScenarioStatus>> StartMemoryLeak(
            [FromBody] MemoryLeakRequest request,
            CancellationToken cancellationToken) =>
            ExecuteStartAsync(() => _service.StartMemoryLeakAsync(request, cancellationToken));

        /// <summary>
        /// メモリリークシナリオを停止します
        /// 停止後、確保されたメモリは自動的に解放されます
        /// </summary>
        /// <returns>停止したシナリオの状態</returns>
        [HttpPost("memory-leak/stop")]
        public ActionResult<ScenarioStatus> StopMemoryLeak()
        {
            return Ok(_service.StopScenario(ScenarioToggleType.MemoryLeak));
        }

        /// <summary>
        /// 確率的レイテンシシナリオを開始します
        /// リクエストの一部に遅延を注入して、レイテンシ監視のテストに使用します
        /// </summary>
        /// <param name="request">シナリオの設定（実行時間、リクエスト頻度、発生確率、遅延時間）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>開始したシナリオの状態</returns>
        [HttpPost("probabilistic-latency/start")]
        public Task<ActionResult<ScenarioStatus>> StartProbabilisticLatency(
            [FromBody] ProbabilisticLatencyRequest request,
            CancellationToken cancellationToken) =>
            ExecuteStartAsync(() => _service.StartProbabilisticLatencyAsync(request, cancellationToken));

        /// <summary>
        /// 確率的レイテンシシナリオを停止します
        /// </summary>
        /// <returns>停止したシナリオの状態</returns>
        [HttpPost("probabilistic-latency/stop")]
        public ActionResult<ScenarioStatus> StopProbabilisticLatency()
        {
            return Ok(_service.StopScenario(ScenarioToggleType.ProbabilisticLatency));
        }

        /// <summary>
        /// シナリオ開始処理を共通化したヘルパーメソッド
        /// 例外処理とHTTPステータスコードのマッピングを行います
        /// </summary>
        /// <param name="startAction">シナリオ開始処理を実行するデリゲート</param>
        /// <returns>
        /// 成功時: 200 OK とシナリオの状態
        /// 既に実行中の場合: 409 Conflict
        /// </returns>
        private async Task<ActionResult<ScenarioStatus>> ExecuteStartAsync(Func<Task<ScenarioStatus>> startAction)
        {
            try
            {
                var status = await startAction();
                return Ok(status);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }
    }
}
