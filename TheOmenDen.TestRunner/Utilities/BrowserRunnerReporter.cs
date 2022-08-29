using Xunit.Internal;
using Xunit.Runner.Common;

namespace TheOmenDen.TestRunner.Utilities;

public class BrowserRunnerReporter : IRunnerReporter
{
    private BrowserClient? client;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly object ClientLock = new();



    public BrowserRunnerReporter(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    protected BrowserClient Client
    {
        get
        {
            lock (ClientLock)
            {
                using var httpClient = _httpClientFactory.CreateClient();

                client ??= new BrowserClient(httpClient);
            }

            return client;
        }
    }

    public ValueTask<_IMessageSink> CreateMessageHandler(IRunnerLogger logger, _IMessageSink? diagnosticMessageSink)
    {
        return new(new BrowserClient(logger));
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public string Description => "The Omen Den Test Runner";
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool ForceNoLogo => false;
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool IsEnvironmentallyEnabled => true;
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public string? RunnerSwitch => null;
}