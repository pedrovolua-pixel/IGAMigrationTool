internal static class Check
{
    internal static int Count { get; private set; }
    internal static void That(bool value, string message)
    {
        Count++;
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Equal<T>(T actual, T expected, string message) => That(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}, actual {actual}");
    internal static void Group(string name) => Console.WriteLine($"PASS {name}");
}
