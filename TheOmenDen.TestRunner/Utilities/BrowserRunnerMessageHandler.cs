using Xunit.Runner.Common;

namespace TheOmenDen.TestRunner.Utilities;
public class BrowserRunnerMessageHandler : DefaultRunnerReporterMessageHandler
{
    private const Int32 MaxLength = 4096;
    private Int32 _assembliesInFlight;
    private readonly String _baseUri;
    private static readonly Object ClientLock = new();
    private readonly BrowserClient _browserClient;

    public BrowserRunnerMessageHandler(IRunnerLogger logger, string baseUri, int assembliesInFlight, BrowserClient browserClient) 
        : base(logger)
    {
        _baseUri = baseUri;
        _assembliesInFlight = assembliesInFlight;
        _browserClient = browserClient;
    }
}