using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SyntheticEvaluation;

/// <summary>Freezes supplied synthetic general-AI populations; establishes no live authority.</summary>
public static class EvaluationSampler
{
    public const string AlgorithmVersion = "synthetic-evaluation-sampling-v1";
    private const int MaximumEntries = 100_000;

    public static SamplingResult Build(SamplingInput? input)
    {
        if (input?.Versions?.Bindings is null || input.Members is null
            || input.Members.Count > MaximumEntries)
        {
            return Deny(SamplingIssue.InvalidInput);
        }

        // Reject malformed manifest cardinality without enumerating or copying its entries.
        if (input.Versions.Bindings.Count != Enum.GetValues<SamplingVersionKind>().Length)
        {
            return Deny(SamplingIssue.InvalidVersion);
        }

        var members = input.Members.ToArray();
        var bindings = input.Versions.Bindings.ToArray();
        if (members.Length > MaximumEntries || members.Any(member => member is null)
            || bindings.Any(binding => binding is null))
        {
            return Deny(SamplingIssue.InvalidInput);
        }

        var secondaryCount = 0L;
        for (var i = 0; i < members.Length; i++)
        {
            var secondary = members[i].SecondaryModuleIds;
            if (secondary is null || secondary.Count > MaximumEntries)
            {
                return Deny(SamplingIssue.InvalidInput);
            }

            var detached = secondary.ToArray();
            secondaryCount += detached.Length;
            if (secondaryCount > MaximumEntries || detached.Any(id => id is null))
            {
                return Deny(SamplingIssue.InvalidInput);
            }

            members[i] = members[i] with { SecondaryModuleIds = Array.AsReadOnly(detached) };
        }

        if (!Reference(input.SampleId) || !Reference(input.PopulationId) || !Reference(input.ScopeId))
        {
            return Deny(SamplingIssue.InvalidReference);
        }

        if (!Digest(input.Seed) || input.Versions.CorrectionCutoffUtc.Offset != TimeSpan.Zero
            || input.Versions.EvaluationDateUtc.Offset != TimeSpan.Zero
            || input.Versions.PredecessorSampleDigest is { } predecessor && !Digest(predecessor)
            || bindings.Length != Enum.GetValues<SamplingVersionKind>().Length
            || bindings.Any(binding => !Enum.IsDefined(binding.Kind) || !Reference(binding.Version))
            || bindings.Select(binding => binding.Kind).Distinct().Count() != bindings.Length)
        {
            return Deny(SamplingIssue.InvalidVersion);
        }

        if (input.HardReviewBudget is < 0)
        {
            return Deny(SamplingIssue.InvalidBudget);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (!Reference(member.Id) || !Reference(member.ScopeId) || !Reference(member.EnvironmentId)
                || !Reference(member.PrimaryModuleId) || !Reference(member.CategoryId)
                || !Reference(member.RuleVersion) || !Reference(member.ModelPromptVersion)
                || !Reference(member.ConfidenceBandId)
                || member.UnavailableReason is { } reason && !Reference(reason)
                || member.SecondaryModuleIds.Any(id => !Reference(id)))
            {
                return Deny(SamplingIssue.InvalidReference);
            }

            if (!Enum.IsDefined(member.Severity) || member.ScopeId != input.ScopeId
                || member.Available != (member.UnavailableReason is null)
                || member.SecondaryModuleIds.Contains(member.PrimaryModuleId, StringComparer.Ordinal)
                || member.SecondaryModuleIds.Distinct(StringComparer.Ordinal).Count() != member.SecondaryModuleIds.Count)
            {
                return Deny(SamplingIssue.InvalidMember);
            }

            if (!member.PrimarySettled)
            {
                return Deny(SamplingIssue.AmbiguousPrimary);
            }

            if (!ids.Add(member.Id))
            {
                return Deny(SamplingIssue.DuplicateMember);
            }

            members[i] = member with
            {
                SecondaryModuleIds = Array.AsReadOnly(member.SecondaryModuleIds.Order(StringComparer.Ordinal).ToArray())
            };
        }

        Array.Sort(members, (a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
        Array.Sort(bindings, (a, b) => a.Kind.CompareTo(b.Kind));
        var versions = input.Versions with { Bindings = Array.AsReadOnly(bindings) };
        var frozen = input with { Versions = versions, Members = Array.AsReadOnly(members) };
        var groups = members.Where(member => !Mandatory(member)).GroupBy(Key.From)
            .OrderBy(group => group.Key, KeyComparer.Instance).ToArray();
        var high = members.Count(Mandatory);
        var lower = members.Length - high;
        var quota = members.Length < 100 ? lower : Math.Min(lower,
            Math.Max(groups.Length, Math.Max(Math.Max(0, 100 - high), high >= 100 ? 100 : 0)));
        if (input.HardReviewBudget is { } budget && budget < high + quota)
        {
            return Deny(SamplingIssue.InsufficientBudget);
        }

        var allocations = Allocate(groups.Select(group => group.Count()).ToArray(), quota);
        var strata = groups.Select((group, i) => new SamplingStratum(group.Key.EnvironmentId,
            group.Key.PrimaryModuleId, group.Key.CategoryId, group.Key.Severity, group.Count(), allocations[i])).ToArray();
        var versionBytes = Write(writer => WriteVersions(writer, versions));
        var versionDigest = Hash(versionBytes);
        var populationBytes = Write(writer => WritePopulation(writer, frozen, versionDigest));
        var populationDigest = Hash(populationBytes);
        var seed = Convert.FromHexString(input.Seed);
        var ranks = new List<SamplingRank>(lower);
        var selected = members.Where(Mandatory).Select(member => new SamplingSelection(member, true, null)).ToList();
        for (var i = 0; i < groups.Length; i++)
        {
            var ranked = groups[i].Select(member => (Member: member,
                Rank: new SamplingRank(member.Id, Rank(seed, frozen.ScopeId, populationDigest, member))))
                .OrderBy(pair => pair.Rank.RankDigest, StringComparer.Ordinal)
                .ThenBy(pair => pair.Member.Id, StringComparer.Ordinal).ToArray();
            ranks.AddRange(ranked.Select(pair => pair.Rank));
            selected.AddRange(ranked.Take(allocations[i]).Select(pair => new SamplingSelection(pair.Member, false, pair.Rank.RankDigest)));
        }

        var orderedRanks = ranks.OrderBy(rank => rank.MemberId, StringComparer.Ordinal).ToArray();
        var orderedSelected = selected.OrderBy(selection => selection.Member.Id, StringComparer.Ordinal).ToArray();
        var sampleBytes = Write(writer => WriteSample(writer, frozen, populationDigest, versionDigest,
            strata, orderedRanks, orderedSelected));
        return new SamplingResult(null, new SamplingProjection(frozen, strata, orderedRanks, orderedSelected,
            Encoding.UTF8.GetString(versionBytes), versionDigest, Encoding.UTF8.GetString(populationBytes),
            populationDigest, Encoding.UTF8.GetString(sampleBytes), Hash(sampleBytes)));
    }

    private static SamplingResult Deny(SamplingIssue issue) => new(issue, null);
    private static bool Mandatory(SamplingMember member) => member.Severity is SamplingSeverity.Critical or SamplingSeverity.High;
    private static bool Reference(string? value) => value is { Length: > 10 and <= 128 }
        && value.StartsWith("synthetic-", StringComparison.Ordinal)
        && value.AsSpan(10).ContainsAnyExcept("abcdefghijklmnopqrstuvwxyz0123456789._-".AsSpan()) == false;
    private static bool Digest(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static int[] Allocate(int[] sizes, int quota)
    {
        if (quota == sizes.Sum())
        {
            return (int[])sizes.Clone();
        }

        var allocations = Enumerable.Repeat(1, sizes.Length).ToArray();
        var residual = quota - sizes.Length;
        if (residual == 0)
        {
            return allocations;
        }

        var capacity = sizes.Sum() - sizes.Length;
        var remainders = new long[sizes.Length];
        for (var i = 0; i < sizes.Length; i++)
        {
            var product = (long)residual * (sizes[i] - 1);
            allocations[i] += (int)(product / capacity);
            remainders[i] = product % capacity;
        }

        foreach (var i in Enumerable.Range(0, sizes.Length).OrderByDescending(i => remainders[i]).ThenBy(i => i)
            .Take(quota - allocations.Sum()))
        {
            allocations[i]++;
        }

        return allocations;
    }

    private static string Rank(byte[] seed, string scope, string populationDigest, SamplingMember member)
    {
        using var bytes = new MemoryStream();
        bytes.Write(Encoding.ASCII.GetBytes("iga.synthetic-evaluation.sample-rank.v1\0"));
        bytes.Write(seed);
        Span<byte> length = stackalloc byte[4];
        foreach (var text in new[] { scope, populationDigest, member.EnvironmentId, member.PrimaryModuleId,
            member.CategoryId, member.Severity.ToString(), member.Id })
        {
            var encoded = Encoding.UTF8.GetBytes(text);
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)encoded.Length);
            bytes.Write(length);
            bytes.Write(encoded);
        }

        return Hash(bytes.ToArray());
    }

    private static byte[] Write(Action<Utf8JsonWriter> action)
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            action(writer);
        }

