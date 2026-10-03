import { jsx as _jsx, jsxs as _jsxs } from "file:///private/tmp/iga-cycle12-b12/src/web/node_modules/react/jsx-runtime.js";
const profile = "synthetic-review-maturity-fix-packages-equal-v1";
const templateVersion = "fictional-fix-templates-v1";
const templateDigest = "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669";
const maximumBytes = 32 * 1024 * 1024;
const maximumRecords = 1e5;
const disclaimer = "Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.";
const templates = [
	{
		templateId: "fictional-config-v1",
		kind: "Configuration",
		text: "{\n  \"fixtureOnly\": true,\n  \"reviewRequired\": true\n}"
	},
	{
		templateId: "fictional-script-v1",
		kind: "Script",
		text: "# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'"
	},
	{
		templateId: "fictional-sql-v1",
		kind: "Sql",
		text: "-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;"
	}
];
const warnings = [
	"Finding confirmation, rejection or deferral does not review artifacts or validate remediation.",
	"Package identity ordering is not priority or effort. Existing finding groups are preserved without root-cause merging.",
	"Source guidance is retained verbatim as historical input; its unavailable sections describe that upstream projection."
];
const unavailable = [
	"Consultant artifact review, approval history and content invalidation are unavailable.",
	"Priority, effort, customer objectives, task conversion/workflows and CSV/export are unavailable.",
	"Customer-system execution, external connectors, validated recovery/remediation and report publication are unavailable."
];
const guidanceWarnings = [
	"Synthetic fixture guidance only; no actual One Identity defect, supported remediation or customer approval is established.",
	"Every option is review-only and unverified. Finding confirmation, rejection or deferral does not review guidance or validate remediation.",
	"Stable identity ordering is not a priority calculation. Recovery guidance does not establish executed or verified restoration.",
	"This is a current detached value, not durable recommendation history. The run remains Scoring and unpublished."
];
const guidanceUnavailable = [
	"Priority calculation, effort estimates and consultant overrides are unavailable.",
	"Customer-approved objectives, roles and approvals are unavailable.",
	"Fix artifacts, recommendation review, task conversion/workflows and CSV export are unavailable.",
	"Customer risk acceptance, validated remediation, reassessment and actual report publication/sharing are unavailable.",
	"No SQL, script, configuration artifact, customer-system execution, external task connector or ROI calculation exists."
];
const fixedVersions = {
	profileVersion: "synthetic-profile-v1",
	scoringAlgorithmVersion: "pilot-health-v1",
	aiPolicyVersion: "synthetic-ai-disabled-v1",
	promptVersion: "synthetic-prompt-disabled-v1",
	modelVersion: "synthetic-model-disabled-v1",
	applicationVersion: "synthetic-fix-packages-app-v1",
	workSchemaVersion: "synthetic-run-work-v1"
};
const fixedCapability = {
	matrixVersion: "synthetic-analysis-matrix-v1",
	stateAtLock: "FixtureVerified",
	productBuild: "fixture-product-v1",
	databaseSchemaBuild: "fixture-facts-v1",
	hotfixSetDigest: "synthetic-hotfix-digest",
	sqlServerBuild: "synthetic-sql-build",
	queryPackVersion: "synthetic-query-pack-v1",
	normalizationSchemaVersion: "synthetic-normalization-v1",
	ruleCatalogVersion: "synthetic-analysis-catalog-v1"
};
function shape(value, keys) {
	return value !== null && typeof value === "object" && !Array.isArray(value) && Object.keys(value).length === keys.length && keys.every((key) => Object.hasOwn(value, key));
}
const digest = (value) => typeof value === "string" && /^[a-f0-9]{64}$/.test(value);
const integer = (value) => Number.isSafeInteger(value) && value >= 0;
const uuid = (value) => typeof value === "string" && /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/.test(value) && value !== "00000000-0000-0000-0000-000000000000";
function text(value, blank = false) {
	if (typeof value !== "string" || value.length > 16384 || !blank && /^\p{White_Space}*$/u.test(value)) return false;
	for (let index = 0; index < value.length; index++) {
		const unit = value.charCodeAt(index);
		if (unit >= 55296 && unit <= 56319) {
			const next = value.charCodeAt(++index);
			if (!(next >= 56320 && next <= 57343)) return false;
		} else if (unit >= 56320 && unit <= 57343) return false;
	}
	return true;
}
const array = (value) => Array.isArray(value) && value.length <= maximumRecords;
const texts = (value, required = false) => array(value) && (!required || value.length > 0) && value.every((item) => text(item));
const same = (a, b) => canonical(a) === canonical(b);
function sameSet(a, b) {
	return Array.isArray(a) && Array.isArray(b) && a.length === b.length && new Set(a).size === a.length && new Set(b).size === b.length && same([...a].sort(), [...b].sort());
}
function sorted(values, key) {
	return values.every((item, index) => index === 0 || key(values[index - 1]) < key(item));
}
// Default .NET escaping and ordinal keys for this closed graph. Its numbers are
// safe integers; arbitrary decimal/exponent values are not admitted.
function canonical(value, depth = 0) {
	if (depth > 32) throw new Error("Invalid package depth");
	if (typeof value === "string") {
		let result = "\"";
		const simple = {
			"\\": "\\\\",
			"\b": "\\b",
			"\f": "\\f",
			"\n": "\\n",
			"\r": "\\r",
			"	": "\\t"
		};
		for (let index = 0; index < value.length; index++) {
			const unit = value.charCodeAt(index);
			const char = value[index];
			if (Object.hasOwn(simple, char)) result += simple[char];
			else if (unit < 32 || unit >= 127 || "<>&'\"+`".includes(char)) result += `\\u${unit.toString(16).toUpperCase().padStart(4, "0")}`;
			else result += char;
		}
		return result + "\"";
	}
	if (value === null) return "null";
	if (typeof value === "boolean") return value ? "true" : "false";
	if (Number.isSafeInteger(value)) return String(value);
	if (Array.isArray(value)) return "[" + value.map((item) => canonical(item, depth + 1)).join(",") + "]";
	if (typeof value === "object") {
		const record = value;
		return "{" + Object.keys(record).sort().map((key) => canonical(key, depth + 1) + ":" + canonical(record[key], depth + 1)).join(",") + "}";
	}
	throw new Error("Unsupported package value");
}
function scope(value) {
	return shape(value, [
		"customerId",
		"projectId",
		"environmentId"
	]) && value.customerId === "synthetic-customer" && value.projectId === "synthetic-project" && value.environmentId === "synthetic-environment";
}
function locks(run) {
	return [
		["Baseline", run.selection.baselineId],
		["Profile", fixedVersions.profileVersion],
		["Rule catalog", fixedCapability.ruleCatalogVersion],
		["Capability", fixedCapability.matrixVersion],
		["Desired outcomes", "disabled"],
		["Scoring algorithm", fixedVersions.scoringAlgorithmVersion],
		["AI policy", fixedVersions.aiPolicyVersion],
		["Prompt", fixedVersions.promptVersion],
		["Model", fixedVersions.modelVersion],
		["Application", fixedVersions.applicationVersion],
		["Work schema", fixedVersions.workSchemaVersion],
		["Complete frozen input", "synthetic-input-lock-v1"],
		["Exact capability tuple", fixedCapability.matrixVersion],
		["Scripted result fixture", "synthetic-outcomes-v1"],
		["Frozen analysis contents", "synthetic-analysis-lock-v1"],
		["Fictional fix-package templates", templateVersion]
	];
}
function bound(preview, run) {
	if (!shape(preview, [
		"schemaVersion",
		"runId",
		"runRevision",
		"runInputDigest",
		"baselineId",
		"profileId",
		"status",
		"reasonCode",
		"snapshot"
	]) || run.schemaVersion !== 1 || run.demoOnly !== true || !uuid(run.runId) || !integer(run.revision) || run.revision < 1 || run.selection.scopeId !== "demo-scope" || run.selection.profileId !== profile || ![
		"synthetic-analysis-healthy-v1",
		"synthetic-analysis-findings-v1",
		"synthetic-analysis-mixed-v1",
		"synthetic-analysis-gaps-v1"
	].includes(run.selection.baselineId) || preview.schemaVersion !== "synthetic-fix-package-demo-v1" || preview.runId !== run.runId || preview.runRevision !== run.revision || preview.profileId !== profile || preview.baselineId !== run.selection.baselineId || !digest(preview.runInputDigest) || !array(run.lockedInputs) || run.lockedInputs.length !== locks(run).length || new Set(run.lockedInputs.map((item) => item.name)).size !== run.lockedInputs.length || !run.lockedInputs.every((item) => shape(item, [
		"name",
		"version",
		"sha256"
	]) && text(item.name) && text(item.version) && digest(item.sha256))) return false;
	const lock = (name) => run.lockedInputs.find((item) => item.name === name);
	return locks(run).every(([name, version]) => lock(name)?.version === version) && preview.runInputDigest === lock("Complete frozen input")?.sha256 && lock("Fictional fix-package templates")?.sha256 === templateDigest;
}
function validSource(s, value, run) {
	if (!shape(s, [
		"scope",
		"runId",
		"runRevision",
		"runState",
		"runInputDigest",
		"baselineId",
		"profileId",
		"frozenVersions",
		"capabilityLock",
		"analysisLock",
		"analysisFixtureDigest",
		"analysisContentDigest",
		"savedCoverageDigest",
		"reviewRunId",
		"reviewRunRevision",
		"reviewSnapshotDigest"
	]) || !scope(s.scope)) return false;
	const v = s.frozenVersions, c = s.capabilityLock, a = s.analysisLock;
	if (!shape(v, [
		...Object.keys(fixedVersions),
		"desiredOutcomeVersion",
		"scriptedResultsDigest",
		"analysisFixtureDigest",
		"maturityFixtureDigest",
		"fixPackageTemplateDigest"
	]) || !Object.entries(fixedVersions).every(([key, fixed]) => v[key] === fixed) || v.desiredOutcomeVersion !== null || v.fixPackageTemplateDigest !== templateDigest || !digest(v.scriptedResultsDigest) || !digest(v.maturityFixtureDigest) || v.analysisFixtureDigest !== s.analysisFixtureDigest || !shape(c, [
		...Object.keys(fixedCapability),
		"compatibilityLevel",
		"modules",
		"lockDigest"
	]) || !Object.entries(fixedCapability).every(([key, fixed]) => c[key] === fixed) || c.compatibilityLevel !== 160 || !digest(c.lockDigest) || !array(c.modules) || c.modules.length !== 2 || !c.modules.every((m) => shape(m, ["id", "version"]) && m.version === "synthetic-module-v1") || !sameSet(c.modules.map((m) => m.id), ["SyntheticOperations", "SyntheticSecurity"]) || !shape(a, [
		"scope",
		"packVersion",
		"packDigest",
		"presetId",
		"presetVersion",
		"evidenceDigest",
		"catalogVersion",
		"catalogDigest",
		"profileId",
		"profileVersion",
		"profileDigest",
		"compatibility"
	]) || !scope(a.scope) || a.packVersion !== "synthetic-analysis-pack-v1" || a.presetVersion !== "synthetic-evidence-v1" || a.presetId !== run.selection.baselineId || a.catalogVersion !== c.ruleCatalogVersion || a.profileId !== "synthetic-analysis-equal-v1" || a.profileVersion !== v.profileVersion || ![
		a.packDigest,
		a.evidenceDigest,
		a.catalogDigest,
		a.profileDigest
	].every(digest) || !shape(a.compatibility, [
		"sourceProduct",
		"productVersion",
		"evidenceSchemaVersion",
		"ruleLanguageVersion"
	]) || a.compatibility.sourceProduct !== "SYNTHETIC-ONLY" || a.compatibility.productVersion !== "fixture-product-v1" || a.compatibility.evidenceSchemaVersion !== "fixture-facts-v1" || a.compatibility.ruleLanguageVersion !== "count-predicate-v1") return false;
	const lock = (name) => run.lockedInputs.find((item) => item.name === name);
	const draft = value.reportDraft?.status === "Ready" ? value.reportDraft.snapshot?.source : null;
	return s.runId === run.runId && s.runId === value.runId && s.runRevision === run.revision && s.runRevision === value.runRevision && s.runState === "Scoring" && s.profileId === profile && s.baselineId === run.selection.baselineId && s.reviewRunId === run.runId && s.reviewRunRevision === run.revision && s.runInputDigest === lock("Complete frozen input")?.sha256 && s.analysisFixtureDigest === value.fixtureDigest && s.analysisFixtureDigest === lock("Frozen analysis contents")?.sha256 && digest(s.analysisFixtureDigest) && s.reviewSnapshotDigest === value.reviewSnapshotDigest && s.reviewSnapshotDigest === value.review?.snapshotDigest && digest(s.reviewSnapshotDigest) && digest(s.analysisContentDigest) && digest(s.savedCoverageDigest) && c.lockDigest === lock("Exact capability tuple")?.sha256 && v.scriptedResultsDigest === lock("Scripted result fixture")?.sha256 && v.scoringAlgorithmVersion === value.algorithmVersion && (!draft || draft.analysisContentDigest === s.analysisContentDigest && draft.savedCoverageDigest === s.savedCoverageDigest && same(draft.frozenVersions, v) && same(draft.capabilityLock, c) && same(draft.analysisLock, a));
}
function validFinding(f, value, run) {
	if (!shape(f, [
		"findingId",
		"ruleId",
		"ruleVersion",
		"categoryId",
		"severity",
		"originalTitle",
		"presentationTitle",
		"businessContext",
		"initialState",
		"currentState",
		"findingRevision",
		"rootCause",
		"occurrences",
		"options",
		"validationGuidance",
		"guidanceReferences",
		"assumptions",
		"limitations"
	]) || !digest(f.findingId) || !text(f.ruleId) || f.ruleVersion !== "synthetic-rule-v1" || !["SECURITY", "OPERATIONS"].includes(f.categoryId) || ![
		"Critical",
		"High",
		"Medium",
		"Low",
		"Informational"
	].includes(f.severity) || !text(f.originalTitle) || !text(f.presentationTitle) || !text(f.businessContext, true) || !text(f.rootCause) || !["Proposed", "AutoConfirmed"].includes(f.initialState) || ![
		"Proposed",
		"AutoConfirmed",
		"Confirmed",
		"Rejected",
		"Deferred"
	].includes(f.currentState) || !integer(f.findingRevision) || f.initialState === "AutoConfirmed" && f.currentState !== "AutoConfirmed" || f.initialState === "Proposed" && f.currentState === "AutoConfirmed" || ["Critical", "High"].includes(f.severity) && f.initialState !== "Proposed" || f.findingRevision === 0 && (f.currentState !== f.initialState || f.presentationTitle !== f.originalTitle || f.businessContext !== "") || !array(f.occurrences) || f.occurrences.length === 0 || !array(f.options) || f.options.length !== 2 || !texts(f.validationGuidance, true) || !texts(f.guidanceReferences, true) || !texts(f.assumptions) || !texts(f.limitations, true)) return false;
	const original = value.findings.find((item) => item.id === f.findingId), current = value.review.findings.find((item) => item.id === f.findingId);
	if (!original || !current || original.rootCauseKey !== f.findingId || original.baselineId !== run.selection.baselineId || original.ruleId !== f.ruleId || original.ruleVersion !== f.ruleVersion || original.category !== f.categoryId || current.category !== f.categoryId || original.severity !== f.severity || original.originalTitle !== f.originalTitle || current.originalTitle !== f.originalTitle || original.title !== f.presentationTitle || current.title !== f.presentationTitle || current.businessContext !== f.businessContext || original.initialState !== f.initialState || current.initialState !== f.initialState || original.state !== f.currentState || current.state !== f.currentState || current.revision !== f.findingRevision || original.rootCause !== f.rootCause || !array(original.recommendations) || original.recommendations.length !== f.options.length || !sameSet(f.guidanceReferences, original.sources) || f.validationGuidance.join(" ") !== original.validationGuidance || !same(f.assumptions, original.assumptions) || !same(f.limitations, original.limitations) || !f.options.every((o) => shape(o, [
		"scopedOptionId",
		"optionId",
		"status",
		"text",
		"prerequisites",
		"risk",
		"recoveryGuidance"
	]) && digest(o.scopedOptionId) && o.status === "Unverified" && [
		o.optionId,
		o.text,
		o.prerequisites,
		o.risk,
		o.recoveryGuidance
	].every((t) => text(t))) || !sorted(f.options, (o) => o.optionId) || !sameSet(f.options.map((o) => o.optionId), ["compare-new-fixture", "inspect-fixture"]) || !sameSet(f.options.map((o) => `${o.text} Prerequisites: ${o.prerequisites} Risk: ${o.risk} Recovery: ${o.recoveryGuidance}`), original.recommendations) || !f.options.every((o) => `${o.text} Prerequisites: ${o.prerequisites} Risk: ${o.risk} Recovery: ${o.recoveryGuidance}` === original.recommendations[o.optionId === "inspect-fixture" ? 0 : 1]) || !f.occurrences.every((o) => shape(o, [
		"occurrenceId",
		"objectId",
		"objectType",
		"moduleId",
		"originalDigest",
		"evidenceReference"
	]) && digest(o.occurrenceId) && digest(o.originalDigest) && text(o.objectId) && text(o.evidenceReference) && o.objectType === "SyntheticControl" && o.moduleId === (f.categoryId === "SECURITY" ? "SyntheticSecurity" : "SyntheticOperations")) || !sorted(f.occurrences, (o) => o.occurrenceId) || !sameSet(f.occurrences.map((o) => o.occurrenceId), current.occurrenceIds) || !sameSet(f.occurrences.map((o) => o.objectId), original.objectIds) || !sameSet(f.occurrences.map((o) => o.originalDigest), original.originalDigests) || !sameSet(f.occurrences.map((o) => o.originalDigest), current.originalDigests) || !sameSet(f.occurrences.map((o) => o.evidenceReference), original.evidenceReferences)) return false;
	return f.occurrences.every((o) => {
		const i = original.objectIds.indexOf(o.objectId);
		return original.originalDigests[i] === o.originalDigest && original.evidenceReferences[i] === o.evidenceReference;
	});
}
function validGuidance(g, value, run) {
	const r = value.review;
	return shape(g, [
		"schemaVersion",
		"status",
		"source",
		"contentDigest",
		"findings",
		"warnings",
		"unavailableSections"
	]) && g.schemaVersion === "synthetic-recommendation-guidance-v1" && g.status === "SyntheticUnverified" && digest(g.contentDigest) && same(g.warnings, guidanceWarnings) && same(g.unavailableSections, guidanceUnavailable) && validSource(g.source, value, run) && shape(r, [
		"schemaVersion",
		"demoOnly",
		"runId",
		"runRevision",
		"status",
		"reasonCode",
		"snapshotDigest",
		"actor",
		"findings"
	]) && r.schemaVersion === 1 && r.demoOnly === true && r.status === "Ready" && r.reasonCode === null && r.runId === run.runId && r.runRevision === run.revision && array(r.findings) && array(value.findings) && array(g.findings) && sorted(g.findings, (f) => f.findingId) && g.findings.length === value.findings.length && g.findings.length === r.findings.length && new Set(value.findings.map((f) => f.id)).size === value.findings.length && new Set(r.findings.map((f) => f.id)).size === r.findings.length && g.findings.every((f) => validFinding(f, value, run));
}
function structural(value, run, preview) {
	if (value.schemaVersion !== 1 || value.demoOnly !== true || value.runId !== run.runId || value.runRevision !== run.revision || !bound(preview, run) || preview !== value.fixPackages && !same(preview, value.fixPackages)) return false;
	if (preview.status === "Unavailable") return preview.snapshot === null && typeof preview.reasonCode === "string" && [
		"fix_packages_source_unavailable",
		"fix_packages_capture_mismatch",
		"fix_packages_integrity_denied"
	].includes(preview.reasonCode);
	const s = preview.snapshot, g = value.recommendationGuidance;
	if (preview.status !== "Ready" || preview.reasonCode !== null || value.status !== "Ready" || value.reasonCode !== null || run.state !== "Scoring" || run.cancelRequested !== false || !shape(run.progress, [
		"plannedUnits",
		"terminalUnits",
		"remainingUnits",
		"allTerminal"
	]) || !integer(run.progress.plannedUnits) || run.progress.plannedUnits < 1 || run.progress.terminalUnits !== run.progress.plannedUnits || run.progress.remainingUnits !== 0 || run.progress.allTerminal !== true || !["Complete", "CompleteWithGaps"].includes(run.coverageCompletionKind ?? "") || !array(run.stateCounts) || new Set(run.stateCounts.map((c) => c.state)).size !== run.stateCounts.length || !run.stateCounts.every((c) => shape(c, ["state", "count"]) && integer(c.count) && [
		"Pass",
		"Finding",
		"NotApplicable",
		"NotAssessed",
		"InsufficientEvidence",
		"Excluded",
		"Inaccessible",
		"Redacted",
		"Unsupported",
		"Error"
	].includes(c.state)) || run.stateCounts.reduce((sum, c) => sum + c.count, 0) !== run.progress.terminalUnits || run.coverageCompletionKind !== (run.stateCounts.some((c) => c.count > 0 && ![
		"Pass",
		"Finding",
		"NotApplicable"
	].includes(c.state)) ? "CompleteWithGaps" : "Complete") || !shape(g, [
		"status",
		"reasonCode",
		"snapshot"
	]) || g.status !== "Ready" || g.reasonCode !== null || !shape(s, [
		"schemaVersion",
		"status",
		"disclaimer",
		"guidance",
		"templateVersion",
		"templateDigest",
		"templates",
		"packages",
		"warnings",
		"unavailableSections",
		"canonicalJson",
		"contentDigest"
	]) || s.schemaVersion !== "synthetic-fix-package-preview-v1" || s.status !== "Unverified" || s.disclaimer !== disclaimer || s.templateVersion !== templateVersion || s.templateDigest !== templateDigest || typeof s.canonicalJson !== "string" || s.canonicalJson.length > maximumBytes || new TextEncoder().encode(s.canonicalJson).length > maximumBytes || !digest(s.contentDigest) || !validGuidance(s.guidance, value, run) || !same(s.guidance, g.snapshot) || !same(s.templates, templates) || !same(s.unavailableSections, unavailable) || !same(s.warnings, s.guidance.findings.length === 0 ? [...warnings, "No findings were supplied; no fix packages or actions are available."] : warnings) || !array(s.packages) || s.packages.length !== s.guidance.findings.length || !sorted(s.packages, (p) => p.findingId)) return false;
	let records = s.packages.length;
	for (const p of s.packages) {
		const f = s.guidance.findings.find((item) => item.findingId === p.findingId);
		if (!shape(p, [
			"packageId",
			"findingId",
			"options"
		]) || !digest(p.packageId) || !f || !array(p.options) || p.options.length !== f.options.length || !sorted(p.options, (o) => o.scopedOptionId) || !sameSet(p.options.map((o) => o.scopedOptionId), f.options.map((o) => o.scopedOptionId))) return false;
		records += p.options.length;
		for (const o of p.options) {
			if (!shape(o, ["scopedOptionId", "artifacts"]) || !digest(o.scopedOptionId) || !array(o.artifacts) || o.artifacts.length !== templates.length) return false;
			records += o.artifacts.length;
			if (!o.artifacts.every((a, i) => shape(a, [
				"artifactId",
				"templateId",
				"kind",
				"status",
				"text"
			]) && digest(a.artifactId) && a.status === "Unverified" && a.templateId === templates[i].templateId && a.kind === templates[i].kind && a.text === templates[i].text)) return false;
		}
		if (records > maximumRecords) return false;
	}
	const { canonicalJson, contentDigest: _digest, ...payload } = s;
	const parsed = JSON.parse(canonicalJson);
	// Canonical parse equality rejects duplicate keys and alternate escapes/order;
	// complete actual-property equality rejects stale or self-consistent forged caches.
	return canonical(parsed) === canonicalJson && canonical(payload) === canonicalJson;
}
async function hash(text) {
	const bytes = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text));
	return Array.from(new Uint8Array(bytes), (b) => b.toString(16).padStart(2, "0")).join("");
}
export async function coherentFixPackages(value, run) {
	try {
		if (value.schemaVersion !== 1 || value.demoOnly !== true || value.runId !== run.runId || value.runRevision !== run.revision) return false;
		if (run.selection.profileId !== profile) return value.fixPackages === null && array(run.lockedInputs) && !run.lockedInputs.some((i) => i.name === "Fictional fix-package templates");
		const preview = value.fixPackages;
		if (!preview || !structural(value, run, preview)) return false;
		if (!(await Promise.all(locks(run).slice(0, 11).map(async ([name, version]) => run.lockedInputs.find((i) => i.name === name).sha256 === await hash(version)))).every(Boolean)) return false;
		if (preview.status === "Unavailable") return structural(value, run, preview);
		const s = preview.snapshot, source = s.guidance.source, originalCanonical = s.canonicalJson, originalDigest = s.contentDigest;
		const { contentDigest: _guidanceDigest, ...guidancePayload } = s.guidance;
		if (await hash(s.canonicalJson) !== s.contentDigest || await hash(canonical(guidancePayload)) !== s.guidance.contentDigest || await hash(canonical({
			templateVersion,
			templates
		})) !== s.templateDigest) return false;
		const identities = [];
		for (const p of s.packages) {
			identities.push(hash(canonical({
				findingId: p.findingId,
				runId: source.runId,
				scope: source.scope
			})).then((id) => id === p.packageId));
			const f = s.guidance.findings.find((item) => item.findingId === p.findingId);
			for (const o of f.options) identities.push(hash(canonical({
				findingId: f.findingId,
				optionId: o.optionId,
				runId: source.runId,
				scope: source.scope
			})).then((id) => id === o.scopedOptionId));
			for (const o of p.options) for (const a of o.artifacts) identities.push(hash(canonical({
				packageId: p.packageId,
				scopedOptionId: o.scopedOptionId,
				templateId: a.templateId,
				templateVersion
			})).then((id) => id === a.artifactId));
			for (const o of f.occurrences) identities.push(hash(canonical({
				EvidenceDigest: source.analysisLock.evidenceDigest,
				Id: f.ruleId,
				ObjectId: o.objectId,
				PresetId: source.analysisLock.presetId,
				Scope: {
					CustomerId: source.scope.customerId,
					EnvironmentId: source.scope.environmentId,
					ProjectId: source.scope.projectId
				},
				Version: f.ruleVersion,
				runId: source.runId
			})).then((id) => id === o.occurrenceId));
		}
		return (await Promise.all(identities)).every(Boolean) && s.canonicalJson === originalCanonical && s.contentDigest === originalDigest && structural(value, run, preview);
	} catch {
		return false;
	}
}
export function FixPackagePreview({ preview, run, analysis }) {
	if (preview === null) return null;
	let coherent = false;
	try {
		coherent = structural(analysis, run, preview);
	} catch {}
	if (!coherent || preview.status !== "Ready" || !preview.snapshot) return /* @__PURE__ */ _jsxs("section", {
		className: "fix-package-preview",
		"aria-label": "Fictional fix packages",
		children: [
			/* @__PURE__ */ _jsx("h3", {
				id: "fix-package-preview-heading",
				tabIndex: -1,
				children: "Fictional fix packages"
			}),
			/* @__PURE__ */ _jsx("p", {
				role: "status",
				children: "Fictional fix packages are unavailable for this captured source. No artifact, approval or health result is inferred."
			}),
			coherent && preview.status === "Unavailable" && /* @__PURE__ */ _jsxs("p", { children: ["Reason: ", /* @__PURE__ */ _jsx("code", { children: preview.reasonCode })] })
		]
	});
	const s = preview.snapshot;
	return /* @__PURE__ */ _jsxs("section", {
		className: "fix-package-preview",
		"aria-label": "Fictional fix packages",
		children: [
			/* @__PURE__ */ _jsx("p", {
				className: "eyebrow",
				children: "Fictional review-only examples"
			}),
			/* @__PURE__ */ _jsx("h3", {
				id: "fix-package-preview-heading",
				tabIndex: -1,
				children: "Fictional fix packages"
			}),
			/* @__PURE__ */ _jsxs("div", {
				className: "fix-package-warning",
				role: "note",
				children: [
					/* @__PURE__ */ _jsx("strong", { children: "Unverified · Review-only" }),
					/* @__PURE__ */ _jsx("p", { children: s.disclaimer }),
					/* @__PURE__ */ _jsx("p", { children: "Finding decisions do not review artifacts or validate remediation. Stable identity order is not priority or effort." })
				]
			}),
			s.packages.length === 0 ? /* @__PURE__ */ _jsx("p", {
				className: "fix-package-empty",
				role: "status",
				children: "No findings were supplied; no fix packages or actions are available. Empty input does not establish healthy coverage or validated remediation."
			}) : s.packages.map((p) => {
				const f = s.guidance.findings.find((item) => item.findingId === p.findingId);
				return /* @__PURE__ */ _jsxs("details", {
					className: "fix-package-group",
					children: [
						/* @__PURE__ */ _jsxs("summary", { children: [
							f.presentationTitle,
							" · ",
							f.currentState,
							" · Unverified artifacts"
						] }),
						/* @__PURE__ */ _jsx("h4", { children: "Current finding and immutable original" }),
						/* @__PURE__ */ _jsx(Rows, { values: [
							["Package ID", p.packageId],
							["Finding ID", f.findingId],
							["Current title", f.presentationTitle],
							["Current finding state", f.currentState],
							["Finding revision", String(f.findingRevision)],
							["Business context", f.businessContext || "No business context supplied."],
							["Original title", f.originalTitle],
							["Original finding state", f.initialState],
							["Rule ID", f.ruleId],
							["Rule version", f.ruleVersion],
							["Category", f.categoryId],
							["Severity", f.severity],
							["Root cause", f.rootCause]
						] }),
						/* @__PURE__ */ _jsxs("details", { children: [
							/* @__PURE__ */ _jsx("summary", { children: "Original occurrences and evidence provenance" }),
							/* @__PURE__ */ _jsx("p", { children: "Evidence references are inert identifiers; no evidence is resolved or opened." }),
							f.occurrences.map((o) => /* @__PURE__ */ _jsx("article", { children: /* @__PURE__ */ _jsx(Rows, { values: [
								["Occurrence ID", o.occurrenceId],
								["Object ID", o.objectId],
								["Object type", o.objectType],
								["Module", o.moduleId],
								["Original digest", o.originalDigest],
								["Evidence reference", o.evidenceReference]
							] }) }, o.occurrenceId))
						] }),
						p.options.map((o) => {
							const original = f.options.find((item) => item.scopedOptionId === o.scopedOptionId);
							return /* @__PURE__ */ _jsxs("details", {
								className: "fix-package-option",
								children: [
									/* @__PURE__ */ _jsxs("summary", { children: [original.optionId, " · Unverified"] }),
									/* @__PURE__ */ _jsx(Rows, { values: [
										["Scoped option ID", original.scopedOptionId],
										["Original option ID", original.optionId],
										["Recommendation status", original.status],
										["Original option guidance", original.text],
										["Prerequisites", original.prerequisites],
										["Risk", original.risk],
										["Recovery guidance", original.recoveryGuidance]
									] }),
									o.artifacts.map((a) => /* @__PURE__ */ _jsxs("article", {
										className: "fix-package-artifact",
										children: [
											/* @__PURE__ */ _jsxs("h5", { children: [a.kind, " · Unverified"] }),
											/* @__PURE__ */ _jsx(Rows, { values: [
												["Artifact ID", a.artifactId],
												["Template ID", a.templateId],
												["Artifact kind", a.kind],
												["Artifact status", a.status]
											] }),
											/* @__PURE__ */ _jsx("pre", { children: /* @__PURE__ */ _jsx("code", { children: a.text }) })
										]
									}, a.artifactId))
								]
							}, o.scopedOptionId);
						}),
						/* @__PURE__ */ _jsxs("details", { children: [
							/* @__PURE__ */ _jsx("summary", { children: "Validation, references, assumptions and limitations" }),
							/* @__PURE__ */ _jsx("h5", { children: "Validation guidance" }),
							/* @__PURE__ */ _jsx(TextList, { values: f.validationGuidance }),
							/* @__PURE__ */ _jsx("h5", { children: "Guidance references" }),
							/* @__PURE__ */ _jsx(TextList, { values: f.guidanceReferences }),
							/* @__PURE__ */ _jsx("h5", { children: "Assumptions" }),
							/* @__PURE__ */ _jsx(TextList, { values: f.assumptions }),
							/* @__PURE__ */ _jsx("h5", { children: "Finding limitations" }),
							/* @__PURE__ */ _jsx(TextList, { values: f.limitations })
						] })
					]
				}, p.packageId);
			}),
			/* @__PURE__ */ _jsxs("details", { children: [
				/* @__PURE__ */ _jsx("summary", { children: "Current package warnings and unavailable capabilities" }),
				/* @__PURE__ */ _jsx("h4", { children: "Current warnings" }),
				/* @__PURE__ */ _jsx(TextList, { values: s.warnings }),
				/* @__PURE__ */ _jsx("h4", { children: "Current unavailable capabilities" }),
				/* @__PURE__ */ _jsx(TextList, { values: s.unavailableSections })
			] }),
			/* @__PURE__ */ _jsxs("details", { children: [
				/* @__PURE__ */ _jsx("summary", { children: "Historical upstream guidance boundary" }),
				/* @__PURE__ */ _jsx("p", { children: "These retained statements describe the earlier guidance projection. Current fictional package limitations are stated separately above." }),
				/* @__PURE__ */ _jsx(Rows, { values: [["Guidance schema", s.guidance.schemaVersion], ["Guidance status", s.guidance.status]] }),
				/* @__PURE__ */ _jsx("h4", { children: "Historical guidance warnings" }),
				/* @__PURE__ */ _jsx(TextList, { values: s.guidance.warnings }),
				/* @__PURE__ */ _jsx("h4", { children: "Historical upstream unavailable capabilities" }),
				/* @__PURE__ */ _jsx(TextList, { values: s.guidance.unavailableSections })
			] }),
			/* @__PURE__ */ _jsxs("details", { children: [
				/* @__PURE__ */ _jsx("summary", { children: "Fixed fictional templates" }),
				/* @__PURE__ */ _jsx(Rows, { values: [["Template version", s.templateVersion], ["Template catalog digest", s.templateDigest]] }),
				s.templates.map((t) => /* @__PURE__ */ _jsxs("article", { children: [
					/* @__PURE__ */ _jsxs("h4", { children: [t.kind, " template · Unverified"] }),
					/* @__PURE__ */ _jsx(Rows, { values: [["Template ID", t.templateId], ["Template kind", t.kind]] }),
					/* @__PURE__ */ _jsx("pre", { children: /* @__PURE__ */ _jsx("code", { children: t.text }) })
				] }, t.templateId))
			] }),
			/* @__PURE__ */ _jsxs("details", {
				className: "fix-package-source",
				children: [
					/* @__PURE__ */ _jsx("summary", { children: "Captured source, versions and digests" }),
					/* @__PURE__ */ _jsx("p", { children: "Digests identify this captured fictional value and grant no access. This view does not establish durable artifact review history or a completed assessment." }),
					/* @__PURE__ */ _jsx(Rows, { values: [
						["Preview schema", s.schemaVersion],
						["Preview status", s.status],
						["Package content digest", s.contentDigest],
						["Guidance content digest", s.guidance.contentDigest],
						["Detail schema", preview.schemaVersion],
						["Detail status", preview.status],
						["Run ID", preview.runId],
						["Run revision", String(preview.runRevision)],
						["Complete frozen input digest", preview.runInputDigest],
						["Baseline", preview.baselineId],
						["Profile", preview.profileId]
					] }),
					/* @__PURE__ */ _jsx(Rows, { values: sourceRows(s.guidance.source) })
				]
			})
		]
	});
}
function Rows({ values }) {
	return /* @__PURE__ */ _jsx("dl", { children: values.map(([label, value]) => /* @__PURE__ */ _jsxs("div", { children: [/* @__PURE__ */ _jsx("dt", { children: label }), /* @__PURE__ */ _jsx("dd", { children: value })] }, label)) });
}
function TextList({ values }) {
	return values.length ? /* @__PURE__ */ _jsx("ul", { children: values.map((value, index) => /* @__PURE__ */ _jsx("li", { children: value }, index)) }) : /* @__PURE__ */ _jsx("p", { children: "No values supplied." });
}
function sourceRows(source) {
	const result = [];
	function visit(value, path) {
		if (Array.isArray(value)) value.forEach((item, index) => visit(item, `${path}[${index}]`));
		else if (value !== null && typeof value === "object") Object.keys(value).sort().forEach((key) => visit(value[key], path ? `${path}.${key}` : key));
		else result.push([path, value === null ? "Not supplied" : String(value)]);
	}
	visit(source, "");
	return result;
}
