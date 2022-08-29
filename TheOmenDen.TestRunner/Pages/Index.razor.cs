using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using System.Xml.Linq;
using Xunit.Internal;
using Xunit;
using Xunit.Runner.Common;
using Xunit.Runners;
using System.Collections.Concurrent;
using System.Text;
using Blazorise;

namespace TheOmenDen.TestRunner.Pages
{
    public partial class Index : ComponentBase, IAsyncDisposable
    {
        [Inject]
        public INotificationService NotificationService { get; init; }

        private IEnumerable<IXunitTestCase> _xUnitTestCases = Enumerable.Empty<IXunitTestCase>();

        private static readonly object AssemblyLock = new();

        private readonly AssemblyRunner _assemblyRunner;

        private static readonly ManualResetEvent _finished = new(false);

        private static Int32 _failed = 0;

        private volatile bool _cancel;

        private bool _hasFailed;

        private bool _disposed;

        private Int32 _testCasesToRun = 0;

        private Int32 _testCasesDiscovered = 0;
        
        private readonly ConcurrentDictionary<string, ExecutionSummary> _completionMessages = new();
        
        protected override async Task OnInitializedAsync()
        {
            var projectAssembly = typeof(Program).Assembly.FullName;

            _failed = await LoadTestsAsync(projectAssembly ?? String.Empty, null);
        }

        private async ValueTask<Int32> LoadTestsAsync(String testAssembly, string? typeName, CancellationToken cancellationToken = default)
        {
            await using var runner = AssemblyRunner.WithoutAppDomain(testAssembly);

            runner.OnDiscoveryComplete = OnDiscoveryComplete;
            runner.OnExecutionComplete = OnExecutionComplete;
            runner.OnTestFailed = OnTestFailed;
            runner.OnTestSkipped = OnTestSkipped;
            runner.OnTestStarting = OnTestStarting;
            runner.Start(typeName);

            _finished.WaitOne();

            return _failed;
        }

        private void OnDiscoveryComplete(DiscoveryCompleteInfo info)
        {
            lock (AssemblyLock)
            {
                _testCasesToRun = info.TestCasesToRun;
                _testCasesDiscovered = info.TestCasesDiscovered;
            }
        }

        private void OnExecutionComplete(ExecutionCompleteInfo info)
        {
            lock (AssemblyLock)
            {
                NotificationService.Success($"Finished: {info.TotalTests} tests in {Math.Round(info.ExecutionTime, 3)}s ({info.TestsFailed} failed, {info.TestsSkipped} skipped)");
            }

            _finished.Set();
        }

        private void OnTestFailed(TestFailedInfo info)
        {
            lock (AssemblyLock)
            {
                NotificationService.Error($"[FAIL] {info.TestDisplayName}: {info.ExceptionMessage}");
                
                if (!String.IsNullOrWhiteSpace(info.ExceptionStackTrace))
                {
                    Console.WriteLine(info.ExceptionStackTrace);
                }
            }
            _failed = 1;
        }

        private void OnTestSkipped(TestSkippedInfo info)
        {
            lock (AssemblyLock)
            {
                NotificationService.Warning($"[SKIP] {info.TestDisplayName}: {info.SkipReason}");
            }
        }

        private void OnTestStarting(TestStartingInfo info)
        {
            lock (AssemblyLock)
            {
            }
        }

        public async ValueTask<int> RunProject(
      XunitProject project,
      _IMessageSink reporterMessageHandler)
        {
            XElement? assembliesElement = null;

            var clockTime = Stopwatch.StartNew();
            
            var xmlTransformers = TransformFactory.GetXmlTransformers(project);
            
            var needsXml = xmlTransformers.Count > 0;
            
            // TODO: Parallelize the ones that will parallelize, and then run the rest sequentially?
            var parallelizeAssemblies = project.Assemblies
                .All(assembly => assembly.Configuration.ParallelizeAssemblyOrDefault);

            if (needsXml)
            {
                assembliesElement = new XElement("assemblies");
            }

            var originalWorkingFolder = Directory.GetCurrentDirectory();

            if (parallelizeAssemblies)
            {
                var tasks = project.Assemblies.Select(
                    assembly => Task.Run(
                        () => RunProjectAssembly(
                            assembly,
                            needsXml,
                            reporterMessageHandler
                        ).AsTask()
                    )
                );

                var results = await Task.WhenAll(tasks);

                foreach (var assemblyElement in results.WhereNotNull())
                {
                    assembliesElement?.Add(assemblyElement);
                }
            }
            else
            {
                foreach (var assembly in project.Assemblies)
                {
                    var assemblyElement = await RunProjectAssembly(
                        assembly,
                        needsXml,
                        reporterMessageHandler);

                    if (assemblyElement is not null)
                    {
                        assembliesElement?.Add(assemblyElement);
                    }
                }
            }

            clockTime.Stop();

            assembliesElement?.Add(new XAttribute("timestamp", DateTime.Now.ToString(CultureInfo.InvariantCulture)));

            if (!_completionMessages.IsEmpty)
            {
                var summaries = new TestExecutionSummaries
                {
                    ElapsedClockTime = clockTime.Elapsed
                };

                foreach (var completionMessage in _completionMessages.OrderBy(kvp => kvp.Key))
                {
                    summaries.Add(completionMessage.Key, completionMessage.Value);
                }

                reporterMessageHandler.OnMessage(summaries);
            }

            Directory.SetCurrentDirectory(originalWorkingFolder);

            if (assembliesElement is not null)
            {
                xmlTransformers.ForEach(transformer => transformer(assembliesElement));
            }

            return _hasFailed 
                ? 1 
                : _completionMessages.Values
                    .Sum(summary => summary.Failed + summary.Errors);
        }

