using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add AWS Lambda support. When application is run in Lambda Kestrel is swapped out as the web server with Amazon.Lambda.AspNetCoreServer. This
// package will act as the webserver translating request and responses between the Lambda event source and ASP.NET Core.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(builder.Environment.ApplicationName)
    )
    .WithTracing(tracingOptions =>
        tracingOptions.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
    )
    .AddLambdaExtension()
    .UseOtlpExporter();


var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () =>
{
    Thread.Sleep(1000);
    return "Welcome to running ASP.NET Core Minimal API on AWS Lambda";
});

app.Run();
