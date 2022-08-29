using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using System.Text;
using Blazorise;
using Xunit.Internal;
using Xunit.Runner.Common;

namespace TheOmenDen.TestRunner.Utilities;

public sealed class BrowserClient : IDisposable, IAsyncDisposable
{
    private const string UniqueIdkey = "UNIQUEIDKEY";

    private static readonly MediaTypeWithQualityHeaderValue JsonMediaType = new(MediaTypeNames.Application.Json);
    
    private static readonly HttpMethod PatchHttpMethod = HttpMethod.Patch;
    
    private ConcurrentQueue<IDictionary<string, object?>> _addQueue = new();
    
    private readonly int _buildId;
    
    private readonly IHttpClientFactory _clientFactory;
    
    private readonly ManualResetEventSlim _finished = new(initialState: false);
    
    private readonly IRunnerLogger _logger;

    private readonly ConcurrentDictionary<string, int> _testToTestIdMap = new();
    
    private readonly AutoResetEvent _workEvent = new(initialState: false);
    
    private volatile bool _previousErrors;
    
    private volatile bool _shouldExit;
    
    private ConcurrentQueue<IDictionary<string, object?>> _updateQueue = new();
    
    public BrowserClient(IRunnerLogger logger,
        IHttpClientFactory clientFactory,
        INotificationService notificationService)
    {
        _clientFactory = clientFactory;
        
        _logger = logger;

        Task.Run(RunLoop);
    }

    private async Task RunLoop()
    {
        int? runId = null;

        try
        {
            runId = await CreateTestRun();

            while (!_shouldExit || !_addQueue.IsEmpty || !_updateQueue.IsEmpty)
            {
                _workEvent.WaitOne(); // Wait for work

                // Get local copies of the queues
                var addQueue = Interlocked.Exchange(ref _addQueue, new ConcurrentQueue<IDictionary<string, object?>>());
                var updateQueue = Interlocked.Exchange(ref _updateQueue, new ConcurrentQueue<IDictionary<string, object?>>());
                
                if (_previousErrors)
                {
                    break;
                }

                // We have to do adds before update because we need the test ID from the add to inject into the update
                await SendTestResults(true, runId.GetValueOrDefault(0), addQueue.ToArray())
                    .ConfigureAwait(false);

                await SendTestResults(false, runId.GetValueOrDefault(0), updateQueue.ToArray())
                    .ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"Browser.RunLoop: Could not create test run. Message: {e.Message}");
        }
        finally
        {
            try
            {
                if (runId.HasValue)
                {
                    await FinishTestRun(runId.Value);
                }
            }
            catch (Exception e)
            {
                _logger.LogError("RunId is not set, cannot complete test run");
                _logger.LogError($"Browser.RunLoop: Could not finish test run. Message: {e.Message}");
            }

            _finished.Set();
        }
    }

    public void AddTest(
        IDictionary<string, object?> request,
        string testUniqueId)
    {
        request.Add(UniqueIdkey, testUniqueId);
        _addQueue.Enqueue(request);
        _workEvent.Set();
    }

    public void UpdateTest(
        IDictionary<string, object?> request,
        string testUniqueId)
    {
        request.Add(UniqueIdkey, testUniqueId);
        _updateQueue.Enqueue(request);
        _workEvent.Set();
    }

    private async Task<int?> CreateTestRun(CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateClient();

        var requestMessage = new Dictionary<string, object?>
        {
            { "name", $"xUnit Runner Test Run on {DateTime.UtcNow:o}"},
            { "build", new Dictionary<string, object?> { { "id", _buildId } } },
            { "isAutomated", true }
        };

        var bodyString = JsonSerializer.Serialize(requestMessage);

        var url = $"{client.BaseAddress}?api-version=1.0";

        var responseString = String.Empty;

        try
        {
            var bodyBytes = Encoding.UTF8.GetBytes(bodyString);

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new ByteArrayContent(bodyBytes)
            };

            request.Content.Headers.ContentType = JsonMediaType;
            request.Headers.Accept.Add(JsonMediaType);

            using var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            using var response = await client.SendAsync(request, tcs.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"When sending 'POST {url}', received status code '{response.StatusCode}'; request body: {bodyString}");
                _previousErrors = true;
            }

            var responseStream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            var responseJson = await JsonSerializer.DeserializeAsync<JsonElement>(responseStream, default(JsonSerializerOptions), cancellationToken);

            if (responseJson.ValueKind is not JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Response was not a JSON object");
            }

