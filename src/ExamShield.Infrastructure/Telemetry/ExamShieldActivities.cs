using System.Diagnostics;

namespace ExamShield.Infrastructure.Telemetry;

/// <summary>
/// Central ActivitySource for the ExamShield capture pipeline.
/// Works with any OTLP-compatible collector (Jaeger, Grafana Tempo, etc.) via the
/// OTel auto-instrumentation agent or by configuring OTEL_TRACES_EXPORTER at runtime.
/// No NuGet packages required — System.Diagnostics.ActivitySource is built into .NET.
/// </summary>
public static class ExamShieldActivities
{
    public const string SourceName = "ExamShield";
    public const string Version    = "1.0.0";

    public static readonly ActivitySource Source = new(SourceName, Version);
}
