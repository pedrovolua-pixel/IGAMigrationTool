using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RecommendationGuidance;

var checks = 0;
var input = GuidanceFixture.Create();
var snapshot = Build(input);
var finding = input.Findings[0];
var option = finding.Options[0];
Check("schema/status and every option stay unverified", snapshot.SchemaVersion == "synthetic-recommendation-guidance-v1" && snapshot.Status == "SyntheticUnverified" && snapshot.Findings[0].Options[0].Status == "Unverified");
Check("literal independently authored option identity", snapshot.Findings[0].Options[0].ScopedOptionId == GuidanceFixture.ExpectedOptionId);
Check("literal independently authored full content digest", snapshot.ContentDigest == GuidanceFixture.ExpectedContentDigest);
var golden = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "golden-payload.json"));
Check("independent full golden canonical bytes", RecommendationGuidanceBuilder.CanonicalPayload(snapshot).SequenceEqual(golden));
Check("golden payload itself matches fixed hash", Hash(golden) == GuidanceFixture.ExpectedContentDigest);
Check("repeat exact bytes and digest", Build(input).ContentDigest == snapshot.ContentDigest && RecommendationGuidanceBuilder.CanonicalPayload(Build(input)).SequenceEqual(golden));
Check("original/current presentation separate", snapshot.Findings[0].OriginalTitle == "Original fictional title" && snapshot.Findings[0].PresentationTitle == "Current fictional title" && snapshot.Findings[0].CurrentState == "Confirmed");
Check("all exact option fields retained", snapshot.Findings[0].Options[0] == new GuidanceOption(GuidanceFixture.ExpectedOptionId, "inspect-fixture", "Unverified", option.Text, option.Prerequisites, option.Risk, option.RecoveryGuidance));
Check("original occurrence retained", snapshot.Findings[0].Occurrences[0] == finding.Occurrences[0]);
Check("ordered guidance and provenance retained", snapshot.Findings[0].ValidationGuidance.SequenceEqual(finding.ValidationGuidance) && snapshot.Findings[0].GuidanceReferences.SequenceEqual(finding.GuidanceReferences));
Check("warnings explicitly exclude priority and restoration claims", snapshot.Warnings.Length == 4 && snapshot.Warnings.Any(value => value.Contains("not a priority")) && snapshot.Warnings.Any(value => value.Contains("verified restoration")));
Check("unavailable future capabilities explicit", snapshot.UnavailableSections.Length == 5 && snapshot.UnavailableSections.Any(value => value.Contains("CSV export")) && snapshot.UnavailableSections.Any(value => value.Contains("report publication")));
var culture = CultureInfo.CurrentCulture;
try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Check("culture independent canonical bytes", Build(input).ContentDigest == snapshot.ContentDigest); }
finally { CultureInfo.CurrentCulture = culture; }
var callerBytes = RecommendationGuidanceBuilder.CanonicalPayload(snapshot); callerBytes[0] = 0;
Check("canonical output byte array detached", RecommendationGuidanceBuilder.CanonicalPayload(snapshot).SequenceEqual(golden));
using (var versions = JsonDocument.Parse(input.Source.FrozenVersions.GetRawText()))
using (var capability = JsonDocument.Parse(input.Source.CapabilityLock.GetRawText()))
using (var analysis = JsonDocument.Parse(input.Source.AnalysisLock.GetRawText()))
{
    var detached = Build(input with { Source = input.Source with { FrozenVersions = versions.RootElement, CapabilityLock = capability.RootElement, AnalysisLock = analysis.RootElement } });
    versions.Dispose(); capability.Dispose(); analysis.Dispose();
    Check("all source documents survive caller disposal", RecommendationGuidanceBuilder.CanonicalPayload(detached).SequenceEqual(golden));
}
var callerNode = Node(input.Source.AnalysisLock);
var captured = Build(input with { Source = input.Source with { AnalysisLock = Json(callerNode) } });
callerNode["presetId"] = "changed-after-return";
Check("caller object changes cannot alter earlier source", captured.Source.AnalysisLock.GetProperty("presetId").GetString() == input.Source.BaselineId && captured.ContentDigest == snapshot.ContentDigest);
var callerArray = new[] { option };
var copiedInput = input with { Findings = [finding with { Options = callerArray.ToImmutableArray() }] };
var before = Build(copiedInput);
callerArray[0] = option with { Text = "Changed caller array" };
Check("immutable option collections detached from caller array", before.Findings[0].Options[0].Text == option.Text && before.ContentDigest == snapshot.ContentDigest);

