using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticScenarios.Models;
using Microsoft.Extensions.Logging;

namespace DiagnosticScenarios.Services
{
    /// <summary>
    /// バックグラウンドで実行される診断シナリオの管理を行うサービスのインターフェース
    /// 各シナリオの開始、停止、状態取得の機能を提供します
    /// </summary>
    public interface IScenarioToggleService
    {
        /// <summary>
        /// 全シナリオの現在の状態を取得します
        /// </summary>
        /// <returns>全シナリオの状態のコレクション</returns>
        IReadOnlyCollection<ScenarioStatus> GetStatuses();
        
        /// <summary>
        /// 指定されたシナリオの現在の状態を取得します
        /// </summary>
        /// <param name="scenario">状態を取得するシナリオの種類</param>
        /// <returns>指定されたシナリオの状態</returns>
        ScenarioStatus GetStatus(ScenarioToggleType scenario);
        
        /// <summary>
        /// 確率的障害シナリオを開始します
        /// バックグラウンドタスクとして実行され、指定された時間まで継続します
        /// </summary>
        /// <param name="request">シナリオの設定パラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>開始されたシナリオの状態</returns>
        /// <exception cref="InvalidOperationException">シナリオが既に実行中の場合</exception>
        Task<ScenarioStatus> StartProbabilisticFailureAsync(ProbabilisticFailureRequest request, CancellationToken cancellationToken);
        
        /// <summary>
        /// CPUスパイクシナリオを開始します
        /// バックグラウンドタスクとして実行され、指定された時間まで継続します
        /// </summary>
        /// <param name="request">シナリオの設定パラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>開始されたシナリオの状態</returns>
        /// <exception cref="InvalidOperationException">シナリオが既に実行中の場合</exception>
        Task<ScenarioStatus> StartCpuSpikeAsync(CpuSpikeRequest request, CancellationToken cancellationToken);
        
        /// <summary>
        /// メモリリークシナリオを開始します
        /// バックグラウンドタスクとして実行され、指定された時間まで継続します
        /// </summary>
        /// <param name="request">シナリオの設定パラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>開始されたシナリオの状態</returns>
        /// <exception cref="InvalidOperationException">シナリオが既に実行中の場合</exception>
        Task<ScenarioStatus> StartMemoryLeakAsync(MemoryLeakRequest request, CancellationToken cancellationToken);
        
        /// <summary>
        /// 確率的レイテンシシナリオを開始します
        /// バックグラウンドタスクとして実行され、指定された時間まで継続します
        /// </summary>
        /// <param name="request">シナリオの設定パラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>開始されたシナリオの状態</returns>
        /// <exception cref="InvalidOperationException">シナリオが既に実行中の場合</exception>
        Task<ScenarioStatus> StartProbabilisticLatencyAsync(ProbabilisticLatencyRequest request, CancellationToken cancellationToken);
        
        /// <summary>
        /// 指定されたシナリオを停止します
        /// 実行中でない場合は何も行いません
        /// </summary>
        /// <param name="scenario">停止するシナリオの種類</param>
        /// <returns>停止後のシナリオの状態</returns>
        ScenarioStatus StopScenario(ScenarioToggleType scenario);
    }

    /// <summary>
    /// IScenarioToggleServiceの実装クラス
    /// 複数のシナリオを並行して実行し、スレッドセーフに状態を管理します
    /// </summary>
    internal sealed class ScenarioToggleService : IScenarioToggleService
    {
        private readonly ILogger<ScenarioToggleService> _logger;
        // シナリオごとの状態を管理するスレッドセーフなディクショナリ
        private readonly ConcurrentDictionary<ScenarioToggleType, ScenarioState> _state;
        // メモリリークシナリオで確保したメモリブロックを管理
        private readonly ConcurrentDictionary<Guid, byte[]> _memoryLeases = new();

