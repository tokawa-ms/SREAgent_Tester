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
    public interface IScenarioToggleService
    {
        IReadOnlyCollection<ScenarioStatus> GetStatuses();
        ScenarioStatus GetStatus(ScenarioToggleType scenario);
        Task<ScenarioStatus> StartProbabilisticFailureAsync(ProbabilisticFailureRequest request, CancellationToken cancellationToken);
        Task<ScenarioStatus> StartCpuSpikeAsync(CpuSpikeRequest request, CancellationToken cancellationToken);
        Task<ScenarioStatus> StartMemoryLeakAsync(MemoryLeakRequest request, CancellationToken cancellationToken);
        Task<ScenarioStatus> StartProbabilisticLatencyAsync(ProbabilisticLatencyRequest request, CancellationToken cancellationToken);
        ScenarioStatus StopScenario(ScenarioToggleType scenario);
    }

    internal sealed class ScenarioToggleService : IScenarioToggleService
    {
        private readonly ILogger<ScenarioToggleService> _logger;
        private readonly ConcurrentDictionary<ScenarioToggleType, ScenarioState> _state;
        private readonly ConcurrentDictionary<Guid, byte[]> _memoryLeases = new();

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

        private async Task SimulateRequestAsync(int failurePercentage, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            if (failurePercentage > 0 && Random.Shared.Next(0, 100) < failurePercentage)
            {
                throw new InvalidOperationException("Simulated probabilistic failure.");
            }
        }

        private async Task SimulateLatencyAsync(int triggerPercentage, int delayMilliseconds, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            if (triggerPercentage > 0 && Random.Shared.Next(0, 100) < triggerPercentage)
            {
                await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

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

        private void ReleaseAllMemory()
        {
            foreach (var key in _memoryLeases.Keys)
            {
                _memoryLeases.TryRemove(key, out _);
            }
        }

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