        private async ValueTask<XElement?> RunProjectAssembly(
        XunitProjectAssembly assembly,
        bool needsXml,
        _IMessageSink reporterMessageHandler)
        {
            if (_cancel)
            {
                return null;
            }

            var assemblyElement = needsXml ? new XElement("assembly") : null;

            try
            {
                var assemblyFileName = Guard.ArgumentNotNull(assembly.AssemblyFileName);

                // Default to false for console runners
                assembly.Configuration.PreEnumerateTheories ??= false;

                // Setup discovery and execution options with command-line overrides
                var discoveryOptions = _TestFrameworkOptions.ForDiscovery(assembly.Configuration);
                
                var executionOptions = _TestFrameworkOptions.ForExecution(assembly.Configuration);

                var assemblyDisplayName = Path.GetFileNameWithoutExtension(assemblyFileName);
                
                var noColor = assembly.Project.Configuration.NoColorOrDefault;
                
                var diagnosticMessageSink = ConsoleDiagnosticMessageSink.ForDiagnostics(consoleLock, assemblyDisplayName, assembly.Configuration.DiagnosticMessagesOrDefault, noColor);
                
                var internalDiagnosticsMessageSink = ConsoleDiagnosticMessageSink.ForInternalDiagnostics(consoleLock, assemblyDisplayName, assembly.Configuration.InternalDiagnosticMessagesOrDefault, noColor);
                
                var appDomainSupport = assembly.Configuration.AppDomainOrDefault;
                
                var shadowCopy = assembly.Configuration.ShadowCopyOrDefault;
                
                var longRunningSeconds = assembly.Configuration.LongRunningTestSecondsOrDefault;

                using var _ = AssemblyHelper.SubscribeResolveForAssembly(assemblyFileName, internalDiagnosticsMessageSink);
                
                await using var controller = XunitFrontController.ForDiscoveryAndExecution(assembly, diagnosticMessageSink: diagnosticMessageSink);

                var executionStarting = new TestAssemblyExecutionStarting
                {
                    Assembly = assembly,
                    ExecutionOptions = executionOptions
                };
                
                reporterMessageHandler.OnMessage(executionStarting);

                IExecutionSink resultsSink = new DelegatingExecutionSummarySink(reporterMessageHandler, () => cancel, (summary, _) => _completionMessages.TryAdd(controller.TestAssemblyUniqueID, summary));
                
                if (assemblyElement is not null)
                {
                    resultsSink = new DelegatingXmlCreationSink(resultsSink, assemblyElement);
                }

                if (longRunningSeconds > 0 && diagnosticMessageSink is not null)
                {
                    resultsSink = new DelegatingLongRunningTestDetectionSink(resultsSink, TimeSpan.FromSeconds(longRunningSeconds), diagnosticMessageSink);
                }

                if (assembly.Configuration.FailSkipsOrDefault)
                {
                    resultsSink = new DelegatingFailSkipSink(resultsSink);
                }

                using var sink = resultsSink;

                var settings = new FrontControllerFindAndRunSettings(discoveryOptions, executionOptions, assembly.Configuration.Filters);
                
                controller.FindAndRun(resultsSink, settings);
                
                resultsSink.Finished.WaitOne();

                var executionFinished = new TestAssemblyExecutionFinished
                {
                    Assembly = assembly,
                    ExecutionOptions = executionOptions,
                    ExecutionSummary = resultsSink.ExecutionSummary
                };

                reporterMessageHandler.OnMessage(executionFinished);

                if (assembly.Configuration.StopOnFailOrDefault 
                    && resultsSink.ExecutionSummary.Failed != 0)
                {
                    await NotificationService.Warning("Canceling due to test failure...");
                    _cancel = true;
                }
            }
            catch (Exception ex)
            {
                _hasFailed = true;
                var sb = new StringBuilder();
                
                var e = ex;
                
                while (e is not null)
                {
                    sb.AppendLine($"{e.GetType().FullName}: {e.Message}");

                    if (assembly.Configuration.InternalDiagnosticMessagesOrDefault)
                    {
                        sb.AppendLine(e.StackTrace);
                    }

                    e = e.InnerException;
                }
            }

            return assemblyElement;
        }
        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            lock (AssemblyLock)
            {
                _finished.Dispose();
                _disposed = true;
            }

            await _assemblyRunner.DisposeAsync();

            GC.SuppressFinalize(this);
        }


    }
}
