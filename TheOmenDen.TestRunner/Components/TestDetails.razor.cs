using Xunit.Runner.Common;
using Xunit.Runner.v2;

namespace TheOmenDen.TestRunner.Components;

public partial class TestDetails: ComponentBase
{
    [Parameter] public TestClass? TestClass { get; set; }
    [Parameter] public Xunit3MethodInfo? MethodInformation { get; set; }

    [Parameter] public Xunit3AttributeInfo? AttributeInfo { get; set; }

    private readonly FactDiscoverer _factDiscoverer;

    private IEnumerable<IXunitTestCase> _xunitTestCases = Enumerable.Empty<IXunitTestCase>();

    protected override async Task OnInitializedAsync()
    {
        var options = _TestFrameworkOptions.ForDiscovery();

        var methodInfo = MethodInformation;

        var testMethod = new TestMethod(TestClass,MethodInformation);
        
        _xunitTestCases = await _factDiscoverer.Discover(options, testMethod, AttributeInfo);
    }

}
