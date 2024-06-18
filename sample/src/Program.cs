using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add AWS Lambda support. When application is run in Lambda Kestrel is swapped out as the web server with Amazon.Lambda.AspNetCoreServer. This
// package will act as the webserver translating request and responses between the Lambda event source and ASP.NET Core.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Services.AddSingleton<ConsoleOpenTelemetryListener>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(builder.Environment.ApplicationName)
    )
    .WithTracing(tracingOptions => 
        tracingOptions.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
    )
    .UseOtlpExporter();


var app = builder.Build();

var tracerProvider = app.Services.GetRequiredService<TracerProvider>();
ActivityListener listener = new()
{
    ShouldListenTo = _ => true,
    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
    ActivityStopped = activity =>
    {
        Console.WriteLine("Activity stopped: " + activity.Source.Name + " " + activity.DisplayName + " " + activity.Duration);
        tracerProvider.ForceFlush();
    }
};

ActivitySource.AddActivityListener(listener);

var openTelemetryDebugLogger = app.Services.GetRequiredService<ConsoleOpenTelemetryListener>();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => {
    Thread.Sleep(1000);
    return "Welcome to running ASP.NET Core Minimal API on AWS Lambda";
});

app.Run();


public class ConsoleOpenTelemetryListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.StartsWith("OpenTelemetry"))
            EnableEvents(eventSource, EventLevel.Error);
    }
 
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        Console.WriteLine(string.Format(eventData.Message, eventData.Payload?.Select(p => p?.ToString())?.ToArray()));
    }
}