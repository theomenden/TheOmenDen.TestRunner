using Xunit.Runner.Common;
using Xunit.Runners;

namespace TheOmenDen.TestRunner.Services;
public class TestContextService : ITestContextService
{
    private static readonly object AssemblyLock = new();

    private readonly AssemblyRunner _assemblyRunner;

    private static ManualResetEvent _finished = new(false);

    private static Int32 _failed = 0;

    private volatile bool _cancel;

    private bool _disposed;

    public TestContextService(AssemblyRunner assemblyRunner)
    {
        _assemblyRunner = assemblyRunner;
    }

    public async ValueTask<Int32> LoadTestsAsync(XunitProject xunitProject, String testAssembly, string? typeName, CancellationToken cancellationToken = default)
    {
        await using var runner = AssemblyRunner.WithoutAppDomain(testAssembly);
        
        runner.OnDiscoveryComplete = OnDiscoveryComplete;
        runner.OnExecutionComplete = OnExecutionComplete;
        runner.OnTestFailed = OnTestFailed;
        runner.OnTestSkipped = OnTestSkipped;

        runner.Start(typeName);

        _finished.WaitOne();

        return _failed;
    }
    
    private void OnDiscoveryComplete(DiscoveryCompleteInfo info)
    {
        lock (AssemblyLock)
        {
            Console.WriteLine($"Running {info.TestCasesToRun} of {info.TestCasesDiscovered} tests...");
        }
    }

    private void OnExecutionComplete(ExecutionCompleteInfo info)
    {
        lock (AssemblyLock)
        {
            Console.WriteLine($"Finished: {info.TotalTests} tests in {Math.Round(info.ExecutionTime, 3)}s ({info.TestsFailed} failed, {info.TestsSkipped} skipped)");
        }

        _finished.Set();
    }

    private void OnTestFailed(TestFailedInfo info)
    {
        lock (AssemblyLock)
        {
            Console.WriteLine("[FAIL] {0}: {1}", info.TestDisplayName, info.ExceptionMessage);
            
            if (info.ExceptionStackTrace != null)
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
            Console.WriteLine("[SKIP] {0}: {1}", info.TestDisplayName, info.SkipReason);
        }
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