foreach (var state in new[] { "Proposed", "Confirmed", "Rejected", "Deferred" })
{
    var changed = Build(input with { Findings = [finding with { CurrentState = state }] });
    Check($"{state} retains unverified options", changed.Findings[0].Options.Length == 1 && changed.Findings[0].Options[0].Status == "Unverified");
    Check($"{state} leaves scoped identity fixed", changed.Findings[0].Options[0].ScopedOptionId == GuidanceFixture.ExpectedOptionId);
    Check($"{state} retains immutable original", changed.Findings[0].OriginalTitle == finding.OriginalTitle && changed.Findings[0].Occurrences.SequenceEqual(finding.Occurrences));
}
var initial = Build(input with { Findings = [finding with { FindingRevision = 0, PresentationTitle = finding.OriginalTitle, BusinessContext = "", CurrentState = "Proposed" }] });
Check("initial valid original presentation/state", initial.Findings[0].FindingRevision == 0 && initial.Findings[0].Options[0].Status == "Unverified");
var automatic = Build(input with { Findings = [finding with { Severity = "Low", InitialState = "AutoConfirmed", CurrentState = "AutoConfirmed" }] });
Check("autoconfirmed finding never reviews recommendation", automatic.Findings[0].Options[0].Status == "Unverified");
foreach (var preset in new[] { "synthetic-analysis-healthy-v1", "synthetic-analysis-gaps-v1" })
{
    var lockNode = Node(input.Source.AnalysisLock); lockNode["presetId"] = preset;
    var empty = Build(input with { Source = input.Source with { BaselineId = preset, AnalysisLock = Json(lockNode) }, Findings = [] });
    Check($"{preset} explicit empty advice", empty.Findings.IsEmpty && empty.Status == "SyntheticUnverified");
}

foreach (var changedSource in new[]
{
    input.Source with { RunInputDigest = new string('d',64) }, input.Source with { AnalysisContentDigest = new string('d',64) },
    input.Source with { SavedCoverageDigest = new string('d',64) }, input.Source with { ReviewSnapshotDigest = new string('d',64) },
    input.Source with { RunRevision = 14, ReviewRunRevision = 14 },
    input.Source with { RunId = Guid.Parse("d6bb0d50-a10d-4bb5-82ce-65cb53918e14"), ReviewRunId = Guid.Parse("d6bb0d50-a10d-4bb5-82ce-65cb53918e14") }
}) Check("every changed valid source is content bound", Build(input with { Source = changedSource }).ContentDigest != snapshot.ContentDigest);
var changedAnalysisDigest = Node(input.Source.FrozenVersions); changedAnalysisDigest["analysisFixtureDigest"] = new string('e', 64);
Check("matching changed fixture proof binds digest", Build(input with { Source = input.Source with { AnalysisFixtureDigest = new string('e', 64), FrozenVersions = Json(changedAnalysisDigest) } }).ContentDigest != snapshot.ContentDigest);
foreach (var name in new[] { "packDigest", "catalogDigest", "profileDigest", "evidenceDigest" })
{
    var changed = Node(input.Source.AnalysisLock); changed[name] = new string('d', 64);
    Check($"analysis {name} content bound", Build(input with { Source = input.Source with { AnalysisLock = Json(changed) } }).ContentDigest != snapshot.ContentDigest);
}
foreach (var name in new[] { "scriptedResultsDigest", "maturityFixtureDigest" })
{
    var changed = Node(input.Source.FrozenVersions); changed[name] = new string('d', 64);
    Check($"frozen {name} content bound", Build(input with { Source = input.Source with { FrozenVersions = Json(changed) } }).ContentDigest != snapshot.ContentDigest);
}
var changedLock = Node(input.Source.CapabilityLock); changedLock["lockDigest"] = new string('d', 64);
Check("capability proof content bound", Build(input with { Source = input.Source with { CapabilityLock = Json(changedLock) } }).ContentDigest != snapshot.ContentDigest);
foreach (var changed in new[]
{
    finding with { OriginalTitle = "Changed original" }, finding with { PresentationTitle = "Changed presentation" },
    finding with { BusinessContext = "Changed context" }, finding with { FindingRevision = 2 }, finding with { RootCause = "Changed root cause" },
    finding with { RuleId = "SYN-TRACE" }, finding with { Severity = "High" }, finding with { CategoryId = "OPERATIONS" },
    finding with { ValidationGuidance = ["Changed validation"] }, finding with { GuidanceReferences = ["fixture-guidance:changed"] },
    finding with { Assumptions = ["Changed assumption"] }, finding with { Limitations = ["Changed limitation"] },
    finding with { FindingId = new string('d',64) },
    finding with { Occurrences = [finding.Occurrences[0] with { OccurrenceId = new string('e',64) }] },
    finding with { Occurrences = [finding.Occurrences[0] with { ObjectId = "OTHER" }] },
    finding with { Occurrences = [finding.Occurrences[0] with { ModuleId = "SyntheticOperations" }] },
    finding with { Occurrences = [finding.Occurrences[0] with { OriginalDigest = new string('e',64) }] },
    finding with { Occurrences = [finding.Occurrences[0] with { EvidenceReference = "fixture-evidence:other" }] },
    finding with { Options = [option with { OptionId = "other-option" }] }, finding with { Options = [option with { Text = "Changed text" }] },
    finding with { Options = [option with { Prerequisites = "Changed prerequisite" }] }, finding with { Options = [option with { Risk = "Changed risk" }] },
    finding with { Options = [option with { RecoveryGuidance = "Changed recovery" }] }
}) Check("every content field is hash bound", Build(input with { Findings = [changed] }).ContentDigest != snapshot.ContentDigest);
var reversedText = Build(input with { Findings = [finding with { ValidationGuidance = finding.ValidationGuidance.Reverse().ToImmutableArray() }] });
Check("ordered string arrays remain significant", reversedText.ContentDigest != snapshot.ContentDigest);
const string hostile = "<script>alert(1)</script>\n![image](https://invalid.example/x)\n[link](file:///tmp/x)\n```sh\n$(curl example)\n```\n=SUM(1+1)\n'\"&\u2028";
var hostileResult = Build(input with { Findings = [finding with { PresentationTitle = hostile, Options = [option with { Text = hostile, RecoveryGuidance = hostile }] }] });
Check("hostile content retained as inert data", hostileResult.Findings[0].PresentationTitle == hostile && hostileResult.Findings[0].Options[0].Text == hostile);
Check("hostile canonical JSON encodes HTML", !Encoding.UTF8.GetString(RecommendationGuidanceBuilder.CanonicalPayload(hostileResult)).Contains("<script>"));
Check("hostile data affects hash", hostileResult.ContentDigest != snapshot.ContentDigest);

