using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticScenarios.Models;
using DiagnosticScenarios.Services;
using Microsoft.AspNetCore.Mvc;

namespace DiagnosticScenarios.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScenarioToggleController : ControllerBase
    {
        private readonly IScenarioToggleService _service;

        public ScenarioToggleController(IScenarioToggleService service)
        {
            _service = service;
        }

        [HttpGet("status")]
        public ActionResult<IEnumerable<ScenarioStatus>> GetStatuses()
        {
            return Ok(_service.GetStatuses());
        }

        [HttpPost("probabilistic-failure/start")]
        public Task<ActionResult<ScenarioStatus>> StartProbabilisticFailure(
            [FromBody] ProbabilisticFailureRequest request,
            CancellationToken cancellationToken) =>
            ExecuteStartAsync(() => _service.StartProbabilisticFailureAsync(request, cancellationToken));

        [HttpPost("probabilistic-failure/stop")]
        public ActionResult<ScenarioStatus> StopProbabilisticFailure()
        {
            return Ok(_service.StopScenario(ScenarioToggleType.ProbabilisticFailure));
        }

        [HttpPost("cpu-spike/start")]
        public Task<ActionResult<ScenarioStatus>> StartCpuSpike(
            [FromBody] CpuSpikeRequest request,
            CancellationToken cancellationToken) =>
            ExecuteStartAsync(() => _service.StartCpuSpikeAsync(request, cancellationToken));

        [HttpPost("cpu-spike/stop")]
        public ActionResult<ScenarioStatus> StopCpuSpike()
        {
            return Ok(_service.StopScenario(ScenarioToggleType.CpuSpike));
        }

        [HttpPost("memory-leak/start")]
        public Task<ActionResult<ScenarioStatus>> StartMemoryLeak(
            [FromBody] MemoryLeakRequest request,
            CancellationToken cancellationToken) =>
            ExecuteStartAsync(() => _service.StartMemoryLeakAsync(request, cancellationToken));

        [HttpPost("memory-leak/stop")]
        public ActionResult<ScenarioStatus> StopMemoryLeak()
        {
            return Ok(_service.StopScenario(ScenarioToggleType.MemoryLeak));
        }

        [HttpPost("probabilistic-latency/start")]
        public Task<ActionResult<ScenarioStatus>> StartProbabilisticLatency(
            [FromBody] ProbabilisticLatencyRequest request,
            CancellationToken cancellationToken) =>
            ExecuteStartAsync(() => _service.StartProbabilisticLatencyAsync(request, cancellationToken));

        [HttpPost("probabilistic-latency/stop")]
        public ActionResult<ScenarioStatus> StopProbabilisticLatency()
        {
            return Ok(_service.StopScenario(ScenarioToggleType.ProbabilisticLatency));
        }

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