        /// <summary>
        /// ScenarioToggleServiceのコンストラクタ
        /// 全シナリオの初期状態を構築します
        /// </summary>
        /// <param name="logger">ロガー（DIコンテナから注入）</param>
        public ScenarioToggleService(ILogger<ScenarioToggleService> logger)
        {
            _logger = logger;
            _state = new ConcurrentDictionary<ScenarioToggleType, ScenarioState>(
                Enum.GetValues(typeof(ScenarioToggleType))
                    .Cast<ScenarioToggleType>()
                    .Select(t => new KeyValuePair<ScenarioToggleType, ScenarioState>(t, new ScenarioState(t))));
        }

        public IReadOnlyCollection<ScenarioStatus> GetStatuses()
        {
            return _state.Keys.Select(GetStatus).ToArray();
        }

        public ScenarioStatus GetStatus(ScenarioToggleType scenario)
        {
            return _state.TryGetValue(scenario, out var state)
                ? state.ToStatus()
                : new ScenarioStatus
                {
                    Scenario = scenario,
                    IsActive = false
                };
        }

        public Task<ScenarioStatus> StartProbabilisticFailureAsync(ProbabilisticFailureRequest request, CancellationToken cancellationToken)
        {
            return StartScenarioAsync(ScenarioToggleType.ProbabilisticFailure, request.DurationMinutes, request, cancellationToken, token => RunProbabilisticFailureAsync(request, token));
        }

        public Task<ScenarioStatus> StartCpuSpikeAsync(CpuSpikeRequest request, CancellationToken cancellationToken)
        {
            return StartScenarioAsync(ScenarioToggleType.CpuSpike, request.DurationMinutes, request, cancellationToken, token => RunCpuSpikeAsync(request, token));
        }

        public Task<ScenarioStatus> StartMemoryLeakAsync(MemoryLeakRequest request, CancellationToken cancellationToken)
        {
            return StartScenarioAsync(ScenarioToggleType.MemoryLeak, request.DurationMinutes, request, cancellationToken, token => RunMemoryLeakAsync(request, token));
        }

        public Task<ScenarioStatus> StartProbabilisticLatencyAsync(ProbabilisticLatencyRequest request, CancellationToken cancellationToken)
        {
            return StartScenarioAsync(ScenarioToggleType.ProbabilisticLatency, request.DurationMinutes, request, cancellationToken, token => RunProbabilisticLatencyAsync(request, token));
        }

        public ScenarioStatus StopScenario(ScenarioToggleType scenario)
        {
            if (_state.TryGetValue(scenario, out var state))
            {
                state.Cancel();
                return state.ToStatus();
            }

            return new ScenarioStatus
            {
                Scenario = scenario,
                IsActive = false
            };
        }

        /// <summary>
        /// シナリオを開始する共通ロジック
        /// スレッドセーフにシナリオの状態を管理し、バックグラウンドタスクとして実行します
        /// </summary>
        /// <typeparam name="TRequest">リクエストの型</typeparam>
        /// <param name="scenario">開始するシナリオの種類</param>
        /// <param name="durationMinutes">実行時間（分）</param>
        /// <param name="request">シナリオのパラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <param name="scenarioWork">シナリオの実行ロジックを表すデリゲート</param>
        /// <returns>開始されたシナリオの状態</returns>
        /// <exception cref="InvalidOperationException">シナリオが既に実行中、またはシナリオ状態が見つからない場合</exception>
        /// <exception cref="ArgumentNullException">requestがnullの場合</exception>
        /// <exception cref="ArgumentOutOfRangeException">durationMinutesが0以下の場合</exception>
        private Task<ScenarioStatus> StartScenarioAsync<TRequest>(
            ScenarioToggleType scenario,
            int durationMinutes,
            TRequest request,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task> scenarioWork)
        {
            if (!_state.TryGetValue(scenario, out var state))
            {
                throw new InvalidOperationException($"Scenario state not found for {scenario}.");
            }

            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var duration = TimeSpan.FromMinutes(durationMinutes);
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            }