        return bytes.ToArray();
    }

    private static void WriteVersions(Utf8JsonWriter writer, SamplingVersionManifest versions)
    {
        writer.WriteStartObject();
        writer.WriteString("schema", "synthetic-evaluation-sampling-versions-v1");
        writer.WriteStartArray("bindings");
        foreach (var binding in versions.Bindings)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", binding.Kind.ToString());
            writer.WriteString("version", binding.Version);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteString("correctionCutoffUtc", versions.CorrectionCutoffUtc.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("evaluationDateUtc", versions.EvaluationDateUtc.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("predecessorSampleDigest", versions.PredecessorSampleDigest);
        writer.WriteEndObject();
    }

    private static void WritePopulation(Utf8JsonWriter writer, SamplingInput input, string versionDigest)
    {
        writer.WriteStartObject();
        writer.WriteString("schema", "synthetic-evaluation-sampling-population-v1");
        writer.WriteString("populationId", input.PopulationId);
        writer.WriteString("scopeId", input.ScopeId);
        writer.WriteString("versionManifestDigest", versionDigest);
        writer.WriteStartArray("members");
        foreach (var member in input.Members)
        {
            writer.WriteStartObject();
            writer.WriteString("id", member.Id);
            writer.WriteString("scopeId", member.ScopeId);
            writer.WriteString("environmentId", member.EnvironmentId);
            writer.WriteString("primaryModuleId", member.PrimaryModuleId);
            writer.WriteString("categoryId", member.CategoryId);
            writer.WriteString("ruleVersion", member.RuleVersion);
            writer.WriteString("modelPromptVersion", member.ModelPromptVersion);
            writer.WriteString("confidenceBandId", member.ConfidenceBandId);
            writer.WriteString("severity", member.Severity.ToString());
            writer.WriteBoolean("primarySettled", member.PrimarySettled);
            writer.WriteBoolean("available", member.Available);
            writer.WriteString("unavailableReason", member.UnavailableReason);
            writer.WriteStartArray("secondaryModuleIds");
            foreach (var secondary in member.SecondaryModuleIds)
            {
                writer.WriteStringValue(secondary);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSample(Utf8JsonWriter writer, SamplingInput input, string populationDigest,
        string versionDigest, SamplingStratum[] strata, SamplingRank[] ranks, SamplingSelection[] selected)
    {
        writer.WriteStartObject();
        writer.WriteString("schema", AlgorithmVersion);
        writer.WriteString("sampleId", input.SampleId);
        writer.WriteString("populationId", input.PopulationId);
        writer.WriteString("scopeId", input.ScopeId);
        writer.WriteString("populationDigest", populationDigest);
        writer.WriteString("versionManifestDigest", versionDigest);
        writer.WriteString("seed", input.Seed);
        if (input.HardReviewBudget is { } budget) { writer.WriteNumber("hardReviewBudget", budget); }
        else { writer.WriteNull("hardReviewBudget"); }
        writer.WriteStartArray("strata");
        foreach (var stratum in strata)
        {
            writer.WriteStartObject();
            writer.WriteString("environmentId", stratum.EnvironmentId);
            writer.WriteString("primaryModuleId", stratum.PrimaryModuleId);
            writer.WriteString("categoryId", stratum.CategoryId);
            writer.WriteString("severity", stratum.Severity.ToString());
            writer.WriteNumber("populationCount", stratum.PopulationCount);
            writer.WriteNumber("allocation", stratum.Allocation);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("ranks");
        foreach (var rank in ranks)
        {
            writer.WriteStartObject();
            writer.WriteString("memberId", rank.MemberId);
            writer.WriteString("rankDigest", rank.RankDigest);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("selected");
        foreach (var selection in selected)
        {
            writer.WriteStartObject();
            writer.WriteString("memberId", selection.Member.Id);
            writer.WriteBoolean("mandatory", selection.Mandatory);
            writer.WriteString("rankDigest", selection.RankDigest);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private sealed record Key(string EnvironmentId, string PrimaryModuleId, string CategoryId, SamplingSeverity Severity)
    {
        internal static Key From(SamplingMember member) => new(member.EnvironmentId, member.PrimaryModuleId, member.CategoryId, member.Severity);
    }

    private sealed class KeyComparer : IComparer<Key>
    {
        internal static readonly KeyComparer Instance = new();
        public int Compare(Key? x, Key? y)
        {
            var result = StringComparer.Ordinal.Compare(x!.EnvironmentId, y!.EnvironmentId);
            if (result == 0) { result = StringComparer.Ordinal.Compare(x.PrimaryModuleId, y.PrimaryModuleId); }
            if (result == 0) { result = StringComparer.Ordinal.Compare(x.CategoryId, y.CategoryId); }
            return result == 0 ? StringComparer.Ordinal.Compare(x.Severity.ToString(), y.Severity.ToString()) : result;
        }
    }
}
