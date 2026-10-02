internal static class Check
{
    internal static int Count { get; private set; }
    internal static void That(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Count++;
    }
    internal static void Equal<T>(T actual, T expected, string description)
    {
        That(EqualityComparer<T>.Default.Equals(actual, expected), $"{description}; expected {expected}, actual {actual}");
    }
    internal static void Group(string name) => Console.WriteLine($"PASS {name}");
}
