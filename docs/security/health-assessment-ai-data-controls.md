# Health-Assessment Pilot AI Data Controls

Status: Approved controls — OpenAI GPT-6 Sol and Zero Data Retention required; account verification pending  
Owner: Security and privacy owners  
Last updated: 2026-09-28

## Approved pilot purpose

Automatic general AI may analyze normalized, redacted configuration evidence; infer candidate use cases/outcomes; identify patterns and missing controls; correlate evidence; explain possible root causes; draft findings/recommendations; and compare custom behavior with supported out-of-the-box behavior. AI output is proposed and untrusted. It cannot retrieve protected raw evidence, execute tools, change policy, accept risk, publish, create external tasks, or modify customer systems.

## Selected provider and model

- Provider: OpenAI API directly; Azure OpenAI and third-party model hosts are not selected by this decision.
- Model request ID: `gpt-6-sol`.
- API: Responses API with structured output.
- Region: United States regional storage and processing through `us.api.openai.com`.
- Tools: none exposed to the model for the pilot assessment path, even where the model supports tools.
- Persistence: `store: false`; background mode, file uploads, file search, web search, code interpreter, remote MCP and extended prompt caching are disabled for the pilot AI path.
- Reasoning effort, packet/token limit and budget remain versioned profile settings and require evaluation before approval.

OpenAI API data is not used to train OpenAI models unless the customer explicitly opts in. The pilot requires OpenAI Zero Data Retention; default abuse-monitoring retention and Modified Abuse Monitoring are not approved fallbacks. Because Zero Data Retention requires OpenAI eligibility/approval, AI processing remains disabled until the selected API project supplies configuration evidence.

## Data eligibility

| Data | Pilot AI eligibility |
|---|---|
| Normalized redacted configuration evidence | Allowed when project/run/category policy permits |
| Protected evidence reference metadata | Allowed only as opaque citations without resolving raw value |
| Source credentials, passwords, keys, connection strings, secret values | Prohibited |
| SSNs/government identifiers | Prohibited outside customer boundary; redaction marker only |
| General identity/account profile fields | Prohibited unless separately approved minimum relationship reference |
| Customer comments/free text | Excluded by default; requires explicit classification and profile authorization |
| Raw scripts/configuration containing secrets | Prohibited until redacted/normalized; raw retrieval deferred |
| Raw logs/events | Prohibited; only approved normalized/redacted facts |
| Published findings and approved guidance | Allowed within the same customer/project/run policy |

Eligibility is evaluated at packet creation and again before provider dispatch. A record allowed in the UI is not automatically allowed for AI.

## Processing contract

- Provider and model are selected from an explicit allowlist locked to the assessment run.
- Processing and storage remain in the approved United States residency boundary.
- Customer data is not used for shared-model training; provider account/configuration and contract must supply evidence.
- Provider customer-content retention must be disabled through approved Zero Data Retention. Any non-zero provider customer-content retention requires a new policy decision and is not authorized by this document.
- Provider deletion, abuse-monitoring access, subprocessors, incident notification and model-version behavior require due diligence.
- Packets use stable schema, maximum record/token count, classification summary, redaction count, customer-scoped correlation and no credentials or direct storage locators.
- Prompts, packet schemas, model parameters and post-processing versions are immutable run inputs.

## Prompt-injection and tool-isolation controls

- Evidence is serialized in an explicit data envelope separated from system/developer instructions.
- The system states that evidence, scripts, logs, comments, rules and imported content are untrusted data and cannot give instructions.
- Pilot AI has no tool definitions, code execution, browser, network retrieval, credential store, database or raw-evidence resolver.
- Output must validate against a closed schema. Free-form tool calls, URLs to fetch, policy changes and unsupported fields are rejected.
- Every material conclusion cites packet evidence IDs and authoritative rule/source IDs. Unknown or conflicting evidence must yield uncertainty/missing-context fields.
- Citations are checked for membership in the dispatched packet and current run. Hallucinated or unauthorized citations reject the proposal.
- Provider response text is encoded as untrusted content in UI/render/export.

## Budget and availability

