internal static class Check
{
    internal static int Count { get; private set; }
    internal static void That(bool value, string code)
    {
        Count++;
        if (!value) throw new InvalidOperationException("V8 independent assertion failed: " + code);
    }
    internal static void Equal<T>(T actual, T expected, string code) => That(EqualityComparer<T>.Default.Equals(actual, expected), code);
    internal static void Group(string code) => Console.WriteLine("PASS " + code);
}
