using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PracticalOtel.LambdaFlusher;

namespace OpenTelemetry;

public static class LambdaExtensionSetup
{
    public static OpenTelemetryBuilder AddLambdaExtension(this OpenTelemetryBuilder builder)
    {
        builder.Services.AddHostedService<OtelLambdaExtensionService>();
        builder.Services.AddSingleton(sp => 
            new ExtensionClient("OtelLambdaExtensionService", sp.GetRequiredService<ILogger<ExtensionClient>>()));
        return builder;
    }
}
