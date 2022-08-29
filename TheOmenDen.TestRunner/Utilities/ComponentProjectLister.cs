using System.Text.Json.Serialization;
using System.Text.Json;
using Xunit.Runner.Common;

namespace TheOmenDen.TestRunner.Utilities;
/// <summary>
/// List the test cases into the <see cref="Components.TestDetails"/>
/// </summary>
public static class ComponentProjectLister
{
    /// <summary>
	/// Retrieve a stream of the contents of the test cases to the component, based on the provided option and format.
	/// </summary>
    public static IAsyncEnumerable<TestCase> StreamTestCasesAsync<TTestCase>(
        IReadOnlyDictionary<string, List<TTestCase>> testCasesByAssembly,
        ListFormat listFormat = ListFormat.Text)
        where TTestCase : _ITestCaseMetadata
    {
        var cases = Full(testCasesByAssembly, listFormat);

        return cases;
    }


    private static IAsyncEnumerable<TestCase> Full<TTestCase>(
        IReadOnlyDictionary<string, List<TTestCase>> testCasesByAssembly,
        ListFormat format)
            where TTestCase : _ITestCaseMetadata
    {
        var fullTestCases =
            testCasesByAssembly
                .SelectMany(kvp => kvp.Value.Select(tc => new { assemblyFileName = kvp.Key, testCase = tc }))
                .Select(tuple => new TestCase(
                    tuple.assemblyFileName,
                    tuple.testCase.TestCaseDisplayName,
                    tuple.testCase.TestClassNameWithNamespace ?? String.Empty,
                    tuple.testCase.TestMethodName ?? String.Empty,
                    tuple.testCase.SkipReason ?? String.Empty,
                    tuple.testCase.Traits.Count > 0
                        ? tuple.testCase.Traits
                        : new Dictionary<string, IReadOnlyList<String>>(1)
                ))
                .OrderBy(x => x.Assembly)
                .ThenBy(x => x.DisplayName)
                .ToList();

        var jsonOptions = new JsonSerializerOptions { WriteIndented = format == ListFormat.Text, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        if (format == ListFormat.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(fullTestCases, jsonOptions));
        }

        return fullTestCases.ToAsyncEnumerable();
    }
}