            if (!responseJson.TryGetProperty("id", out var idProp)
                || !(idProp.TryGetInt32(out var id)))
            {
                throw new InvalidOperationException($"Response JSON did not have an integer 'id' property");
            }

            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"When sending 'POST {url}' with body '{bodyString}'\nexception was thrown: {ex.Message}\nresponse string:\n{responseString}");
            throw;
        }
    }

    private async Task FinishTestRun(int testRunId, CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateClient();

        var requestMessage = new Dictionary<string, object?>
        {
            { "completedDate", DateTime.UtcNow },
            { "state", "Completed" }
        };

        var bodyString = JsonSerializer.Serialize(requestMessage);

        var url = $"{client.BaseAddress}/{testRunId}?api-version=1.0";

        try
        {
            var bodyBytes = Encoding.UTF8.GetBytes(bodyString);

            using var request = new HttpRequestMessage(PatchHttpMethod, url)
            {
                Content = new ByteArrayContent(bodyBytes)
            };

            request.Content.Headers.ContentType = JsonMediaType;

            request.Headers.Accept.Add(JsonMediaType);

            using var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            using var response = await client.SendAsync(request, tcs.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"When sending 'PATCH {url}', received status code '{response.StatusCode}'; request body: {bodyString}");
                _previousErrors = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"When sending 'PATCH {url}' with body '{bodyString}', exception was thrown: {ex.Message}");
            throw;
        }
    }

    private async Task SendTestResults(
        bool isAdd,
        int runId,
        ICollection<IDictionary<string, object?>> body,
        CancellationToken cancellationToken = default)
    {
        if (body.Count == 0)
        {
            return;
        }

        // For adds, we need to remove the unique IDs and correlate to the responses
        // For update we need to look up the responses
        var added = new List<string>(body.Count);

        if (isAdd)
        {
            // Add them to the list so we can ref by ordinal on the response
            foreach (var item in body)
            {
                var test = (string?)item[UniqueIdkey];
                Guard.NotNull("Pulled null test unique ID from work queue", test);

                item.Remove(UniqueIdkey);
                added.Add(test);
            }
        }
        
        // The values should be in the map
        foreach (var item in body)
        {
            var test = (string?)item[UniqueIdkey];
            Guard.NotNull("Pulled null test unique ID from work queue", test);

            item.Remove(UniqueIdkey);

            // lookup and add
            var testId = _testToTestIdMap[test];
            item.Add("id", testId);
        }

        using var client = _clientFactory.CreateClient();

        var method = isAdd ? HttpMethod.Post : PatchHttpMethod;

        var bodyString = ToJson(body);

        var url = $"{client.BaseAddress}/{runId}/results?api-version=3.0-preview";

        try
        {
            var bodyBytes = Encoding.UTF8.GetBytes(bodyString);

            var request = new HttpRequestMessage(method, url)
            {
                Content = new ByteArrayContent(bodyBytes)
            };

            request.Content.Headers.ContentType = JsonMediaType;
            request.Headers.Accept.Add(JsonMediaType);

            using var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var response = await client.SendAsync(request, tcs.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"When sending '{method} {url}', received status code '{response.StatusCode}'; request body:\n{bodyString}");
                _previousErrors = true;
            }

            if (isAdd)
            {
                var respStream = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);

                var responseJson = await JsonSerializer.DeserializeAsync<JsonElement>(respStream, default(JsonSerializerOptions), cancellationToken);
                
                if (responseJson.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException($"JSON response was not in the proper format (expected Object, got {responseJson.ValueKind})");
                }

                if (!responseJson.TryGetProperty("value", out var testCases) ||
                    testCases.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("JSON response was missing top-level 'value' array");
                }

                for (var i = 0; i < testCases.GetArrayLength(); ++i)
                {
                    var testCase = testCases[i];

                    if (testCase.ValueKind is not JsonValueKind.Object)
                    {
                        throw new InvalidOperationException($"JSON response value element {i} was not in the proper format (expected Object, got {testCase.ValueKind})");
                    }

                    if (!testCase.TryGetProperty("id", out var idProp) || !idProp.TryGetInt32(out var id))
                    {
                        throw new InvalidOperationException($"JSON response value element {i} is missing an 'id' property or it wasn't an integer");
                    }

                    // Match the test by ordinal
                    var test = added![i];

                    _testToTestIdMap[test] = id;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"When sending '{method} {url}' with body '{bodyString}', exception was thrown: {ex.Message}");
            throw;
        }
    }

    private static string ToJson(IEnumerable<IDictionary<string, object?>> data)
    {
        var results = String.Join(",", 
            data.Select(x => JsonSerializer.Serialize(x)));

        return $"[{results}]";
    }

    public void Dispose()
    {        // Free up to process any remaining work
        _shouldExit = true;
        _workEvent.Set();

        _finished.Wait();
        _finished.Dispose();
    }

    public ValueTask DisposeAsync()
    {        // Free up to process any remaining work
        _shouldExit = true;
        _workEvent.Set();

        _finished.Wait();
        _finished.Dispose();

        return ValueTask.CompletedTask;
    }
}