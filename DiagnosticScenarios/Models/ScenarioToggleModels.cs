using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DiagnosticScenarios.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ScenarioToggleType
    {
        ProbabilisticFailure,
        CpuSpike,
        MemoryLeak,
        ProbabilisticLatency
    }

    public sealed class ScenarioStatus
    {
        public ScenarioToggleType Scenario { get; init; }
        public bool IsActive { get; init; }
        public DateTimeOffset? EndsAtUtc { get; init; }
        public string? LastMessage { get; init; }
        public IReadOnlyDictionary<string, object?>? ActiveConfig { get; init; }
    }

    public sealed class ProbabilisticFailureRequest
    {
        [Range(1, 180)]
        public int DurationMinutes { get; set; }

        [Range(1, 1000)]
        public int RequestsPerSecond { get; set; }

        [Range(0, 100)]
        public int FailurePercentage { get; set; }
    }

    public sealed class CpuSpikeRequest
    {
        [Range(1, 180)]
        public int DurationMinutes { get; set; }

        [Range(1, 300)]
        public int IntervalSeconds { get; set; }

        [Range(0, 100)]
        public int TriggerPercentage { get; set; }

        [Range(1, 30)]
        public int SpikeSeconds { get; set; }
    }

    public sealed class MemoryLeakRequest
    {
        [Range(1, 180)]
        public int DurationMinutes { get; set; }

        [Range(1, 300)]
        public int IntervalSeconds { get; set; }

        [Range(0, 100)]
        public int TriggerPercentage { get; set; }

        [Range(1, 1024)]
        public int MemoryMegabytes { get; set; }

        [Range(1, 60)]
        public int HoldSeconds { get; set; }
    }

    public sealed class ProbabilisticLatencyRequest
    {
        [Range(1, 180)]
        public int DurationMinutes { get; set; }

        [Range(1, 1000)]
        public int RequestsPerSecond { get; set; }

        [Range(0, 100)]
        public int TriggerPercentage { get; set; }

        [Range(1, 10000)]
        public int DelayMilliseconds { get; set; }
    }
}
