using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using SyntheticFixPackages;
using SyntheticFixReview;
using SyntheticPlanningTasks;

internal static class SourceBoundaryCases
{
    private static FixPackageSnapshot Forge(FixPackageSnapshot value, string property, object? replacement)
    {
        // Reflection is exclusively for rejected adversarial inputs. Every
        // accepted artifact snapshot comes from the real owned database store.
        var clone = (FixPackageSnapshot)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(value, null)!;
        typeof(FixPackageSnapshot).GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(clone, replacement);
        return clone;
    }
    internal static async Task Run()
    {
        foreach (var name in new[] { "normal", "hostile", "empty", "source-b", "source-return", "run-later" })
        {
            // Same literal UUID, no registered source/task/event exists: reads
            // never seed a high-water mark and each literal remains independent.
            var fixture = new DomainFixture(name, Guid.Parse("12345678-1234-4234-8234-123456789abc"));
            await fixture.Initialize(); var before = await OwnedDatabase.FullRows(fixture.RunId);
            var read = await fixture.Read();
            Check.Equal(DatabaseCases.Canonical(read.Source!), File.ReadAllText(OracleCases.Fixture(name + "-binding-golden.json")), "actual-real-store-composition-full-independent-literal-task-binding");
            Check.Equal(DatabaseCases.Canonical(read.Options), File.ReadAllText(OracleCases.Fixture(name + "-options-golden.json")), "actual-real-store-composition-full-independent-literal-options-identities-vectors");
            Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), before, "literal-source-composition-does-not-register-any-row");
        }
        var normal = new DomainFixture(); await normal.Initialize(); var package = normal.Package();
        var actual = await normal.ArtifactStore().ReadAsync(normal.RunId, Policies.ArtifactConsultant);
        Check.That(actual.Succeeded, "negative-corpus-original-snapshot-obtained-from-real-store");
        var snapshot = actual.Snapshot!;
        Check.That(PlanningTaskSourceBuilder.Build(package, snapshot).Succeeded, "real-positive-source-build-before-rejection-corpus-no-fake-path");
        void Deny(FixPackageSnapshot? p, ArtifactReviewSnapshot? s)
        {
            var result = PlanningTaskSourceBuilder.Build(p, s);
            Check.That(result.Issue is not null && result.Source is null, "forged-source-or-artifact-proof-typed-denial-no-partial-source");
        }
        Deny(null, snapshot); Deny(package, null);
        foreach (var replacement in new (string Name, object? Value)[]
        {
            ("SchemaVersion", "foreign"), ("Status", "ReviewedForPlanning"), ("CanonicalJson", "{}"),
            ("ContentDigest", new string('0',64)), ("TemplateVersion", "foreign"), ("TemplateDigest", new string('0',64)),
            ("Warnings", ImmutableArray<string>.Empty), ("UnavailableSections", ImmutableArray<string>.Empty),
            ("Guidance", package.Guidance with { ContentDigest = new string('0',64) }),
            ("Packages", package.Packages.Select(p => p with { Options = p.Options.Select(o => o with { Artifacts = o.Artifacts[..2] }).ToImmutableArray() }).ToImmutableArray())
        }) Deny(Forge(package, replacement.Name, replacement.Value), snapshot);
        foreach (var bad in new[]
        {
            snapshot with { ActorId = "" }, snapshot with { Entries = default }, snapshot with { Entries = [] },
            snapshot with { Entries = snapshot.Entries.Reverse().ToImmutableArray() },
            snapshot with { Entries = snapshot.Entries.Select(e => e with { Revision = 1 }).ToImmutableArray() },
            snapshot with { Entries = snapshot.Entries.Select(e => e with { State = ArtifactReviewState.ReviewedForPlanning }).ToImmutableArray() },
            snapshot with { Entries = snapshot.Entries.Select(e => e with { Artifact = e.Artifact with { ArtifactTextDigest = new string('0',64) } }).ToImmutableArray() },
            snapshot with { Source = snapshot.Source with { Scope = new("foreign", "synthetic-project", "synthetic-environment") } },
            snapshot with { Source = snapshot.Source with { RunId = Guid.NewGuid() } },
            snapshot with { Source = snapshot.Source with { SourceDigest = new string('0',64) } },
            snapshot with { Source = snapshot.Source with { ContractDigest = new string('0',64) } },
            snapshot with { Source = snapshot.Source with { ProfileId = "synthetic-review-maturity-fix-review-equal-v1" } }
        }) Deny(package, bad);
        foreach (var identity in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(OracleCases.Fixture("identity-golden.json")))!)
        {
            var scope = JsonSerializer.Deserialize<PlanningTaskScope>(identity.GetProperty("scope"), V14Program.Web)!;
            Check.Equal(PlanningTaskSourceBuilder.TaskId(scope, identity.GetProperty("runId").GetGuid(), identity.GetProperty("findingId").GetString()!, identity.GetProperty("scopedOptionId").GetString()!), identity.GetProperty("taskId").GetString(), "actual-identity-helper-vs-preauthored-seven-coordinate-goldens");
        }
        Check.Group("TC14-T01/T04/T09 actual six full-byte literal sources and forged rejection corpus");
    }
}