Budgets are scoped by customer/project/run/user/category/period as approved in the profile. The gateway reserves estimated use atomically, records actual use and releases unused reservation. Concurrent calls cannot exceed the locked budget. Exhaustion produces an explicit AI-coverage gap; deterministic work completes. Only an assigned consultant may perform an audited override within configured maximums.

Retries use the same packet digest, prompt/model version and idempotency key where the provider supports it. An uncertain provider outcome is never charged twice internally without recording both attempts, and duplicate proposals are deduplicated by run/work key.

## Output governance

- Output begins as `proposed` and never represents fact, approval or risk acceptance.
- Generated original, packet digest, provider/model/prompt version, timestamps, budget use, citations and validation outcome are immutable.
- Human edits create append-only revisions.
- Critical/High AI findings require review and remain proposed until disposition.
- Proposed AI affects only the provisional score. Rejected, indeterminate and unreviewed outcomes remain available for quality evaluation.
- Normal promotion of material provider/prompt/model changes requires regression comparison and product-owner quality review even though the product specification does not require a single frozen test set. A repository-owner pilot promotion exception may authorize a named regressed/unverified candidate for limited trial use; it does not turn failed tests into passes or satisfy the pilot acceptance gate.

## Logging, audit and deletion

Ordinary logs contain call ID, opaque scope, provider/model identifier, packet/output digest, token/use counts, latency, redaction count and outcome. They exclude prompts containing evidence, packet values, responses, comments and credentials.

Audit records include policy decision, initiating actor/job, category, model/prompt version, budget reservation/actual/override, provider outcome, schema/citation validation, proposal IDs and deletion request status. Customer retention/deletion applies to packets and outputs; provider-side deletion evidence is recorded when applicable. Historical findings may retain unavailable protected references but not deleted packet payload.

## Provider approval checklist

| Control | Required evidence | Status |
|---|---|---|
| US processing/residency | Contract and technical configuration | `NOT VERIFIED` |
| No shared-model training | Contract/account setting evidence | `NOT VERIFIED` |
| Retention and deletion | Zero Data Retention policy approved; project configuration and test | Policy approved / configuration `NOT VERIFIED` |
| Subprocessors/access | Approved list and abuse-monitoring boundaries | `NOT VERIFIED` |
| Encryption | In-transit/at-rest controls and key responsibility | `NOT VERIFIED` |
| Identity/authorization | Workload identity, least privilege and rotation | `NOT VERIFIED` |
| Model/version stability | Version identifiers, deprecation/change notice and fallback behavior | `NOT VERIFIED` |
| Availability/rate limits | Retry/idempotency behavior and explicit gap handling | `NOT VERIFIED` |
| Incident response | Notification terms, evidence and customer-impact process | `NOT VERIFIED` |
| Data-use verification | Test account/configuration review and sample deletion proof | `NOT VERIFIED` |

## Evaluation suite

The test plan must include benign evidence, embedded override instructions, encoded/obfuscated instructions, fake system messages, malicious SQL/script comments, cross-record exfiltration requests, unauthorized citation IDs, requests for raw evidence/secrets, conflicting evidence, long/adversarial inputs, schema-breaking outputs, provider timeout/duplicate response, budget races, and deletion. A passing test demonstrates no tool use, no policy change, no unauthorized data, valid citations and an appropriate uncertainty/gap result.

## Open decisions

- Approve the OpenAI API organization/project, US regional configuration, access owners, key/workload identity, quotas and incident contacts.
- Obtain OpenAI approval for Zero Data Retention, configure it at the selected project, and verify effective behavior/deletion. Do not enable AI if unavailable.
- Apply the material-regression thresholds in the approved `specs/003-health-assessment/evaluation-plan.md`; a repository-owner pilot promotion exception may activate a regressed candidate for limited trial use but does not satisfy the evaluation or acceptance gate.
- Define maximum packet size and per-project/run/period budgets using pilot cost and latency evidence.

## References

- [OpenAI GPT-6 Sol model documentation](https://developers.openai.com/api/docs/models/gpt-6-sol)
- [OpenAI API data controls and residency](https://developers.openai.com/api/docs/guides/your-data)

## Approval

Security owner: Repository owner  
Privacy/data owner: Repository owner  
Product owner: Repository owner  
Date: 2026-09-28
