using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticScenarios.Models;
using Microsoft.AspNetCore.Http;
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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        // シナリオごとの状態を管理するスレッドセーフなディクショナリ
        private readonly ConcurrentDictionary<ScenarioToggleType, ScenarioState> _state;

        private static class ScenarioTargetEndpoints
        {
            public const string ProbabilisticFailure = "api/ScenarioTarget/probabilistic-failure";
            public const string ProbabilisticLatency = "api/ScenarioTarget/probabilistic-latency";
            public const string CpuSpike = "api/ScenarioTarget/cpu-spike";
            public const string MemoryLeak = "api/ScenarioTarget/memory-leak";
            public const string MemoryLeakRelease = "api/ScenarioTarget/memory-leak/release";
        }

        /// <summary>
        /// ScenarioToggleServiceのコンストラクタ
        /// 全シナリオの初期状態を構築します
        /// </summary>
        /// <param name="logger">ロガー（DIコンテナから注入）</param>
        public ScenarioToggleService(
            ILogger<ScenarioToggleService> logger,
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
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
            return StartScenarioAsync(ScenarioToggleType.ProbabilisticFailure, request.DurationMinutes, request, cancellationToken, (client, baseUri, token) => RunProbabilisticFailureAsync(request, client, baseUri, token));
        }

        public Task<ScenarioStatus> StartCpuSpikeAsync(CpuSpikeRequest request, CancellationToken cancellationToken)
        {
            return StartScenarioAsync(ScenarioToggleType.CpuSpike, request.DurationMinutes, request, cancellationToken, (client, baseUri, token) => RunCpuSpikeAsync(request, client, baseUri, token));
        }

        public Task<ScenarioStatus> StartMemoryLeakAsync(MemoryLeakRequest request, CancellationToken cancellationToken)
        {
            return StartScenarioAsync(ScenarioToggleType.MemoryLeak, request.DurationMinutes, request, cancellationToken, (client, baseUri, token) => RunMemoryLeakAsync(request, client, baseUri, token));
        }

        public Task<ScenarioStatus> StartProbabilisticLatencyAsync(ProbabilisticLatencyRequest request, CancellationToken cancellationToken)
        {
            return StartScenarioAsync(ScenarioToggleType.ProbabilisticLatency, request.DurationMinutes, request, cancellationToken, (client, baseUri, token) => RunProbabilisticLatencyAsync(request, client, baseUri, token));
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
            Func<HttpClient, Uri, CancellationToken, Task> scenarioWork)
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
                var httpClient = _httpClientFactory.CreateClient();
                var baseUri = ResolveBaseUri();

                state.Start(scenarioCts, DateTimeOffset.UtcNow.Add(duration), request);

                state.RunningTask = Task.Run(async () =>
                {
                    try
                    {
                        await scenarioWork(httpClient, baseUri, scenarioCts.Token).ConfigureAwait(false);
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
                            await ReleaseAllMemoryAsync(httpClient, baseUri).ConfigureAwait(false);
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
        private Task RunProbabilisticFailureAsync(ProbabilisticFailureRequest request, HttpClient httpClient, Uri baseUri, CancellationToken cancellationToken)
        {
            return RunRequestsPerSecondAsync(
                request.RequestsPerSecond,
                token => SendScenarioRequestAsync(httpClient, baseUri, ScenarioTargetEndpoints.ProbabilisticFailure, request, token, logNonSuccess: false),
                cancellationToken);
        }

        /// <summary>
        /// 確率的レイテンシシナリオの実行ロジック
        /// 指定された頻度でリクエストをシミュレートし、一定確率で遅延を注入します
        /// </summary>
        /// <param name="request">シナリオのパラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private Task RunProbabilisticLatencyAsync(ProbabilisticLatencyRequest request, HttpClient httpClient, Uri baseUri, CancellationToken cancellationToken)
        {
            return RunRequestsPerSecondAsync(
                request.RequestsPerSecond,
                token => SendScenarioRequestAsync(httpClient, baseUri, ScenarioTargetEndpoints.ProbabilisticLatency, request, token),
                cancellationToken);
        }

        /// <summary>
        /// CPUスパイクシナリオの実行ロジック
        /// 定期的にCPUビジーループを実行して、CPU使用率を急上昇させます
        /// </summary>
        /// <param name="request">シナリオのパラメータ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task RunCpuSpikeAsync(CpuSpikeRequest request, HttpClient httpClient, Uri baseUri, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await SendScenarioRequestAsync(httpClient, baseUri, ScenarioTargetEndpoints.CpuSpike, request, cancellationToken).ConfigureAwait(false);
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
        private async Task RunMemoryLeakAsync(MemoryLeakRequest request, HttpClient httpClient, Uri baseUri, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await SendScenarioRequestAsync(httpClient, baseUri, ScenarioTargetEndpoints.MemoryLeak, request, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(request.IntervalSeconds), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // expected when toggle stops the scenario
            }
        }

        private async Task RunRequestsPerSecondAsync(int requestsPerSecond, Func<CancellationToken, Task> requestFactory, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var windowStart = DateTime.UtcNow;
                    var batch = new List<Task>(requestsPerSecond);
                    for (int i = 0; i < requestsPerSecond; i++)
                    {
                        batch.Add(requestFactory(cancellationToken));
                    }

                    try
                    {
                        await Task.WhenAll(batch).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Scenario request batch encountered errors");
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

        private async Task SendScenarioRequestAsync(HttpClient httpClient, Uri baseUri, string relativeUri, object payload, CancellationToken cancellationToken, bool logNonSuccess = true)
        {
            var requestUri = new Uri(baseUri, relativeUri);
            try
            {
                using var response = await httpClient.PostAsJsonAsync(requestUri, payload, cancellationToken).ConfigureAwait(false);
                if (logNonSuccess && !response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning(
                        "Scenario target {Endpoint} responded with {Status}: {Content}",
                        requestUri,
                        response.StatusCode,
                        content);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scenario request to {Endpoint} failed", requestUri);
            }
        }

        private async Task ReleaseAllMemoryAsync(HttpClient httpClient, Uri baseUri)
        {
            try
            {
                var requestUri = new Uri(baseUri, ScenarioTargetEndpoints.MemoryLeakRelease);
                using var response = await httpClient.PostAsync(requestUri, content: null).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogWarning(
                        "Memory leak release endpoint responded with {Status}: {Content}",
                        response.StatusCode,
                        content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Memory leak release request failed");
            }
        }

        private Uri ResolveBaseUri()
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("Unable to resolve current HTTP context for scenario start.");

            var request = httpContext.Request;
            var host = request.Host;
            if (!host.HasValue)
            {
                throw new InvalidOperationException("Current request does not specify a host header.");
            }

            var pathBase = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
            if (!pathBase.EndsWith('/'))
            {
                pathBase += "/";
            }

            var builder = new UriBuilder
            {
                Scheme = request.Scheme,
                Host = host.Host,
                Port = host.Port ?? -1,
                Path = pathBase
            };

            return builder.Uri;
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
