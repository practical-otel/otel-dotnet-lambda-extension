using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace PracticalOtel.LambdaFlusher;

internal class OtelLambdaExtensionService : BackgroundService
{
    private readonly ExtensionClient _extensionClient;
    private readonly TracerProvider _tracerProvider;
    private readonly ILogger<OtelLambdaExtensionService> _logger;

    private static readonly Channel<Activity> _channel = Channel.CreateUnbounded<Activity>();

    private readonly ActivityListener _listener = new()
    {
        ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
        //ShouldListenTo = _ => true,
        Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        ActivityStopped = activity =>
        {
            Console.WriteLine("Activity stopped: " + activity.Source.Name + " " + activity.DisplayName + " " + activity.Duration);
           _channel.Writer.WriteAsync(activity);
        }
    };

    public OtelLambdaExtensionService(ExtensionClient extensionClient, TracerProvider tracerProvider, ILogger<OtelLambdaExtensionService> logger)
    {
        _extensionClient = extensionClient;
        _tracerProvider = tracerProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ActivitySource.AddActivityListener(_listener);
        await _extensionClient.RegisterExtensionAsync();


        while(true)
        {
            await _extensionClient.GetNextAsync();
            await _channel.Reader.WaitToReadAsync();
            _tracerProvider.ForceFlush();
        }
    }
}

