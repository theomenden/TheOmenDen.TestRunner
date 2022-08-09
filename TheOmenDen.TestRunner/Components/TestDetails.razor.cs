namespace TheOmenDen.TestRunner.Components;

public partial class TestDetails: ComponentBase
{
    private IEnumerable<IXunitTestCase> _xUnitTestCases = Enumerable.Empty<IXunitTestCase>();

    protected override async Task OnInitializedAsync()
    {
    }

}
