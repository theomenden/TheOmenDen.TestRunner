namespace TheOmenDen.TestRunner.Services;

public interface ITestContextService
{
    IAsyncEnumerable<IXunitTestCase> DiscoverTestCasesAsync(Stream stream);
}