var second = finding with { FindingId = new string('d', 64), Occurrences = [finding.Occurrences[0] with { OccurrenceId = new string('e', 64), ObjectId = "OBJECT-2" }] };
var anotherOption = option with { OptionId = "compare-new-fixture", Text = "Compare new fixture evidence." };
var multi = input with { Findings = [second, finding with { Options = [option, anotherOption], Occurrences = [finding.Occurrences[0], finding.Occurrences[0] with { OccurrenceId = new string('f', 64), ObjectId = "OBJECT-3" }] }] };
var ordered = Build(multi);
Check("repeat option IDs across findings legal", ordered.Findings.Length == 2 && ordered.Findings.All(value => value.Options.Any(item => item.OptionId == "inspect-fixture")));
Check("finding scoped identity prevents collisions", ordered.Findings[0].Options.Single(value => value.OptionId == "inspect-fixture").ScopedOptionId != ordered.Findings[1].Options[0].ScopedOptionId);
Check("finding/occurrence/option stable order", ordered.Findings[0].FindingId == GuidanceFixture.FindingId && ordered.Findings[0].Occurrences[0].OccurrenceId == new string('c', 64) && ordered.Findings[0].Options[0].OptionId == "compare-new-fixture");
var random = new Random(731);
for (var index = 0; index < 100; index++)
{
    var shuffled = multi.Findings.OrderBy(_ => random.Next()).Select(value => value with { Options = value.Options.OrderBy(_ => random.Next()).ToImmutableArray(), Occurrences = value.Occurrences.OrderBy(_ => random.Next()).ToImmutableArray() }).ToImmutableArray();
    var moduleNode = Node(input.Source.CapabilityLock);
    var modules = moduleNode["modules"]!.AsArray(); var one = modules[0]!.DeepClone(); modules[0] = modules[1]!.DeepClone(); modules[1] = one;
    var reversed = new JsonObject(moduleNode.OrderBy(_ => random.Next()).Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, pair.Value?.DeepClone())));
    Check("keyed arrays and source properties canonical", Build(multi with { Source = multi.Source with { CapabilityLock = Json(reversed) }, Findings = shuffled }).ContentDigest == ordered.ContentDigest);
}

