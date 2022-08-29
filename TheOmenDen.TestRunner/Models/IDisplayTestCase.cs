namespace TheOmenDen.TestRunner.Models;
public interface IDisplayTestCase
{
    String Assembly { get; }
    String DisplayName { get; }
    String Class { get; }
    String Method { get; }
    String Skip { get; }
    IReadOnlyDictionary<String, IReadOnlyList<String>> Traits { get; }
}
