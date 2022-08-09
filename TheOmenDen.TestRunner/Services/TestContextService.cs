using Xunit;
using Xunit.Runner.Common;

namespace TheOmenDen.TestRunner.Services;
public class TestContextService: ITestContextService
{
    private readonly _IReflectionAttributeInfo _attributeInfo;

    public TestContextService()
    {
        var attributeData = CustomAttributeData
            .GetCustomAttributes(typeof(FactAttribute))
            .First();

        _attributeInfo = new ReflectionAttributeInfo(attributeData);
    }
    
    public async IAsyncEnumerable<IXunitTestCase> DiscoverTestCasesAsync(Stream stream)
    {

        _ITestClass testClass = default;

        var testCases = new List<IXunitTestCase>();

        foreach (var method in testClass.Class.GetMethods(true))
        {
            var testMethod = new TestMethod(testClass, method);

            var discoverer = new FactDiscoverer();

            testCases = (await discoverer.Discover(_TestFrameworkOptions.ForDiscovery(), testMethod, _attributeInfo))
                .ToList();
        }

        if (!testCases.Any())
        {
            yield break;
        }

        foreach (var testCase in testCases)
        {
            yield return testCase;
        }
    }
}