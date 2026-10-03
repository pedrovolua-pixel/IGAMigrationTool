using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RecommendationGuidance;
using SyntheticFixPackages;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string Root = AppContext.BaseDirectory;
    private static int checks, accepted, denials;
    private static string lastCode = "initialization";
    private static JsonElement expectations;
    private static readonly Dictionary<string, FixPackageSnapshot> fixtures = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, FixPackageHtmlSnapshot> previews = new(StringComparer.Ordinal);

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 0 && (args.Length != 2 || args[0] != "--write-previews" || !Path.IsPathFullyQualified(args[1])))
                throw new InvalidOperationException("invalid_cli");
            using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "expected-manifest.json")));
            expectations = manifest.RootElement;
            foreach (var name in new[] { "normal", "hostile", "empty" }) Compose(name);
            IdentityAndStateCases();
            MalformedCases();
            RendererForgeryCases();
            BoundsAndDetachedCases();
            if (args.Length == 2) WritePreviews(args[1]);
            Console.WriteLine($"PASS V11 independent composition; checks={checks}; accepted={accepted}; denials={denials}");
            Console.WriteLine($"PASS V11 normal canonical; bytes={Encoding.UTF8.GetByteCount(fixtures["normal"].CanonicalJson)}; sha256={fixtures["normal"].ContentDigest}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"FAIL V11; code={lastCode}; checks={checks}; exception={exception.GetType().Name}");
            return 1;
        }
    }

    private static GuidanceInput Input(string name) => JsonSerializer.Deserialize<GuidanceInput>(File.ReadAllBytes(Path.Combine(Root, name + "-input.json")), JsonOptions)!;
    private static GuidanceSnapshot Guidance(GuidanceInput input)
    {
        var result = RecommendationGuidanceBuilder.Build(input);
        Require(result.Succeeded && result.Snapshot is not null, "actual-guidance-builder-accepted");
        return result.Snapshot!;
    }
    private static FixPackageSnapshot Build(GuidanceSnapshot guidance)
    {
        var result = FixPackageBuilder.Build(guidance);
        Require(result.Succeeded && result.Snapshot is not null && result.Issue is null, "actual-fix-builder-accepted");
        accepted++;
        return result.Snapshot!;
    }
    private static FixPackageHtmlSnapshot Render(FixPackageSnapshot snapshot)
    {
        var result = FixPackageHtmlRenderer.Render(snapshot);
        Require(result.Succeeded && result.Snapshot is not null && result.Issue is null, "actual-renderer-accepted");
        return result.Snapshot!;
    }
    private static void Compose(string name)
    {
        var guidance = Guidance(Input(name));
        var expected = expectations.GetProperty("cases").GetProperty(name);
        var upstream = File.ReadAllBytes(Path.Combine(Root, name + "-guidance-golden.json"));
        Bytes(RecommendationGuidanceBuilder.CanonicalPayload(guidance), upstream, "full-independent-upstream-canonical");
        Equal(guidance.ContentDigest, expected.GetProperty("guidanceDigest").GetString(), "independent-upstream-digest");
        var snapshot = Build(guidance);
        var golden = File.ReadAllBytes(Path.Combine(Root, name + "-fix-golden.json"));
        Bytes(FixPackageBuilder.CanonicalPayload(snapshot), golden, "full-independent-fix-canonical");
        Bytes(Encoding.UTF8.GetBytes(snapshot.CanonicalJson), golden, "exact-cache-and-original-fields");
        Equal(snapshot.ContentDigest, expected.GetProperty("packageDigest").GetString(), "independent-fix-digest");
        Equal(snapshot.TemplateDigest, expectations.GetProperty("templateDigest").GetString(), "independent-template-digest");
        Equal(Hash(golden), snapshot.ContentDigest, "literal-byte-hash");
        Equal(snapshot.Status, "Unverified", "always-unverified-snapshot");
        Equal(snapshot.Guidance.ContentDigest, guidance.ContentDigest, "upstream-original-digest-preserved");
        Bytes(RecommendationGuidanceBuilder.CanonicalPayload(snapshot.Guidance), upstream, "complete-original-guidance-preserved");
        foreach (var package in snapshot.Packages)
        {
            var expectedPackage = expected.GetProperty("snapshot").GetProperty("packages").EnumerateArray().Single(item => item.GetProperty("findingId").GetString() == package.FindingId);
            Equal(package.PackageId, expectedPackage.GetProperty("packageId").GetString(), "independent-package-identity");
            Require(snapshot.Guidance.Findings.Any(finding => finding.FindingId == package.FindingId), "existing-finding-membership");
            foreach (var option in package.Options)
            {
                var expectedOption = expectedPackage.GetProperty("options").EnumerateArray().Single(item => item.GetProperty("scopedOptionId").GetString() == option.ScopedOptionId);
                Require(snapshot.Guidance.Findings.Single(f => f.FindingId == package.FindingId).Options.Any(o => o.ScopedOptionId == option.ScopedOptionId), "existing-scoped-option-membership");
                Equal(option.Artifacts.Length, 3, "exact-fixed-template-count");
                foreach (var artifact in option.Artifacts)
                {
                    var expectedArtifact = expectedOption.GetProperty("artifacts").EnumerateArray().Single(item => item.GetProperty("templateId").GetString() == artifact.TemplateId);
                    Equal(artifact.ArtifactId, expectedArtifact.GetProperty("artifactId").GetString(), "independent-artifact-identity");
                    Equal(artifact.Kind, expectedArtifact.GetProperty("kind").GetString(), "independent-artifact-kind");
                    Equal(artifact.Text, expectedArtifact.GetProperty("text").GetString(), "independent-fixed-template-text");
                    Equal(artifact.Status, "Unverified", "always-unverified-artifact");
                }
            }
        }
        var html = Render(snapshot);
        Bytes(Encoding.UTF8.GetBytes(html.Html), File.ReadAllBytes(Path.Combine(Root, name + "-html-golden.html")), "full-independent-html-canonical");
        Equal(html.PackageDigest, snapshot.ContentDigest, "actual-html-source-binding");
        Equal(html.ContentDigest, Hash(Encoding.UTF8.GetBytes(html.Html)), "exact-html-byte-digest");
        Equal(Render(snapshot).Html, html.Html, "actual-render-determinism");
        fixtures[name] = snapshot;
        previews[name] = html;
    }

    private static void IdentityAndStateCases()
    {
        var original = Input("normal");
        var normal = fixtures["normal"];
        Equal(normal.Packages.Length, 2, "same-root-cause-groups-not-merged");
        Equal(normal.Guidance.Findings[0].Options[0].OptionId, normal.Guidance.Findings[1].Options[0].OptionId, "repeated-option-labels-allowed-across-groups");
        Require(normal.Packages.SelectMany(p => p.Options).Select(o => o.ScopedOptionId).Distinct().Count() == 4, "scope-qualified-options-distinct");
        var permuted = original with
        {
            Findings = original.Findings.Reverse().Select(f => f with
            { Options = f.Options.Reverse().ToImmutableArray(), Occurrences = f.Occurrences.Reverse().ToImmutableArray() }).ToImmutableArray()
        };
        Equal(Build(Guidance(permuted)).CanonicalJson, normal.CanonicalJson, "independent-input-permutation-normalizes");
        foreach (var state in new[] { "Proposed", "Confirmed", "Rejected", "Deferred" })
        {
            var changed = Build(Guidance(original with
            {
                Findings = original.Findings.Select(f => f with
                { CurrentState = state, PresentationTitle = "Changed fictional presentation", BusinessContext = "Changed fictional context", FindingRevision = 3 }).ToImmutableArray()
            }));
            Require(changed.ContentDigest != normal.ContentDigest && changed.Guidance.ContentDigest != normal.Guidance.ContentDigest, "presentation-source-and-snapshot-digest-change");
            for (var i = 0; i < normal.Packages.Length; i++)
            {
                Equal(changed.Packages[i].PackageId, normal.Packages[i].PackageId, "package-identity-stable-across-review");
                Equal(changed.Guidance.Findings[i].OriginalTitle, normal.Guidance.Findings[i].OriginalTitle, "generated-original-preserved");
                Require(changed.Guidance.Findings[i].Occurrences.SequenceEqual(normal.Guidance.Findings[i].Occurrences), "occurrence-provenance-preserved");
                for (var j = 0; j < normal.Packages[i].Options.Length; j++)
                {
                    Equal(changed.Packages[i].Options[j].ScopedOptionId, normal.Packages[i].Options[j].ScopedOptionId, "option-identity-stable-across-review");
                    Require(changed.Packages[i].Options[j].Artifacts.SequenceEqual(normal.Packages[i].Options[j].Artifacts), "review-never-approves-or-alters-artifacts");
                }
            }
            _ = Render(changed);
        }
        var auto = original.Findings[1] with { InitialState = "AutoConfirmed", CurrentState = "AutoConfirmed" };
        Require(Build(Guidance(original with { Findings = [auto] })).Packages.All(p => p.Options.All(o => o.Artifacts.All(a => a.Status == "Unverified"))), "autoconfirmation-not-artifact-review");
        var empty = fixtures["empty"];
        Equal(empty.Packages.Length, 0, "no-invented-package-for-empty-guidance");
        Require(empty.Warnings.Last() == "No findings were supplied; no fix packages or actions are available.", "empty-explicit-no-action-warning");
    }

    private static void Deny(GuidanceSnapshot? value, FixPackageIssue? expected = null)
    {
        var result = FixPackageBuilder.Build(value);
        Require(!result.Succeeded && result.Snapshot is null && result.Issue is not null, "typed-fail-closed-builder-denial");
        if (expected is not null) Equal(result.Issue, expected, "precise-builder-denial-code");
        denials++;
    }
    private static void MalformedCases()
    {
        var g = fixtures["normal"].Guidance;
        Deny(null, FixPackageIssue.MissingGuidance);
        foreach (var value in new[]
        {
            g with { SchemaVersion = "unknown" }, g with { Status = "Approved" }, g with { ContentDigest = new string('0', 64) },
            g with { Warnings = g.Warnings.Add("Forged warning") }, g with { UnavailableSections = [] },
            g with { Findings = g.Findings.Reverse().ToImmutableArray() },
            g with { Findings = [g.Findings[0] with { Options = g.Findings[0].Options.Reverse().ToImmutableArray() }, g.Findings[1]] },
            g with { Findings = [g.Findings[0] with { Options = [g.Findings[0].Options[0] with { Status = "Verified" }] }, g.Findings[1]] },
            g with { Findings = [g.Findings[0] with { Options = [g.Findings[0].Options[0] with { ScopedOptionId = new string('0', 64) }] }, g.Findings[1]] },
            g with { Source = null! }, g with { Findings = default }, g with { Warnings = default }, g with { UnavailableSections = default },
            g with { Findings = [null!] }, g with { Findings = [g.Findings[0] with { Options = default }] },
            g with { Findings = [g.Findings[0] with { Options = [null!] }] },
            g with { Findings = [g.Findings[0] with { Occurrences = [null!] }] },
            g with { Findings = [g.Findings[0] with { Occurrences = default }] },
            g with { Findings = [g.Findings[0] with { ValidationGuidance = default }] },
            g with { Findings = [g.Findings[0] with { Assumptions = [null!] }] },
            g with { Source = g.Source with { Scope = new("foreign", "synthetic-project", "synthetic-environment") } },
            g with { Source = g.Source with { RunId = Guid.Empty } }, g with { Source = g.Source with { ReviewRunRevision = 18 } },
            g with { Source = g.Source with { RunInputDigest = "malformed" } }, g with { Source = g.Source with { ProfileId = "foreign" } },
            g with { Source = g.Source with { FrozenVersions = default } },
            g with { Source = g.Source with { CapabilityLock = Json("{}") } },
            g with { Source = g.Source with { AnalysisLock = Json("{}") } },
            g with { Findings = [g.Findings[0] with { CurrentState = "ValidatedClosed" }] },
            g with { Findings = [g.Findings[0] with { OriginalTitle = new string('x', 16385) }] }
        }) Deny(value);
        // Rehashing a forged declaration must not make it valid.
        var forged = g with { Status = "Approved" };
        Deny(forged with { ContentDigest = Hash(RecommendationGuidanceBuilder.CanonicalPayload(forged)) }, FixPackageIssue.IntegrityMismatch);
    }

    private static FixPackageSnapshot Forge(FixPackageSnapshot source, string field, object? value)
    {
        var constructor = typeof(FixPackageSnapshot).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var arguments = constructor.GetParameters().Select(parameter =>
            string.Equals(parameter.Name, field, StringComparison.OrdinalIgnoreCase) ? value :
            typeof(FixPackageSnapshot).GetProperties().Single(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)).GetValue(source)).ToArray();
        return (FixPackageSnapshot)constructor.Invoke(arguments);
    }
    private static void RejectRender(FixPackageSnapshot? source)
    {
        var result = FixPackageHtmlRenderer.Render(source);
        Require(!result.Succeeded && result.Snapshot is null && result.Issue is not null, "typed-fail-closed-renderer-denial");
        denials++;
    }
    private static void RendererForgeryCases()
    {
        var source = fixtures["normal"];
        RejectRender(null);
        foreach (var field in new[] { "SchemaVersion", "Status", "Disclaimer", "TemplateVersion", "TemplateDigest", "CanonicalJson", "ContentDigest" })
        {
            RejectRender(Forge(source, field, "forged"));
            RejectRender(Forge(source, field, null));
        }
        RejectRender(Forge(source, "Guidance", null));
        RejectRender(Forge(source, "Guidance", source.Guidance with { ContentDigest = new string('0', 64) }));
        RejectRender(Forge(source, "Templates", source.Templates.SetItem(0, source.Templates[0] with { Text = "forged executable" })));
        RejectRender(Forge(source, "Templates", default(ImmutableArray<FixTemplate>)));
        RejectRender(Forge(source, "Templates", ImmutableArray.Create((FixTemplate)null!)));
        RejectRender(Forge(source, "Packages", default(ImmutableArray<FixPackage>)));
        RejectRender(Forge(source, "Packages", ImmutableArray.Create((FixPackage)null!)));
        RejectRender(Forge(source, "Packages", source.Packages.Reverse().ToImmutableArray()));
        RejectRender(Forge(source, "Packages", source.Packages.SetItem(0, source.Packages[0] with { PackageId = new string('0', 64) })));
        RejectRender(Forge(source, "Packages", source.Packages.SetItem(0, source.Packages[0] with { FindingId = new string('0', 64) })));
        RejectRender(Forge(source, "Packages", source.Packages.SetItem(0, source.Packages[0] with { Options = [] })));
        RejectRender(Forge(source, "Warnings", source.Warnings.Add("forged")));
        RejectRender(Forge(source, "UnavailableSections", ImmutableArray<string>.Empty));
        var altered = Forge(source, "Disclaimer", "Coherently rehashed forged disclaimer");
        var bytes = FixPackageBuilder.CanonicalPayload(altered);
        altered = Forge(Forge(altered, "CanonicalJson", Encoding.UTF8.GetString(bytes)), "ContentDigest", Hash(bytes));
        RejectRender(altered);
        Require(FixPackageBuilder.CanonicalPayload(altered).AsSpan().SequenceEqual(bytes), "actual-properties-canonicalized-independent-of-cache");
        Require(typeof(FixPackageSnapshot).GetConstructors().Length == 0 && typeof(FixPackageSnapshot).GetProperties().All(p => p.SetMethod is null), "closed-get-only-snapshot-factory");
    }

    private static void BoundsAndDetachedCases()
    {
        var input = Input("normal");
        var guidance = fixtures["normal"].Guidance;
        var option = guidance.Findings[0].Options[0];
        // 1 package + 25000 options + 75000 artifacts exceeds the closed 100000-record bound.
        Deny(guidance with { Findings = [guidance.Findings[0] with { Options = Enumerable.Range(0, 25000).Select(i => option with { OptionId = "bound-" + i }).ToImmutableArray() }] }, FixPackageIssue.OutputTooLarge);
        var exactText = new string('x', 16384);
        var boundaryInput = input with { Findings = [input.Findings[0] with { OriginalTitle = exactText }] };
        Equal(Build(Guidance(boundaryInput)).Guidance.Findings[0].OriginalTitle.Length, 16384, "exact-upstream-UTF16-text-boundary-preserved");
        var expanded = input with
        {
            Findings = [input.Findings[0] with
        {
            Options = Enumerable.Range(0, 508).Select(i => new GuidanceOptionInput("byte-bound-" + i, exactText, exactText, exactText, exactText)).ToImmutableArray()
        }]
        };
        var largeGuidance = Guidance(expanded);
        Require(RecommendationGuidanceBuilder.CanonicalPayload(largeGuidance).Length <= 32 * 1024 * 1024, "large-upstream-valid-before-expansion");
        Deny(largeGuidance, FixPackageIssue.OutputTooLarge);
        var bytes = FixPackageBuilder.CanonicalPayload(fixtures["normal"]);
        bytes[0] ^= 1;
        Equal(FixPackageBuilder.CanonicalPayload(fixtures["normal"])[0], (byte)'{', "caller-byte-array-mutation-detached");
        using var document = JsonDocument.Parse(input.Source.FrozenVersions.GetRawText());
        var detached = Build(Guidance(input with { Source = input.Source with { FrozenVersions = document.RootElement } }));
        document.Dispose();
        Equal(detached.CanonicalJson, fixtures["normal"].CanonicalJson, "disposed-caller-JsonDocument-preserved");
        Equal(Render(detached).Html, previews["normal"].Html, "detached-output-still-renderable");
        var originalCaller = input.Findings.ToArray();
        originalCaller[0] = originalCaller[0] with { OriginalTitle = "Caller changed after composition" };
        Require(fixtures["normal"].Guidance.Findings[0].OriginalTitle != originalCaller[0].OriginalTitle, "original-value-not-caller-mutated");
    }
    private static void WritePreviews(string directory)
    {
        Directory.CreateDirectory(directory);
        var files = new List<object>();
        foreach (var name in new[] { "normal", "hostile", "empty" })
        {
            var preview = previews[name];
            File.WriteAllText(Path.Combine(directory, name + ".html"), preview.Html, new UTF8Encoding(false));
            files.Add(new { name = name + ".html", sha256 = preview.ContentDigest, packageDigest = preview.PackageDigest });
        }
        var output = new { schemaVersion = "v11-generated-preview-manifest-v1", expected = expectations, files };
        File.WriteAllText(Path.Combine(directory, "preview-manifest.json"), JsonSerializer.Serialize(output, JsonOptions), new UTF8Encoding(false));
        Console.WriteLine("PASS V11 generated fixed previews; count=3");
    }
    private static JsonElement Json(string value) { using var document = JsonDocument.Parse(value); return document.RootElement.Clone(); }
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private static void Bytes(byte[] actual, byte[] expected, string code)
    {
        if (!actual.AsSpan().SequenceEqual(expected))
        {
            var index = Enumerable.Range(0, Math.Min(actual.Length, expected.Length)).FirstOrDefault(i => actual[i] != expected[i], -1);
            Console.WriteLine($"DIAGNOSTIC V11 byte-mismatch; code={code}; offset={index}; actual-count={actual.Length}; expected-count={expected.Length}");
            if (code == "full-independent-html-canonical") File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "iga-cycle11-v11-html-diagnostic.html"), actual);
        }
        Require(actual.AsSpan().SequenceEqual(expected), code);
    }
    private static void Equal<T>(T actual, T expected, string code) => Require(EqualityComparer<T>.Default.Equals(actual, expected), code);
    private static void Require(bool value, string code)
    {
        lastCode = code;
        if (!value) throw new InvalidOperationException(code);
        checks++;
    }
}
