using Engine.Core.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Engine.Core.Scheduling;

public sealed class TickHostedService : BackgroundService
{
    private readonly TickScheduler _scheduler;
    private readonly ILogger<TickHostedService> _logger;
    private static readonly Action<ILogger, long, int, TimeSpan, Exception?> TickExecutedLog =
        LoggerMessage.Define<long, int, TimeSpan>(
            logLevel: LogLevel.Debug,
            eventId: new EventId(1337, "TickExecuted"),
            formatString: "Tick {Tick} executed {Consumers} consumers in {Elapsed}.");

    public TickHostedService(TickScheduler scheduler, ILogger<TickHostedService> logger)
    {
        _scheduler = scheduler;
        _logger = logger;
        _scheduler.TickExecuted += OnTickExecuted;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _scheduler.RunContinuouslyAsync(stoppingToken);

    private void OnTickExecuted(object? sender, TickTelemetryEventArgs telemetry)
    {
        TickExecutedLog(_logger, telemetry.TickIndex, telemetry.ConsumerCount, telemetry.Elapsed, null);
    }

    public override void Dispose()
    {
        _scheduler.TickExecuted -= OnTickExecuted;
        base.Dispose();
    }
}
