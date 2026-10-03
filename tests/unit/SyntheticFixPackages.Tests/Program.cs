using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RecommendationGuidance;
using SyntheticFixPackages;

internal static class Program
{
    private static int checks;
    private static string last = "startup";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    internal const string Hostile = "<script>alert('x')</script> \0\r\né e\u0301 😀 \" + & ` =SUM(A1)\nhttps://invalid.example/evil";

    private static int Main()
    {
        try
        {
            Goldens();
            Integrity();
            StatesAndIdentity();
            IsolationAndActualFields();
            Bounds();
            Console.WriteLine($"PASS: {checks} substantive fictional fix-package assertions. No review, task, export, authority or execution.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {last}; {exception.GetType().Name}. No fixture payload logged.");
            return 1;
        }
    }

    private static void Goldens()
    {
        using var digests = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "golden-digests.json")));
        foreach (var name in new[] { "normal", "empty", "multi", "hostile" })
        {
            var guidance = Guide(Input(name));
            var value = Package(guidance);
            var literal = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name + "-golden.json"));
            Equal(literal, FixPackageBuilder.CanonicalPayload(value), name + " full independent canonical bytes");
            Equal(Encoding.UTF8.GetString(literal), value.CanonicalJson, name + " exact cached canonical text");
            var expected = digests.RootElement.GetProperty(name);
            Equal(expected.GetProperty("contentDigest").GetString(), value.ContentDigest, name + " independent whole digest");
            Equal(expected.GetProperty("guidanceDigest").GetString(), value.Guidance.ContentDigest, name + " retained independent upstream digest");
            Equal(expected.GetProperty("templateDigest").GetString(), value.TemplateDigest, name + " independent whole catalog digest");
            Equal(expected.GetProperty("canonicalBytes").GetInt32(), literal.Length, name + " literal byte length");
            using var document = JsonDocument.Parse(literal);
            // An independent recursive object oracle also verifies every typed property, including all nested upstream fields.
            Compare(document.RootElement, JsonSerializer.SerializeToElement(new
            {
                value.SchemaVersion,
                value.Status,
                value.Disclaimer,
                value.Guidance,
                value.TemplateVersion,
                value.TemplateDigest,
                value.Templates,
                value.Packages,
                value.Warnings,
                value.UnavailableSections
            }, Options), name);
            Equal(guidance.ContentDigest, value.Guidance.ContentDigest, name + " unchanged upstream content");
            Equal(RecommendationGuidanceBuilder.CanonicalPayload(guidance), RecommendationGuidanceBuilder.CanonicalPayload(value.Guidance), name + " complete upstream preserved");
            Equal(name == "empty" ? 0 : name == "multi" ? 2 : 1, value.Packages.Length, name + " group membership");
            Check(value.Packages.SelectMany(package => package.Options).SelectMany(option => option.Artifacts).All(artifact => artifact.Status == "Unverified"), name + " every artifact unverified");
        }
        var hostile = Package(Guide(Input("hostile")));
        Equal(Hostile, hostile.Guidance.Findings[0].OriginalTitle, "original UTF16/control text fidelity");
        Equal(Hostile, hostile.Guidance.Findings[0].PresentationTitle, "current UTF16/control text fidelity");
        Equal(Hostile, hostile.Guidance.Findings[0].Options[0].Text, "option UTF16/control text fidelity");
        var empty = Package(Guide(Input("empty")));
        Equal(4, empty.Warnings.Length, "explicit empty warning appended");
        Equal("No findings were supplied; no fix packages or actions are available.", empty.Warnings[^1], "no implicit healthy/action empty meaning");
    }

    private static void Integrity()
    {
        Denied(null, FixPackageIssue.MissingGuidance, "missing guidance");
        var good = Guide(Input("normal"));
        var f = good.Findings[0];
        var o = f.Options[0];
        var cases = new (string Name, GuidanceSnapshot Value, FixPackageIssue Issue)[]
        {
            ("schema", good with { SchemaVersion = "future" }, FixPackageIssue.IntegrityMismatch),
            ("status", good with { Status = "Reviewed" }, FixPackageIssue.IntegrityMismatch),
            ("wrong digest", good with { ContentDigest = new string('0', 64) }, FixPackageIssue.IntegrityMismatch),
            ("null digest", good with { ContentDigest = null! }, FixPackageIssue.IntegrityMismatch),
            ("warning omission", good with { Warnings = [] }, FixPackageIssue.IntegrityMismatch),
            ("warning replacement", good with { Warnings = ["Reviewed."] }, FixPackageIssue.IntegrityMismatch),
            ("unavailable omission", good with { UnavailableSections = [] }, FixPackageIssue.IntegrityMismatch),
            ("source null", good with { Source = null! }, FixPackageIssue.InvalidGuidance),
            ("scope null", good with { Source = good.Source with { Scope = null! } }, FixPackageIssue.InvalidGuidance),
            ("wrong scope", good with { Source = good.Source with { Scope = good.Source.Scope with { CustomerId = "other" } } }, FixPackageIssue.InvalidGuidance),
            ("run mismatch", good with { Source = good.Source with { RunId = Guid.NewGuid() } }, FixPackageIssue.InvalidGuidance),
            ("null findings", good with { Findings = default }, FixPackageIssue.InvalidGuidance),
            ("null warning collection", good with { Warnings = default }, FixPackageIssue.InvalidGuidance),
            ("null unavailable collection", good with { UnavailableSections = default }, FixPackageIssue.InvalidGuidance),
            ("null finding", good with { Findings = [null!] }, FixPackageIssue.InvalidGuidance),
            ("duplicate finding", good with { Findings = [f, f] }, FixPackageIssue.InvalidGuidance),
            ("null options", good with { Findings = [f with { Options = default }] }, FixPackageIssue.InvalidGuidance),
            ("null option", good with { Findings = [f with { Options = [null!] }] }, FixPackageIssue.InvalidGuidance),
            ("empty option", good with { Findings = [f with { Options = [] }] }, FixPackageIssue.InvalidGuidance),
            ("duplicate option", good with { Findings = [f with { Options = [o, o] }] }, FixPackageIssue.InvalidGuidance),
            ("null occurrences", good with { Findings = [f with { Occurrences = default }] }, FixPackageIssue.InvalidGuidance),
            ("null occurrence", good with { Findings = [f with { Occurrences = [null!] }] }, FixPackageIssue.InvalidGuidance),
            ("empty occurrence", good with { Findings = [f with { Occurrences = [] }] }, FixPackageIssue.InvalidGuidance),
            ("duplicate occurrence", good with { Findings = [f with { Occurrences = [f.Occurrences[0], f.Occurrences[0]] }] }, FixPackageIssue.InvalidGuidance),
            ("invalid current state", good with { Findings = [f with { CurrentState = "ValidatedClosed" }] }, FixPackageIssue.InvalidGuidance),
            ("null original", good with { Findings = [f with { OriginalTitle = null! }] }, FixPackageIssue.InvalidGuidance),
            ("null text entry", good with { Findings = [f with { ValidationGuidance = [null!] }] }, FixPackageIssue.InvalidGuidance),
            ("default text array", good with { Findings = [f with { Limitations = default }] }, FixPackageIssue.InvalidGuidance),
            ("bad frozen JSON", good with { Source = good.Source with { FrozenVersions = default } }, FixPackageIssue.InvalidGuidance),
            ("null capability JSON", good with { Source = good.Source with { CapabilityLock = GuidanceFixture.Json("null") } }, FixPackageIssue.InvalidGuidance),
            ("wrong analysis JSON kind", good with { Source = good.Source with { AnalysisLock = GuidanceFixture.Json("[]") } }, FixPackageIssue.InvalidGuidance),
            ("changed actual original", good with { Findings = [f with { OriginalTitle = "Substitution" }] }, FixPackageIssue.IntegrityMismatch),
            ("changed actual option", good with { Findings = [f with { Options = [o with { Text = "Substitution" }] }] }, FixPackageIssue.IntegrityMismatch),
            ("changed actual citation", good with { Findings = [f with { Occurrences = [f.Occurrences[0] with { EvidenceReference = "other" }] }] }, FixPackageIssue.IntegrityMismatch),
            ("changed option identity", good with { Findings = [f with { Options = [o with { ScopedOptionId = new string('0', 64) }] }] }, FixPackageIssue.IntegrityMismatch),
            ("forged option review", good with { Findings = [f with { Options = [o with { Status = "Reviewed" }] }] }, FixPackageIssue.IntegrityMismatch)
        };
        foreach (var item in cases) Denied(item.Value, item.Issue, item.Name);
        // Rehashing forged statuses/identities/warnings cannot legitimize a noncanonical guidance object.
        foreach (var item in cases.Where(item => item.Issue == FixPackageIssue.IntegrityMismatch && item.Name is not ("wrong digest" or "null digest" or "changed actual original" or "changed actual option" or "changed actual citation")))
            Denied(item.Value with { ContentDigest = Hash(RecommendationGuidanceBuilder.CanonicalPayload(item.Value)) }, FixPackageIssue.IntegrityMismatch, "rehashed " + item.Name);
        using var sourceDocument = JsonDocument.Parse(good.Source.FrozenVersions.GetRawText());
        var disposed = good with { Source = good.Source with { FrozenVersions = sourceDocument.RootElement } };
        sourceDocument.Dispose();
        Denied(disposed, FixPackageIssue.InvalidGuidance, "disposed source document");
        var duplicate = good.Source.FrozenVersions.GetRawText().Replace("{", "{\"profileVersion\":\"synthetic-profile-v1\",", StringComparison.Ordinal);
        Denied(good with { Source = good.Source with { FrozenVersions = GuidanceFixture.Json(duplicate) } }, FixPackageIssue.InvalidGuidance, "duplicate actual lock property");
    }

    private static void StatesAndIdentity()
    {
        var input = Input("normal");
        var initial = Package(Guide(input));
        foreach (var state in new[] { "Proposed", "Confirmed", "Rejected", "Deferred", "AutoConfirmed" })
        {
            var f = input.Findings[0] with { InitialState = state == "AutoConfirmed" ? "AutoConfirmed" : "Proposed", CurrentState = state, Severity = state == "AutoConfirmed" ? "Low" : "Critical" };
            var changed = Package(Guide(input with { Findings = [f] }));
            Equal(initial.Packages[0].PackageId, changed.Packages[0].PackageId, state + " stable package identity");
            Equal(initial.Packages[0].Options[0].ScopedOptionId, changed.Packages[0].Options[0].ScopedOptionId, state + " unchanged scoped option");
            Equal(initial.Packages[0].Options[0].Artifacts.Select(x => x.ArtifactId).ToArray(), changed.Packages[0].Options[0].Artifacts.Select(x => x.ArtifactId).ToArray(), state + " stable artifact identity");
            Check(changed.Packages[0].Options[0].Artifacts.All(x => x.Status == "Unverified"), state + " never approves artifacts");
            Equal(state, changed.Guidance.Findings[0].CurrentState, state + " preserves current finding state");
            if (state != "Confirmed") Check(initial.ContentDigest != changed.ContentDigest, state + " binds changed current source");
        }
        var revised = Package(Guide(input with { Findings = [input.Findings[0] with { PresentationTitle = "Edited current title", BusinessContext = "Edited context", FindingRevision = 2 }] }));
        Equal(initial.Packages[0].PackageId, revised.Packages[0].PackageId, "presentation edit cannot rename package");
        Check(initial.ContentDigest != revised.ContentDigest, "presentation edit changes complete snapshot digest");
        var nextId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var next = Package(Guide(input with { Source = input.Source with { RunId = nextId, ReviewRunId = nextId } }));
        Check(initial.Packages[0].PackageId != next.Packages[0].PackageId, "different run has distinct package identity");
        Check(initial.Packages[0].Options[0].Artifacts[0].ArtifactId != next.Packages[0].Options[0].Artifacts[0].ArtifactId, "different run has distinct artifact identity");
        var multi = Input("multi");
        var permuted = multi with { Findings = multi.Findings.Reverse().Select(f => f with { Occurrences = f.Occurrences.Reverse().ToImmutableArray(), Options = f.Options.Reverse().ToImmutableArray() }).ToImmutableArray() };
        var beforeCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Equal(Package(Guide(multi)).CanonicalJson, Package(Guide(permuted)).CanonicalJson, "identity-ordered input permutation and Turkish culture");
        }
        finally { CultureInfo.CurrentCulture = beforeCulture; }
        var result = Package(Guide(multi));
        Equal(2, result.Packages.Length, "identical root-cause prose does not merge groups");
        var optionIds = result.Guidance.Findings.SelectMany(f => f.Options).Where(o => o.OptionId == "inspect-fixture").Select(o => o.ScopedOptionId).ToArray();
        Equal(2, optionIds.Distinct(StringComparer.Ordinal).Count(), "repeated option names in separate groups remain distinct");
        Check(result.Packages[0].Options.Select(o => o.ScopedOptionId).SequenceEqual(result.Packages[0].Options.Select(o => o.ScopedOptionId).Order(StringComparer.Ordinal)), "output option order is scoped identity, not priority/name");
        var reordered = Guide(multi);
        reordered = reordered with { Findings = reordered.Findings.Reverse().ToImmutableArray() };
        reordered = reordered with { ContentDigest = Hash(RecommendationGuidanceBuilder.CanonicalPayload(reordered)) };
        Denied(reordered, FixPackageIssue.IntegrityMismatch, "noncanonical supplied finding ordering despite rehash");
    }

    private static void IsolationAndActualFields()
    {
        var original = Guide(Input("normal"));
        using var doc = JsonDocument.Parse(original.Source.FrozenVersions.GetRawText());
        var borrowed = original with { Source = original.Source with { FrozenVersions = doc.RootElement } };
        var snapshot = Package(borrowed);
        doc.Dispose();
        Equal("synthetic-profile-v1", snapshot.Guidance.Source.FrozenVersions.GetProperty("profileVersion").GetString(), "output independently clones borrowed source document");
        Check(!ReferenceEquals(original, snapshot.Guidance), "output owns rebuilt guidance record");
        var bytes = FixPackageBuilder.CanonicalPayload(snapshot);
        bytes[0] = 0;
        Equal((byte)'{', FixPackageBuilder.CanonicalPayload(snapshot)[0], "canonical output caller owns fresh array");
        Equal(snapshot.CanonicalJson, Package(Guide(Input("normal"))).CanonicalJson, "caller byte mutation cannot change returned snapshot");
        Check(typeof(FixPackageSnapshot).IsSealed, "snapshot sealed");
        Check(typeof(FixPackageSnapshot).GetConstructors().Length == 0, "no public snapshot factory");
        Check(typeof(FixPackageSnapshot).GetProperties().All(p => p.SetMethod is null), "snapshot properties get-only");
        var arguments = new object[] { snapshot.SchemaVersion, snapshot.Status, snapshot.Disclaimer, snapshot.Guidance,
            snapshot.TemplateVersion, snapshot.TemplateDigest, snapshot.Templates, snapshot.Packages, snapshot.Warnings,
            snapshot.UnavailableSections, snapshot.CanonicalJson, snapshot.ContentDigest };
        var ctor = typeof(FixPackageSnapshot).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var mutations = new (int Index, object Value, string Name)[]
        {
            (0,"forged","schema"),(1,"Reviewed","status"),(2,"approved","disclaimer"),
            (3,snapshot.Guidance with { ContentDigest = new string('0',64) },"actual nested guidance digest"),
            (4,"future","template version"),(5,new string('0',64),"template digest"),
            (6,ImmutableArray.Create(snapshot.Templates[0] with { Text = "changed" }),"template fields"),
            (7,ImmutableArray.Create(snapshot.Packages[0] with { FindingId = new string('0',64) }),"package finding membership"),
            (7,ImmutableArray.Create(snapshot.Packages[0] with { Options = [snapshot.Packages[0].Options[0] with { Artifacts = [snapshot.Packages[0].Options[0].Artifacts[0] with { Status = "Reviewed" }] }] }),"nested artifact status"),
            (8,ImmutableArray.Create("approved"),"warnings"),(9,ImmutableArray<string>.Empty,"unavailable sections")
        };
        foreach (var mutation in mutations)
        {
            var args = arguments.ToArray(); args[mutation.Index] = mutation.Value;
            var forged = (FixPackageSnapshot)ctor.Invoke(args);
            Check(!FixPackageBuilder.CanonicalPayload(snapshot).AsSpan().SequenceEqual(FixPackageBuilder.CanonicalPayload(forged)), "actual-property serializer detects " + mutation.Name);
            Equal(snapshot.CanonicalJson, forged.CanonicalJson, "forged cache deliberately unchanged " + mutation.Name);
        }
    }

    private static void Bounds()
    {
        var input = Input("normal");
        foreach (var length in new[] { 1, RecommendationGuidanceBuilder.MaximumTextLength })
        {
            var f = input.Findings[0] with { OriginalTitle = new string('<', length), PresentationTitle = new string('<', length) };
            var p = Package(Guide(input with { Findings = [f] }));
            Equal(f.OriginalTitle, p.Guidance.Findings[0].OriginalTitle, "legitimate maximum UTF16 prose preserved " + length);
        }
        var baseGuidance = Guide(input);
        Denied(baseGuidance with { Findings = [baseGuidance.Findings[0] with { PresentationTitle = new string('x', RecommendationGuidanceBuilder.MaximumTextLength + 1) }] }, FixPackageIssue.InvalidGuidance, "upstream UTF16 maximum exceeded");
        var groups = Enumerable.Range(1, 4).Select(i => input.Findings[0] with
        {
            FindingId = new string((char)('0' + i), 64),
            Occurrences = [input.Findings[0].Occurrences[0] with { OccurrenceId = new string((char)('4' + i), 64), ObjectId = "O" + i }],
            Options = Enumerable.Range(0, i == 4 ? 6249 : 6250).Select(j => new GuidanceOptionInput("o" + j.ToString("D5", CultureInfo.InvariantCulture), "X", "X", "X", "X")).ToImmutableArray()
        }).ToImmutableArray();
        var largest = Package(Guide(input with { Findings = groups }));
        Equal(100_000, largest.Packages.Length + largest.Packages.Sum(p => p.Options.Length + p.Options.Sum(o => o.Artifacts.Length)), "exact aggregate record boundary accepted");
        Check(FixPackageBuilder.CanonicalPayload(largest).Length <= FixPackageBuilder.MaximumCanonicalBytes, "accepted record boundary also satisfies byte bound");
        var tooMany = groups.SetItem(3, groups[3] with { Options = groups[3].Options.Add(new("extra", "X", "X", "X", "X")) });
        Denied(Guide(input with { Findings = tooMany }), FixPackageIssue.OutputTooLarge, "aggregate record boundary exceeded");
        // A legitimate upstream payload just below its cap must fail when the new complete envelope exceeds its own cap.
        var largeTexts = Enumerable.Repeat(new string('x', RecommendationGuidanceBuilder.MaximumTextLength), 2047).ToImmutableArray();
        var big = input with { Findings = [input.Findings[0] with { ValidationGuidance = largeTexts }] };
        var candidate = Guide(big);
        var padding = FixPackageBuilder.MaximumCanonicalBytes - 500 - RecommendationGuidanceBuilder.CanonicalPayload(candidate).Length - 3;
        Check(padding is > 0 and <= RecommendationGuidanceBuilder.MaximumTextLength, "byte-bound adversarial fixture remains within individual text limit");
        var nearLimit = Guide(big with { Findings = [big.Findings[0] with { ValidationGuidance = largeTexts.Add(new string('x', padding)) }] });
        Equal(FixPackageBuilder.MaximumCanonicalBytes - 500, RecommendationGuidanceBuilder.CanonicalPayload(nearLimit).Length, "upstream legitimate canonical byte boundary");
        Denied(nearLimit, FixPackageIssue.OutputTooLarge, "complete new envelope byte boundary exceeded");
    }

    private static GuidanceInput Input(string name)
    {
        var input = GuidanceFixture.Create();
        if (name == "empty") return input with { Findings = [] };
        if (name == "hostile") return input with { Findings = [input.Findings[0] with { OriginalTitle = Hostile, PresentationTitle = Hostile, BusinessContext = Hostile, Options = [input.Findings[0].Options[0] with { Text = Hostile }] }] };
        if (name != "multi") return input;
        var first = input.Findings[0] with
        {
            Options = input.Findings[0].Options.Add(new("compare-fixture", "Compare fictional evidence.", "A separate fixture run.", "No closure claim.", "Retain originals.")),
            Occurrences = input.Findings[0].Occurrences.Add(new(new string('f', 64), "OBJECT-2", "SyntheticControl", "SyntheticOperations", GuidanceFixture.Digest, "fixture-evidence:OBJECT-2"))
        };
        var other = input.Findings[0] with { FindingId = new string('d', 64), Occurrences = [new(new string('e', 64), "OBJECT-3", "SyntheticControl", "SyntheticSecurity", GuidanceFixture.Digest, "fixture-evidence:OBJECT-3")] };
        return input with { Findings = [first, other] };
    }

    private static GuidanceSnapshot Guide(GuidanceInput input)
    {
        var result = RecommendationGuidanceBuilder.Build(input);
        Check(result.Succeeded && result.Snapshot is not null && result.Issue is null, "fixture guidance successfully built");
        return result.Snapshot!;
    }
    private static FixPackageSnapshot Package(GuidanceSnapshot input)
    {
        var result = FixPackageBuilder.Build(input);
        Check(result.Succeeded && result.Snapshot is not null && result.Issue is null, "fix package successfully built");
        return result.Snapshot!;
    }
    private static void Denied(GuidanceSnapshot? input, FixPackageIssue issue, string code)
    {
        var result = FixPackageBuilder.Build(input);
        Check(!result.Succeeded && result.Snapshot is null && result.Issue == issue, "payload-free closed denial: " + code);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Check(bool condition, string code) { last = code; if (!condition) throw new InvalidOperationException(); checks++; }
    private static void Equal<T>(T expected, T actual, string code) => Check(EqualityComparer<T>.Default.Equals(expected, actual), code);
    private static void Equal<T>(T[] expected, T[] actual, string code) => Check(expected.SequenceEqual(actual), code);
    private static void Compare(JsonElement expected, JsonElement actual, string path)
    {
        Equal(expected.ValueKind, actual.ValueKind, path + " kind");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                Equal(expected.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray(), actual.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray(), path + " exact fields");
                foreach (var p in expected.EnumerateObject()) Compare(p.Value, actual.GetProperty(p.Name), path + "/" + p.Name);
                break;
            case JsonValueKind.Array:
                Equal(expected.GetArrayLength(), actual.GetArrayLength(), path + " exact ordered length");
                for (var i = 0; i < expected.GetArrayLength(); i++) Compare(expected[i], actual[i], path + "/" + i);
                break;
            case JsonValueKind.String: Equal(expected.GetString(), actual.GetString(), path + " literal"); break;
            default: Equal(expected.GetRawText(), actual.GetRawText(), path + " literal"); break;
        }
    }
}