            lock (state.SyncRoot)
            {
                if (state.IsActive)
                {
                    throw new InvalidOperationException($"Scenario {scenario} is already running.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                var scenarioCts = new CancellationTokenSource();
                scenarioCts.CancelAfter(duration);

                state.Start(scenarioCts, DateTimeOffset.UtcNow.Add(duration), request);

                state.RunningTask = Task.Run(async () =>
                {
                    try
                    {
                        await scenarioWork(scenarioCts.Token).ConfigureAwait(false);
                        var message = scenarioCts.IsCancellationRequested ? "Scenario cancelled" : "Scenario finished";
                        state.Complete(message);
                    }
                    catch (Exception ex) when (ex is OperationCanceledException && scenarioCts.IsCancellationRequested)
                    {
                        state.Complete("Scenario cancelled");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Scenario {Scenario} failed", scenario);
                        state.Complete($"Scenario error: {ex.Message}");
                    }
                    finally
                    {
                        if (scenario == ScenarioToggleType.MemoryLeak)
                        {
                            ReleaseAllMemory();
                        }
                    }
                }, CancellationToken.None);
            }

            return Task.FromResult(state.ToStatus());
        }

        /// <summary>
        /// 確率的障害シナリオの実行ロジック
        /// 指定された頻度でリクエストをシミュレートし、一定確率で例外を発生させます
        /// </summary>
        /// <param name="request">シナリオのパラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task RunProbabilisticFailureAsync(ProbabilisticFailureRequest request, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var windowStart = DateTime.UtcNow;
                    var batch = new List<Task>(request.RequestsPerSecond);
                    for (int i = 0; i < request.RequestsPerSecond; i++)
                    {
                        batch.Add(SimulateRequestAsync(request.FailurePercentage, cancellationToken));
                    }

                    try
                    {
                        await Task.WhenAll(batch).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Probabilistic failure request batch encountered errors");
                    }

                    var remaining = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - windowStart);
                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // expected when toggle stops the scenario
            }
        }

        /// <summary>
        /// 確率的レイテンシシナリオの実行ロジック
        /// 指定された頻度でリクエストをシミュレートし、一定確率で遅延を注入します
        /// </summary>
        /// <param name="request">シナリオのパラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task RunProbabilisticLatencyAsync(ProbabilisticLatencyRequest request, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var windowStart = DateTime.UtcNow;
                    var batch = new List<Task>(request.RequestsPerSecond);
                    for (int i = 0; i < request.RequestsPerSecond; i++)
                    {
                        batch.Add(SimulateLatencyAsync(request.TriggerPercentage, request.DelayMilliseconds, cancellationToken));
                    }

                    try
                    {
                        await Task.WhenAll(batch).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Probabilistic latency batch encountered errors");
                    }

                    var remaining = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - windowStart);
                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // expected when toggle stops the scenario
            }
        }

        /// <summary>
        /// CPUスパイクシナリオの実行ロジック
        /// 定期的にCPUビジーループを実行して、CPU使用率を急上昇させます
        /// </summary>
        /// <param name="request">シナリオのパラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task RunCpuSpikeAsync(CpuSpikeRequest request, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (Random.Shared.Next(0, 100) < request.TriggerPercentage)
                    {
                        _logger.LogInformation("CPU spike triggered for {Seconds} seconds", request.SpikeSeconds);
                        await Task.Run(() => BusyWait(TimeSpan.FromSeconds(request.SpikeSeconds), cancellationToken), CancellationToken.None).ConfigureAwait(false);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(request.IntervalSeconds), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // expected when toggle stops the scenario
            }
        }

        /// <summary>
        /// メモリリークシナリオの実行ロジック
        /// 定期的に大きなバイト配列を確保し、一定時間保持してメモリリークをシミュレートします
        /// </summary>
        /// <param name="request">シナリオのパラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task RunMemoryLeakAsync(MemoryLeakRequest request, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (Random.Shared.Next(0, 100) < request.TriggerPercentage)
                    {
                        var leaseId = Guid.NewGuid();
                        var allocation = new byte[request.MemoryMegabytes * 1024 * 1024];
                        _memoryLeases[leaseId] = allocation;
                        _logger.LogInformation("Allocated {MB} MB for {Seconds} seconds", request.MemoryMegabytes, request.HoldSeconds);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(request.HoldSeconds), cancellationToken).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                            }
                            finally
                            {
                                _memoryLeases.TryRemove(leaseId, out _);
                            }
                        }, CancellationToken.None);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(request.IntervalSeconds), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // expected when toggle stops the scenario
            }
        }

        /// <summary>
        /// リクエストをシミュレートし、一定確率で例外を発生させます
        /// </summary>
        /// <param name="failurePercentage">失敗率（0～100）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task SimulateRequestAsync(int failurePercentage, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            if (failurePercentage > 0 && Random.Shared.Next(0, 100) < failurePercentage)
            {
                throw new InvalidOperationException("Simulated probabilistic failure.");
            }
        }

        /// <summary>
        /// リクエストをシミュレートし、一定確率で遅延を注入します
        /// </summary>
        /// <param name="triggerPercentage">遅延を発生させる確率（0～100）</param>
        /// <param name="delayMilliseconds">遅延時間（ミリ秒）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task SimulateLatencyAsync(int triggerPercentage, int delayMilliseconds, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            if (triggerPercentage > 0 && Random.Shared.Next(0, 100) < triggerPercentage)
            {
                await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// CPUビジーループを実行してCPU使用率を上げます
        /// SpinWaitを使用して効率的にCPUリソースを消費します
        /// </summary>
        /// <param name="duration">ビジーループの実行時間</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private static void BusyWait(TimeSpan duration, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var spinner = new SpinWait();
            while (watch.Elapsed < duration)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                spinner.SpinOnce();
            }
        }

        /// <summary>
        /// メモリリークシナリオで確保した全メモリを解放します
        /// シナリオ停止時に呼び出されます
        /// </summary>
        private void ReleaseAllMemory()
        {
            foreach (var key in _memoryLeases.Keys)
            {
                _memoryLeases.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// 個別のシナリオの実行状態を管理する内部クラス
        /// スレッドセーフに状態の更新と取得を行います
        /// </summary>
        private sealed class ScenarioState
        {
            public ScenarioState(ScenarioToggleType scenario)
            {
                Scenario = scenario;
            }

            public ScenarioToggleType Scenario { get; }
            public object SyncRoot { get; } = new object();
            public bool IsActive { get; private set; }
            public CancellationTokenSource? Cancellation { get; private set; }
            public Task? RunningTask { get; set; }
            public DateTimeOffset? EndsAtUtc { get; private set; }
            public Dictionary<string, object?>? ConfigSnapshot { get; private set; }
            public string? LastMessage { get; private set; }

            public void Start(CancellationTokenSource cts, DateTimeOffset expectedEnd, object request)
            {
                IsActive = true;
                Cancellation = cts;
                EndsAtUtc = expectedEnd;
                ConfigSnapshot = request.GetType()
                    .GetProperties()
                    .Where(p => p.CanRead)
                    .ToDictionary(p => p.Name, p => p.GetValue(request));
                LastMessage = "Running";
            }

            public void Complete(string message)
            {
                lock (SyncRoot)
                {
                    IsActive = false;
                    EndsAtUtc = null;
                    LastMessage = message;
                    ConfigSnapshot = null;
                    Cancellation?.Dispose();
                    Cancellation = null;
                    RunningTask = null;
                }
            }

            public void Cancel()
            {
                lock (SyncRoot)
                {
                    if (!IsActive)
                    {
                        return;
                    }

                    Cancellation?.Cancel();
                }
            }

            public ScenarioStatus ToStatus()
            {
                lock (SyncRoot)
                {
                    return new ScenarioStatus
                    {
                        Scenario = Scenario,
                        IsActive = IsActive,
                        EndsAtUtc = EndsAtUtc,
                        LastMessage = LastMessage,
                        ActiveConfig = ConfigSnapshot
                    };
                }
            }
        }
    }
}
