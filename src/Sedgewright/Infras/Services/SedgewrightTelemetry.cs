using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Sedgewright.Infras.Services;

internal static class SedgewrightTelemetry
{
    private const string ServiceName = "Sedgewright";
    private static readonly ActivitySource ChatActivitySource = new(ServiceName);
    private static readonly Meter ApplicationMeter = new(ServiceName);
    private static readonly Counter<long> ChatTurnCount = ApplicationMeter.CreateCounter<long>("sedgewright.chat.turns");
    private static readonly Histogram<double> ChatTurnDuration = ApplicationMeter.CreateHistogram<double>("sedgewright.chat.turn.duration", "ms");

    public static void Configure(IServiceCollection services)
    {
        bool hasOtlpEndpoint = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("Sedgewright", LogLevel.Information);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error);
            logging.AddFilter("Microsoft.Agents.AI.Workflows.Specialized.Magentic", LogLevel.Debug);
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            });
        });

        OpenTelemetryBuilder openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddSource(ServiceName)
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter(ServiceName)
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (hasOtlpEndpoint)
        {
            openTelemetry.UseOtlpExporter();
        }
    }

    public static Activity? StartChatTurn()
        => ChatActivitySource.StartActivity("sedgewright.chat.send");

    public static void RecordChatTurn(string outcome, double durationMilliseconds)
    {
        TagList tags = default;
        tags.Add("outcome", outcome);
        ChatTurnCount.Add(1, tags);
        ChatTurnDuration.Record(durationMilliseconds, tags);
    }
}