Denied("null input", null);
Denied("null source", input with { Source = null! });
Denied("default findings", input with { Findings = default });
foreach (var source in new[]
{
    input.Source with { Scope = input.Source.Scope with { CustomerId = "other" } }, input.Source with { Scope = input.Source.Scope with { ProjectId = "other" } },
    input.Source with { Scope = input.Source.Scope with { EnvironmentId = "other" } }, input.Source with { Scope = null! },
    input.Source with { RunId = Guid.Empty }, input.Source with { RunRevision = -1 }, input.Source with { RunState = "Published" },
    input.Source with { ReviewRunId = Guid.NewGuid() }, input.Source with { ReviewRunRevision = 14 },
    input.Source with { ProfileId = "synthetic-analysis-equal-v1" }, input.Source with { BaselineId = "unknown" },
    input.Source with { RunInputDigest = "bad" }, input.Source with { AnalysisFixtureDigest = "bad" }, input.Source with { AnalysisContentDigest = "bad" },
    input.Source with { SavedCoverageDigest = "bad" }, input.Source with { ReviewSnapshotDigest = "bad" },
    input.Source with { FrozenVersions = default }, input.Source with { CapabilityLock = default }, input.Source with { AnalysisLock = default }
}) Denied("invalid source/link/version refused", input with { Source = source });
foreach (var name in new[] { "profileVersion", "scoringAlgorithmVersion", "aiPolicyVersion", "promptVersion", "modelVersion", "applicationVersion", "workSchemaVersion", "desiredOutcomeVersion" })
{
    var node = Node(input.Source.FrozenVersions); node[name] = "unknown-v2";
    Denied($"unknown frozen {name}", input with { Source = input.Source with { FrozenVersions = Json(node) } });
}
foreach (var name in new[] { "matrixVersion", "stateAtLock", "productBuild", "databaseSchemaBuild", "hotfixSetDigest", "sqlServerBuild", "queryPackVersion", "normalizationSchemaVersion", "ruleCatalogVersion" })
{
    var node = Node(input.Source.CapabilityLock); node[name] = "unknown-v2";
    Denied($"unknown capability {name}", input with { Source = input.Source with { CapabilityLock = Json(node) } });
}
foreach (var name in new[] { "packVersion", "presetId", "presetVersion", "catalogVersion", "profileId", "profileVersion", "packDigest", "evidenceDigest", "catalogDigest", "profileDigest" })
{
    var node = Node(input.Source.AnalysisLock); node[name] = "unknown-v2";
    Denied($"unknown/mismatched analysis {name}", input with { Source = input.Source with { AnalysisLock = Json(node) } });
}
foreach (var name in new[] { "sourceProduct", "productVersion", "evidenceSchemaVersion", "ruleLanguageVersion" })
{
    var node = Node(input.Source.AnalysisLock); node["compatibility"]![name] = "unknown-v2";
    Denied($"unknown compatibility {name}", input with { Source = input.Source with { AnalysisLock = Json(node) } });
}
foreach (var (part, element) in new[] { ("versions", input.Source.FrozenVersions), ("capability", input.Source.CapabilityLock), ("analysis", input.Source.AnalysisLock) })
{
    var unknown = Node(element); unknown["unknown"] = "data";
    Denied($"unknown strict {part} field", input with { Source = Replace(part, Json(unknown)) });
    var missing = Node(element); missing.Remove(missing.First().Key);
    Denied($"missing strict {part} field", input with { Source = Replace(part, Json(missing)) });
    var duplicate = element.GetRawText().TrimEnd(); duplicate = duplicate[..^1] + "," + JsonSerializer.Serialize(element.EnumerateObject().First().Name) + ":null}";
    Denied($"duplicate strict {part} property", input with { Source = Replace(part, GuidanceFixture.Json(duplicate)) });
}
var extraScope = Node(input.Source.AnalysisLock); extraScope["scope"]!["unknown"] = "data";
Denied("nested extra scope field", input with { Source = input.Source with { AnalysisLock = Json(extraScope) } });
var crossScope = Node(input.Source.AnalysisLock); crossScope["scope"]!["customerId"] = "other";
Denied("nested cross scope", input with { Source = input.Source with { AnalysisLock = Json(crossScope) } });
var extraCompatibility = Node(input.Source.AnalysisLock); extraCompatibility["compatibility"]!["unknown"] = "data";
Denied("nested extra compatibility", input with { Source = input.Source with { AnalysisLock = Json(extraCompatibility) } });
var extraModule = Node(input.Source.CapabilityLock); extraModule["modules"]![0]!["unknown"] = "data";
Denied("nested extra module", input with { Source = input.Source with { CapabilityLock = Json(extraModule) } });
var duplicateModule = Node(input.Source.CapabilityLock); duplicateModule["modules"]![1] = duplicateModule["modules"]![0]!.DeepClone();
Denied("duplicate modules", input with { Source = input.Source with { CapabilityLock = Json(duplicateModule) } });
var wrongFixture = Node(input.Source.FrozenVersions); wrongFixture["analysisFixtureDigest"] = new string('d', 64);
Denied("analysis fixture digest linkage", input with { Source = input.Source with { FrozenVersions = Json(wrongFixture) } });
foreach (var bad in new[]
{
    finding with { FindingId = "bad" }, finding with { RuleVersion = "v2" }, finding with { Severity = "bad" }, finding with { CategoryId = "bad" },
    finding with { OriginalTitle = "" }, finding with { PresentationTitle = " " }, finding with { BusinessContext = null! }, finding with { FindingRevision = -1 },
    finding with { InitialState = "Confirmed" }, finding with { CurrentState = "ValidatedClosed" },
    finding with { CurrentState = "AutoConfirmed" }, finding with { InitialState = "AutoConfirmed" },
    finding with { FindingRevision = 0 }, finding with { Occurrences = default }, finding with { Occurrences = [] },
    finding with { Options = default }, finding with { Options = [] }, finding with { Options = [null!] },
    finding with { Options = [option with { OptionId = " " }] }, finding with { Options = [option with { Text = " " }] },
    finding with { Options = [option with { Prerequisites = null! }] }, finding with { Options = [option with { Risk = "" }] },
    finding with { Options = [option with { RecoveryGuidance = "" }] },
    finding with { ValidationGuidance = [] }, finding with { GuidanceReferences = default }, finding with { Assumptions = default }, finding with { Limitations = [] },
    finding with { ValidationGuidance = [""] }, finding with { PresentationTitle = new string('x', RecommendationGuidanceBuilder.MaximumTextLength + 1) },
    finding with { Occurrences = [finding.Occurrences[0] with { OccurrenceId = "bad" }] }, finding with { Occurrences = [finding.Occurrences[0] with { OriginalDigest = "bad" }] },
    finding with { Occurrences = [finding.Occurrences[0] with { ObjectType = "VendorObject" }] }, finding with { Occurrences = [finding.Occurrences[0] with { ModuleId = "VendorModule" }] },
    finding with { Occurrences = [finding.Occurrences[0] with { EvidenceReference = "" }] }, finding with { Occurrences = [null!] }
}) Denied("invalid finding/content refused", input with { Findings = [bad] });
Denied("null finding", input with { Findings = [null!] });
Denied("duplicate finding", input with { Findings = [finding, finding] });
Denied("duplicate option within group", input with { Findings = [finding with { Options = [option, option] }] });
Denied("duplicate occurrence within group", input with { Findings = [finding with { Occurrences = [finding.Occurrences[0], finding.Occurrences[0]] }] });
Denied("duplicate occurrence across groups", input with { Findings = [finding, second with { Occurrences = finding.Occurrences }] });
Denied("duplicate object within group", input with { Findings = [finding with { Occurrences = [finding.Occurrences[0], finding.Occurrences[0] with { OccurrenceId = new string('e', 64) }] }] });
using (var disposed = JsonDocument.Parse(input.Source.AnalysisLock.GetRawText()))
{
    var value = disposed.RootElement; disposed.Dispose();
    Denied("disposed input source fails closed", input with { Source = input.Source with { AnalysisLock = value } });
}
Console.WriteLine($"PASS {checks} recommendation guidance assertions");

GuidanceSourceBinding Replace(string part, JsonElement value) => part switch
{ "versions" => input.Source with { FrozenVersions = value }, "capability" => input.Source with { CapabilityLock = value }, _ => input.Source with { AnalysisLock = value } };
GuidanceSnapshot Build(GuidanceInput value)
{
    var result = RecommendationGuidanceBuilder.Build(value);
    if (!result.Succeeded) throw new InvalidOperationException($"Unexpected denial: {result.Issue}");
    return result.Snapshot!;
}
void Check(string name, bool condition) { checks++; if (!condition) throw new InvalidOperationException($"FAILED {checks}: {name}"); }
void Denied(string name, GuidanceInput? value) { var result = RecommendationGuidanceBuilder.Build(value); Check(name, !result.Succeeded && result.Issue is not null && result.Snapshot is null); }
JsonObject Node(JsonElement value) => JsonNode.Parse(value.GetRawText())!.AsObject();
JsonElement Json(JsonNode value) => JsonSerializer.SerializeToElement(value);
string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
