using System.Collections;
using System.Security.Cryptography;
using System.Text;
using AssessmentCoverage;
using SyntheticAiExecution;
using SyntheticEvaluationSourceIntegration;

var checks = 0;
void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
const string literal = "{\"a\":\"\\u003Cscript\\u003E\\u0026\\u00E9\\n\",\"enum\":\"High\",\"z\":null}";
var canonical = SourceCanonical.Json(new { z = (string?)null, @enum = AiSeverity.High, a = "<script>&é\n" });
Check(canonical == literal, "independent literal canonical escaping/order/null/named enum");
Check(SourceCanonical.Hash(canonical) == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(literal))), "hash exact literal UTF8 bytes");
Check(!canonical.EndsWith('\n') && !canonical.StartsWith('\uFEFF'), "no BOM/newline");
Check(SourceCanonical.Json(new { a = "<script>&é\n", @enum = AiSeverity.High, z = (string?)null }) == literal, "property insertion order irrelevant");
Check(SourceCanonical.GeneratedJson(new { Z = AiSeverity.High, A = "<" }) == "{\"A\":\"\\u003C\",\"Z\":1}", "embedded owning default serializer representation retained");
var original = new Phase1BEvaluationSourceOccurrence("native-occurrence", new("native-object", "configuration"), "{\"native\":true}", new string('a', 64), "{\"Owner\":1}");
var input = new List<Phase1BEvaluationSourceOccurrence> { original };
var member = new Phase1BEvaluationSourceMember("native-group", 1, input);
input.Clear();
Check(member.Occurrences.Count == 1, "member collection detached");
var members = new List<Phase1BEvaluationSourceMember> { member };
var gaps = new List<Phase1BEvaluationSourceGap> { new(new("native-gap", "configuration"), CoverageState.InsufficientEvidence, "AI_INSUFFICIENT_SOURCE", "AI") };
var capture = new Phase1BEvaluationSourceCapture(Guid.NewGuid(), 1, "input", "ai", "outcome", "analysis", DateTimeOffset.UnixEpoch, members, gaps, literal);
members.Clear(); gaps.Clear();
Check(capture.Members.Count == 1 && capture.Gaps.Count == 1, "capture collections detached");
foreach (var collection in new object[] { member.Occurrences, capture.Members, capture.Gaps })
{
    Check(((IList)collection).IsReadOnly, "nested read-only collection");
    try { ((IList)collection).Clear(); throw new InvalidOperationException("mutable view"); }
    catch (NotSupportedException) { Check(true, "mutation rejects"); }
}
foreach (var type in new[] { typeof(Phase1BEvaluationSourceCapture), typeof(Phase1BEvaluationSourceMember), typeof(Phase1BEvaluationSourceOccurrence), typeof(Phase1BEvaluationSourceGap) })
{
    Check(type.IsSealed && type.GetConstructors().Length == 0, "closed public captured type has no unchecked constructor");
    foreach (var property in type.GetProperties()) Check(property.SetMethod is null, "capture property get only " + property.Name);
}
var permutations = Enumerable.Range(0, 128).Select(i => new { sequence = i, text = i % 2 == 0 ? "fictional Ω <b>" : "line\n\t", absent = (string?)null }).ToArray();
foreach (var item in permutations)
{
    var json = SourceCanonical.Json(item);
    Check(SourceCanonical.Json(item) == json && SourceCanonical.Hash(json) != SourceCanonical.Hash(SourceCanonical.Json(item with { sequence = item.sequence + 1 })), "all semantic values bind bytes deterministically");
}
Console.WriteLine($"PASS {checks} source capture portable assertions; no database or provider.");
