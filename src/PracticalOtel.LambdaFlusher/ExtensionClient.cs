using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Lambda Extension API client
/// </summary>
internal class ExtensionClient : IDisposable
{
    #region HTTP header key names

    /// <summary>
    /// HTTP header that is used to register a new extension name with Extension API
    /// </summary>
    private const string LambdaExtensionNameHeader = "Lambda-Extension-Name";

    /// <summary>
    /// HTTP header used to provide extension registration id
    /// </summary>
    /// <remarks>
    /// Registration endpoint reply will have this header value with a new id, assigned to this extension by the API.
    /// All other endpoints will expect HTTP calls to have id header attached to all requests.
    /// </remarks>
    private const string LambdaExtensionIdHeader = "Lambda-Extension-Identifier";

    /// <summary>
    /// HTTP header to report Lambda Extension error type string.
    /// </summary>
    /// <remarks>
    /// This header is used to report additional error details for Init and Shutdown errors.
    /// </remarks>
    private const string LambdaExtensionFunctionErrorTypeHeader = "Lambda-Extension-Function-Error-Type";

    #endregion

    #region Environment variable names

    /// <summary>
    /// Environment variable that holds server name and port number for Extension API endpoints
    /// </summary>
    private const string LambdaRuntimeApiAddress = "AWS_LAMBDA_RUNTIME_API";

    #endregion

    #region Instance properties

    /// <summary>
    /// Extension id, which is assigned to this extension after the registration
    /// </summary>
    public string? Id { get; private set; }

    #endregion

    #region Constructor and readonly variables

    /// <summary>
    /// Http client instance
    /// </summary>
    /// <remarks>This is an IDisposable object that must be properly disposed of,
    /// thus <see cref="ExtensionClient"/> implements <see cref="IDisposable"/> interface too.</remarks>
    private readonly HttpClient httpClient = new HttpClient();

    /// <summary>
    /// Extension name, calculated from the current executing assembly name
    /// </summary>
    private readonly string _extensionName;
    private readonly ILogger _logger;

    /// <summary>
    /// Extension registration URL
    /// </summary>
    private readonly Uri registerUrl;

    /// <summary>
    /// Next event long poll URL
    /// </summary>
    private readonly Uri nextUrl;

    /// <summary>
    /// Constructor
    /// </summary>
    public ExtensionClient(string extensionName, ILogger logger)
    {
        _extensionName = extensionName ?? throw new ArgumentNullException(nameof(extensionName), "Extension name cannot be null");
        _logger = logger;
        this.httpClient.Timeout = Timeout.InfiniteTimeSpan;
        var apiUri = new UriBuilder(Environment.GetEnvironmentVariable(LambdaRuntimeApiAddress)!).Uri;
        var basePath = "2020-01-01/extension";

        // Calculate all Extension API endpoints' URLs
        this.registerUrl = new Uri(apiUri, $"{basePath}/register");
        this.nextUrl = new Uri(apiUri, $"{basePath}/event/next");
    }

    #endregion

    #region Private methods

    /// <summary>
    /// Register extension with Extension API
    /// </summary>
    /// <param name="events">Event types to by notified with</param>
    /// <returns>Awaitable void</returns>
    /// <remarks>This method is expected to be called just once when extension is being registered with the Extension API.</remarks>
    public async Task RegisterExtensionAsync()
    {
        using var scope = OpenTelemetry.SuppressInstrumentationScope.Begin();

        const string payload = @"{ ""events"": [""INVOKE""] }";

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add(LambdaExtensionNameHeader, _extensionName);

        using var response = await this.httpClient.PostAsync(this.registerUrl, content);

        // if POST call didn't succeed
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error response received for registration request: {response}", await response.Content.ReadAsStringAsync());
            response.EnsureSuccessStatusCode();
        }

        this.Id = response.Headers.GetValues(LambdaExtensionIdHeader).FirstOrDefault();
        if (string.IsNullOrEmpty(this.Id))
        {
            throw new ApplicationException("Extension API register call didn't return a valid identifier.");
        }

        this.httpClient.DefaultRequestHeaders.Add(LambdaExtensionIdHeader, this.Id);
    }

    /// <summary>
    /// Long poll for the next event from Extension API
    /// </summary>
    /// <returns>Awaitable tuple having event type and event details fields</returns>
    /// <remarks>It is important to have httpClient.Timeout set to some value, that is longer than any expected wait time,
    /// otherwise HttpClient will throw an exception when getting the next event details from the server.</remarks>
    public async Task GetNextAsync()
    {
        using var scope = OpenTelemetry.SuppressInstrumentationScope.Begin();
        var response = await this.httpClient.GetAsync(this.nextUrl);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error response received for {url}: {response}", this.nextUrl.PathAndQuery, await response.Content.ReadAsStringAsync());
            response.EnsureSuccessStatusCode();
        }
        Console.WriteLine("Received event: " + await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region IDisposable implementation

    /// <summary>
    /// Dispose of instance Disposable variables
    /// </summary>
    public void Dispose()
    {
        // Quick and dirty implementation to propagate Dispose call to HttpClient instance
        ((IDisposable)httpClient).Dispose();
    }

    #endregion
}
