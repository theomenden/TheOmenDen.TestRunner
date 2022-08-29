namespace TheOmenDen.TestRunner.Models;

public sealed record TestCase(String Assembly, String DisplayName, String Class, String Method, String Skip,
    IReadOnlyDictionary<String, IReadOnlyList<String>> Traits) : IDisplayTestCase;