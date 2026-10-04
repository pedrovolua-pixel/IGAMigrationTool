using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using ReportPublication;

internal static class PreparationChecks
{
    internal static int Run()
    {
        var count = 0;
        foreach (var bundle in new[] { "completed", "warned", "source-limitation" })
        {
            var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "native-fixtures", bundle + ".source.json"));
            var node = JsonNode.Parse(bytes)!;
            var original = FrozenFixtureLoader.Source(node);
            var metadata = FixtureSourceDecoder.MetadataFor(node, original);
            var capture = FixtureSourceDecoder.Decode(bytes, metadata);
            Require(NativePublicationCanonicalV1.SourceBytes(capture).SequenceEqual(bytes)); count++;
            var digest = NativePublicationCanonicalV1.Hash(bytes);
            var supplied = bytes.ToArray(); var suppliedMetadata = metadata.ToArray();
            var owned = FixtureSourceDecoder.Decode(supplied, suppliedMetadata);
            Array.Clear(supplied); Array.Clear(suppliedMetadata);
            Require(NativePublicationCanonicalV1.Hash(NativePublicationCanonicalV1.SourceBytes(owned)) == digest); count++;
            Refuse(() => FixtureSourceDecoder.Decode(bytes, Encoding.UTF8.GetBytes("{\"sourceDigest\":\"" + digest + "\"," + Encoding.UTF8.GetString(metadata)[1..]))); count++;
            var bad = JsonNode.Parse(metadata)!; bad["sourceDigest"] = new string('0', 64);
            Refuse(() => FixtureSourceDecoder.Decode(bytes, FixtureSourceDecoder.CanonicalFixtureControl(bad))); count++;
            bad = JsonNode.Parse(metadata)!; bad["protectedReferences"] = new JsonArray();
            Refuse(() => FixtureSourceDecoder.Decode(bytes, FixtureSourceDecoder.CanonicalFixtureControl(bad))); count++;
            bad = JsonNode.Parse(metadata)!; bad["rawLocator"] = "FICTIONAL_RAW_LOCATOR_SENTINEL";
            Refuse(() => FixtureSourceDecoder.Decode(bytes, FixtureSourceDecoder.CanonicalFixtureControl(bad))); count++;
            bad = JsonNode.Parse(bytes)!; bad["score"]!["quality"]!["value"] = "99";
            var scoreOriginal = node["score"]!.ToJsonString();
            var scoreChanged = Encoding.UTF8.GetString(FixtureSourceDecoder.CanonicalFixtureControl(bad["score"]!));
            Refuse(() => FixtureSourceDecoder.Decode(ReplaceFirst(bytes, "\"score\":" + scoreOriginal, "\"score\":" + scoreChanged), metadata)); count++;
            bad = JsonNode.Parse(bytes)!; bad["inputs"]!["baselineDigest"] = new string('0', 64);
            var inputsChanged = Encoding.UTF8.GetString(FixtureSourceDecoder.CanonicalFixtureControl(bad["inputs"]!));
            Refuse(() => FixtureSourceDecoder.Decode(ReplaceFirst(bytes, "\"inputs\":" + node["inputs"]!.ToJsonString(), "\"inputs\":" + inputsChanged), metadata)); count++;
            bad = JsonNode.Parse(bytes)!; bad["warnings"]!.AsArray().Clear();
            if (bundle != "completed")
            {
                // The final root warnings occurrence follows source score; leave projection warnings untouched.
                var raw = Encoding.UTF8.GetString(bytes); var target = "\"warnings\":" + node["warnings"]!.ToJsonString();
                var index = raw.LastIndexOf(target, StringComparison.Ordinal); Require(index >= 0);
                var changed = raw[..index] + "\"warnings\":[]" + raw[(index + target.Length)..];
                Refuse(() => FixtureSourceDecoder.Decode(Encoding.UTF8.GetBytes(changed), metadata)); count++;
            }
        }
        var clock = new FixtureClock(DateTimeOffset.Parse("2026-10-03T12:00:00.0000000Z", System.Globalization.CultureInfo.InvariantCulture));
        var fired = 0;
        using (clock.CreateTimer(_ => fired++, null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan))
        {
            clock.Advance(TimeSpan.FromSeconds(1), TimeSpan.FromHours(-1)); Require(fired == 0); count++;
            clock.Advance(TimeSpan.FromSeconds(1), TimeSpan.Zero); Require(fired == 1); count++;
            clock.Advance(TimeSpan.FromSeconds(1)); Require(fired == 1); count++;
        }
        var marker = typeof(NativeReportPublisherV1).Assembly.GetType("ReportPublication.PublicationIntegrityException", true)!;
        Require(marker.IsSealed && marker.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Length == 1 && marker.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0].GetParameters().Length == 0); count++;
        var error = (Exception)Activator.CreateInstance(marker, true)!;
        Require(error.Message == "Publication integrity verification failed." && error.InnerException is null
            && marker.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length == 0); count++;
        Console.WriteLine($"PASS: {count} independent preparation decoder/ownership/time/fixed-marker checks; baseline marker visibility {(marker.IsPublic ? "public" : "internal")}.");
        return count;
    }
    private static byte[] ReplaceFirst(byte[] bytes, string target, string replacement)
    {
        var raw = Encoding.UTF8.GetString(bytes); var index = raw.IndexOf(target, StringComparison.Ordinal); Require(index >= 0);
        return Encoding.UTF8.GetBytes(raw[..index] + replacement + raw[(index + target.Length)..]);
    }
    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("Independent preparation assertion failed."); }
    private static void Refuse(Action action)
    {
        try { action(); }
        catch (PublicationIntegrityException) { return; }
        throw new InvalidOperationException("Independent decoder accepted malformed fictional commitment.");
    }
}
