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
    /// Toggleシナリオ用にHTTP経由で負荷や障害を注入するターゲットエンドポイント群
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public sealed class ScenarioTargetController : ControllerBase
    {
        private static readonly ConcurrentDictionary<Guid, byte[]> MemoryLeases = new();
        private readonly ILogger<ScenarioTargetController> _logger;

        public ScenarioTargetController(ILogger<ScenarioTargetController> logger)
        {
            _logger = logger;
        }

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

        private static bool ShouldTrigger(int percentage)
        {
            return percentage > 0 && Random.Shared.Next(0, 100) < percentage;
        }

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
