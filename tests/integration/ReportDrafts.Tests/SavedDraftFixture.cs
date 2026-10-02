using System.Globalization;
using AssessmentCoverage;
using AssessmentMaturity;
using AssessmentRuns;
using AssessmentScoring;
using FindingReview;
using ReportDrafts;

internal static class SavedDraftFixture
{
    internal static DraftReportInput Create(SyntheticRunSnapshot run, SyntheticReviewSnapshot review)
    {
        var states = review.Findings.ToDictionary(f => f.Seed.FindingId, f => Enum.Parse<ScoringFindingState>(f.Current.State.ToString()));
        var response = SyntheticDemoAnalysisAdapter.Project(run, states, review.SnapshotDigest);
        Check.That(response.IsAvailable, "saved engine/review source accepted by existing independent analysis/scoring adapter");
        var data = response.Projection!; var a = data.Analysis; var s = data.Scoring;
        var maturity = PilotMaturityProjector.Project(DemoAnalysisCatalog.FreezeMaturity(run.BaselineCatalogId, run.ProfileCatalogId).Input).Projection!;
        var source = new DraftSourceBinding(new(run.Scope.CustomerId, run.Scope.ProjectId, run.Scope.EnvironmentId), run.RunId, run.Revision, run.State.ToString(), run.InputDigest,
            run.BaselineCatalogId, run.ProfileCatalogId, CoreFixture.Json(run.FrozenInputs), CoreFixture.Json(run.Plan.CapabilityLock), CoreFixture.Json(a.FrozenInputs),
            run.FrozenInputs.AnalysisFixtureDigest!, a.ContentDigest, s.ContentDigest, a.SavedCoverageDigest, review.RunSeed.RunId, run.Revision, review.SnapshotDigest,
            run.FrozenInputs.MaturityFixtureDigest!, maturity.InputDigest, maturity.ContentDigest);
        // Fixtures supply typed module results as INPUT. Expected health/maturity remain independent literals in PersistenceCases.
        object Score(HealthMeasure m) => new { raw = m.RawScore?.ToString(CultureInfo.InvariantCulture), display = m.DisplayScore?.ToString("0.0", CultureInfo.InvariantCulture), status = m.Status?.ToString() ?? "Unavailable", eligibleUnits = m.ScoredUnits };
        object[] Dimension(IReadOnlyList<GroupHealthMeasure> p, IReadOnlyList<GroupHealthMeasure> c) => p.Select(row => (object)new { id = row.Id, provisional = Score(row.Score), publishableCurrent = Score(c.Single(other => other.Id == row.Id).Score) }).ToArray();
        string? Weight(HealthScoreSet scores, string id)
        {
            var available = scores.Categories.Where(row => row.Score.IsAvailable).Select(row => row.Id).ToHashSet();
            if (!available.Contains(id)) return null;
            return (s.Profile.WeightMode == ScoringWeightMode.EqualAssessedCategories ? 1m / available.Count : s.Profile.Categories.Single(c => c.CategoryId == id).Weight / s.Profile.Categories.Where(c => available.Contains(c.CategoryId)).Sum(c => c.Weight)).ToString(CultureInfo.InvariantCulture);
        }
        var findings = a.Groups.Select(group =>
        {
            var members = a.Findings.Where(f => f.RootCauseKey == group.RootCauseKey).ToArray(); var first = members[0]; var current = review.Findings.Single(f => f.Seed.FindingId == group.RootCauseKey);
            return new
            {
                id = group.RootCauseKey,
                title = current.Current.PresentationTitle,
                originalTitle = current.Seed.OriginalTitle,
                initialState = first.InitialDisposition.ToString(),
                category = first.CategoryId,
                severity = first.Severity.ToString(),
                confidencePercent = first.ConfidencePercent.ToString(CultureInfo.InvariantCulture),
                state = current.Current.State.ToString(),
                reviewRequired = current.Current.State == SyntheticFindingState.Proposed && first.Severity is DeterministicAnalysis.SyntheticSeverity.Critical or DeterministicAnalysis.SyntheticSeverity.High,
                ruleId = first.Provenance.RuleId,
                ruleVersion = first.Provenance.RuleVersion,
                baselineId = first.Provenance.BaselineId,
                rootCause = first.RootCause,
                rootCauseKey = group.RootCauseKey,
                objectIds = group.ObjectIds,
                evidenceReferences = members.Select(f => f.Provenance.EvidenceReference).ToArray(),
                facts = members.SelectMany(f => f.Facts.Select(fact => $"{f.ObjectId}: {fact.Description} Count: {fact.Count}.")).ToArray(),
                inferences = members.Select(f => f.Inference).Distinct().ToArray(),
                assumptions = first.Assumptions,
                impact = first.Impact,
                recommendations = first.RecommendationOptions.Select(o => o.Text).ToArray(),
                validationGuidance = string.Join(" ", first.ValidationGuidance),
                sources = new[] { first.GuidanceReference },
                confidenceBand = first.ConfidenceBand,
                method = first.DetectionMethod,
                likelihood = first.Likelihood,
                limitations = first.Limitations,
                originalDigests = current.Seed.OriginalDigests,
                outcomeIds = members.SelectMany(f => f.OutcomeIds).Distinct().ToArray(),
                revision = current.Current.Revision,
                businessContext = current.Current.BusinessContext,
                occurrenceIds = current.Seed.Occurrences.Select(o => o.OccurrenceId).ToArray()
            };
        }).ToArray();
        var maturityContent = new
        {
            status = "Ready",
            reasonCode = (string?)null,
            level = maturity.Level.ToString(),
            algorithmVersion = maturity.Input.Versions.AlgorithmVersion,
            catalogVersion = maturity.Input.Catalog.Version,
            inputDigest = maturity.InputDigest,
            contentDigest = maturity.ContentDigest,
            authorityBoundary = maturity.Input.Catalog.AuthorityBoundary,
            mandatoryDomains = maturity.Counts.MandatoryDomains,
            insufficientIndicators = maturity.Counts.InsufficientIndicators,
            insufficientDomains = maturity.Counts.InsufficientDomains,
            improvementMissingDistinctAssessments = maturity.Counts.ImprovementMissingDistinctAssessments,
            governanceOwnershipEvidenced = maturity.Gates.GovernanceOwnershipEvidenced,
            gates = new[] { ("Developing", maturity.Gates.Developing), ("Defined", maturity.Gates.Defined), ("Managed", maturity.Gates.Managed), ("Optimized", maturity.Gates.Optimized) }.Select(g => new { level = g.Item1, metDomains = g.Item2.MetDomains, mandatoryDomains = g.Item2.MandatoryDomains, requiredPercent = g.Item2.RequiredPercent, isMet = g.Item2.IsMet }).ToArray(),
            domains = maturity.Domains.Select(domain => new
            {
                id = domain.DomainId,
                name = maturity.Input.Catalog.MandatoryDomains.Single(d => d.Id == domain.DomainId).Name,
                baseMet = domain.BaseMet,
                operationAndReviewMet = domain.OperationAndReviewMet,
                improvementMet = domain.ImprovementMet,
                insufficientIndicators = domain.InsufficientIndicators,
                indicators = maturity.Input.Indicators.Where(i => i.DomainId == domain.DomainId).Select(i => new { kind = i.Kind.ToString(), state = i.State.ToString(), reasonCode = i.ReasonCode, evidenceReferences = i.EvidenceReferences, assessmentReferences = i.AssessmentReferences, hasValidatedImprovementEvidence = i.HasValidatedImprovementEvidence }).ToArray()
            }).ToArray(),
            ownership = new { state = maturity.Input.GovernanceOwnership.State.ToString(), ownerId = maturity.Input.GovernanceOwnership.OwnerId, evidenceReferences = maturity.Input.GovernanceOwnership.EvidenceReferences, reasonCode = maturity.Input.GovernanceOwnership.ReasonCode }
        };
        var content = CoreFixture.Json(new
        {
            provisional = Score(s.Provisional.Overall),
            publishableCurrent = Score(s.PublishableCurrent.Overall),
            categories = s.Provisional.Categories.Select(row => new { id = row.Id, provisional = Score(row.Score), publishableCurrent = Score(s.PublishableCurrent.Categories.Single(c => c.Id == row.Id).Score), provisionalWeight = Weight(s.Provisional, row.Id), publishableWeight = Weight(s.PublishableCurrent, row.Id) }).ToArray(),
            objectTypes = Dimension(s.Provisional.ObjectTypes, s.PublishableCurrent.ObjectTypes),
            modules = Dimension(s.Provisional.Modules, s.PublishableCurrent.Modules),
            outcomes = Dimension(s.Provisional.ApprovedOutcomes, s.PublishableCurrent.ApprovedOutcomes),
            quality = new { plannedUnits = run.Plan.ExpectedKeys.Count, executedUnits = run.Results.Count(r => r.State is CoverageState.Pass or CoverageState.Finding), gapUnits = run.Results.Count(r => r.State is not (CoverageState.Pass or CoverageState.Finding or CoverageState.NotApplicable)), notApplicableUnits = run.Results.Count(r => r.State == CoverageState.NotApplicable), proposedReviewUnits = s.Quality.UnreviewedMandatoryFindings, totalFindingUnits = run.Results.Count(r => r.State == CoverageState.Finding) },
            findings,
            warnings = new[] { "Synthetic draft; not published.", "Coverage gaps and mandatory review stay separate from health." },
            maturity = maturityContent,
            reviewHistory = review.Findings.Select(f => new
            {
                findingId = f.Seed.FindingId,
                revision = f.Current.Revision,
                originalTitle = f.Seed.OriginalTitle,
                businessContext = f.Current.BusinessContext,
                events = f.History.Select(e => new { eventId = e.EventId, actorId = e.ActorId, actorRoles = e.ActorRoles.Select(r => r.ToString()).ToArray(), kind = e.Command.Kind.ToString(), recordedAtUtc = e.RecordedAtUtc, revision = e.Outcome.Revision, state = e.Outcome.State.ToString(), reason = e.Command.Reason, text = e.Command.Text, title = e.Command.Title, businessContext = e.Command.BusinessContext }).ToArray()
            }).ToArray(),
            healthyControls = a.Results.Where(r => r.Coverage.State == CoverageState.Pass).Select(r => new { objectId = r.Unit.ObjectId, ruleId = r.Unit.RuleId, ruleVersion = r.Unit.RuleVersion, state = "Pass" }).ToArray(),
            limitations = a.Results.Where(r => r.Coverage.State is not (CoverageState.Pass or CoverageState.Finding or CoverageState.NotApplicable)).Select(r => new { objectId = r.Unit.ObjectId, ruleId = r.Unit.RuleId, state = r.Coverage.State.ToString(), reasonCode = r.Coverage.ReasonCode }).ToArray(),
            methodology = new[] { "pilot-health-v1; gaps excluded from default health.", "Independent pilot-maturity-v1 mandatory domain denominator." },
            unavailableSections = new[] { "Risk acceptance", "Reassessment", "AI", "Tasks", "Publication" }
        });
        return new(source, content);
    }
}
