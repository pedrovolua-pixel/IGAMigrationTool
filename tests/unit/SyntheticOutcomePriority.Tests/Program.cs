using System.Collections.Immutable;
using System.Text.Json;
using AssessmentScoring;
using SyntheticOutcomePriority;
using static Fixtures;

var p = PriorityProjector.Project(Factors)!;
Check(p.RawPriority == 51.75m && p.DisplayPriority == 51.8m && p.OriginalBand == PriorityBand.Medium, "literal51.75");
Check(p.Contributions.Length == 6 && p.Contributions.Sum(t => t.Weight) == 1 && p.Contributions.Sum(t => t.Points) == 51.75m, "six factors");
Check(PriorityProjector.Project(Factors with { MatchedObjectiveIds = ["resilience"] })!.RawPriority == 49.25m, "literal49.25");
Check(PriorityProjector.Project(Factors with { MatchedObjectiveIds = ["access-governance", "resilience"] })!.RawPriority == 51.75m, "objective max");
Check(PriorityProjector.Project(Factors with { MatchedObjectiveIds = [] })!.RawPriority == 41.75m, "complete no match0");
Check(PriorityProjector.Project(Factors with { Exposure = Exposure.PublicFacing })!.RawPriority == 66.75m, "exposure15");
Check(PriorityProjector.Project(Factors with { Dependency = DependencyCriticality.Essential })!.RawPriority == 63m, "dependency11.25");
Check(PriorityProjector.Project(Factors with { OriginalEffort = EffortSize.XS })!.RawPriority == 54.25m, "quick2.5");
Check(PriorityProjector.Project(Factors with { AffectedObjectIds = Enumerable.Range(0, 20).Select(i => "object-" + i).ToImmutableArray() })!.RawPriority == 53.25m, "count1.5");
var capped = Factors with { AffectedObjectIds = Enumerable.Range(0, 101).Select(i => "object-" + i).ToImmutableArray() };
Check(PriorityProjector.Project(capped)!.RawPriority == PriorityProjector.Project(capped with { AffectedObjectIds = capped.AffectedObjectIds.Take(100).ToImmutableArray() })!.RawPriority, "countcap100");
foreach (var boundary in new[] { (39.999m, PriorityBand.Low), (40m, PriorityBand.Medium), (59.999m, PriorityBand.Medium), (60m, PriorityBand.High), (79.999m, PriorityBand.High), (80m, PriorityBand.Immediate) }) Check(PriorityProjector.Band(boundary.Item1) == boundary.Item2, "rawboundary");
foreach (var missing in new[] { Factors with { Exposure = null }, Factors with { Dependency = null }, Factors with { OriginalEffort = null }, Factors with { Objectives = default }, Factors with { AffectedObjectIds = default } }) Check(PriorityProjector.Project(missing) is { RawPriority: null } v && v.MissingInputs.Length > 0, "missing unavailable");
foreach (var invalid in new[] { Factors with { Severity = (ScoringSeverity)99 }, Factors with { Exposure = (Exposure)99 }, Factors with { Dependency = (DependencyCriticality)99 }, Factors with { OriginalEffort = (EffortSize)99 }, Factors with { AffectedObjectIds = ["x", "x"] }, Factors with { Objectives = [new("bad", 1.1m)] }, Factors with { MatchedObjectiveIds = ["foreign"] } }) Check(PriorityProjector.Project(invalid) is null, "invalidfactor");
foreach (var expected in new[] { (EffortSize.XS, 1, 2), (EffortSize.S, 2, 8), (EffortSize.M, 8, 24), (EffortSize.L, 24, 80), (EffortSize.XL, 80, 160) }) { var e = PriorityProjector.Estimate(expected.Item1); Check(e.MinimumPersonHours == expected.Item2 && e.MaximumPersonHours == expected.Item3, "fictionalrange"); }
var content = Content(); Check(OutcomePriorityPolicy.Content(content), "sealedcontent");
Check(OutcomePriorityCanonical.Parse<OutcomeContentVersion>(OutcomePriorityCanonical.Json(content)) == content || OutcomePriorityCanonical.Json(OutcomePriorityCanonical.Parse<OutcomeContentVersion>(OutcomePriorityCanonical.Json(content))) == OutcomePriorityCanonical.Json(content), "contentroundtrip");
Check(OutcomePriorityCanonical.Json(OutcomePriorityCanonical.Seal(content with { UnitLinks = content.UnitLinks.Reverse().ToImmutableArray() })) == OutcomePriorityCanonical.Json(content), "setsorted");
Check(!OutcomePriorityPolicy.Content(content with { Title = "changed" }), "changedcontentdigest");
Check(!OutcomePriorityPolicy.Content(content with { UnitLinks = [content.UnitLinks[0], content.UnitLinks[0]] }), "duplicateunit");
Check(!OutcomePriorityPolicy.Text("\ud800") && !OutcomePriorityPolicy.Text("\0") && !OutcomePriorityPolicy.Text(new string('x', 2001)), "unicodebounds");
Check(OutcomePriorityPolicy.Text("<script>ordinary</script>\n$()"), "inert text");
Check(OutcomePriorityCanonical.Json(new { z = 1.00m, a = new { y = 2, b = "é" } }) == "{\"a\":{\"b\":\"\\u00E9\",\"y\":2},\"z\":1}", "literalcanonical");
foreach (var json in new[] { "{\"a\":1,\"a\":2}", "{\"foreign\":true}" }) { try { _ = OutcomePriorityCanonical.Parse<OutcomeContentVersion>(json); Check(false, "closedJSON"); } catch (JsonException) { Check(true, "closedJSONdenied"); } }
Check(OutcomePriorityPolicy.Source(Source()), "sealedsource");
Check(!OutcomePriorityPolicy.Source(Source() with { ProfileId = "historical-profile" }), "oldprofilenowiden");
var missingSource = Source() with { Options = [new("finding-one", "option-one", "SECURITY", Factors with { Objectives = default })] };
missingSource = OutcomePriorityCanonical.Seal(missingSource);
Check(OutcomePriorityPolicy.Source(missingSource) && OutcomePriorityCanonical.Parse<Phase1BPlanningSource>(OutcomePriorityCanonical.Json(missingSource)).Options[0].Factors.Objectives.IsDefault, "missingmapdurable");
foreach (var role in Enum.GetValues<OutcomeRole>())
{
    var a = Consultant with { Roles = [role], Actions = Enum.GetValues<OutcomeAction>().ToImmutableArray() };
    Check((OutcomePriorityPolicy.Authorize(a, OutcomeScope.Fixed, OutcomeAction.ManagePlanning) is null) == (role == OutcomeRole.Consultant), "planningrolematrix");
    Check(OutcomePriorityPolicy.Authorize(a, OutcomeScope.Fixed, OutcomeAction.ApproveOutcome) == OutcomePriorityIssue.Denied, "noimplicitcustomeractor");
}
Check(OutcomePriorityPolicy.Authorize(Approver, OutcomeScope.Fixed, OutcomeAction.ApproveOutcome) is null, "distinctapprover");
foreach (var bad in new[] { Consultant with { Authenticated = false }, Consultant with { Active = false }, Consultant with { Revoked = true }, Consultant with { AssignmentActive = false }, Consultant with { ResourceState = OutcomeResourceState.Deleted }, Consultant with { Categories = ["OTHER"] }, Consultant with { Actions = [] }, Consultant with { AssignedScope = new("foreign", "p", "e") } }) Check(OutcomePriorityPolicy.Authorize(bad, OutcomeScope.Fixed, OutcomeAction.ManageOutcome) is not null, "authoritynegative");
Console.WriteLine($"PASS unit: {Assertions} independently specified outcome/priority/canonical/authority assertions");
