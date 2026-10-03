# Private pilot browser access: preparation and proposed execution

Status: LOCAL TEMPLATES VERIFIED — PA01 and local PA02 complete; configured hosted checks PASS on0dc58e8; PA03/live deployment gated
Owner: Azure/BFF coordinator
Date: 2026-10-02
Approved product/technical basis: [feature003](../../specs/003-health-assessment/product-spec.md), [technical specification](../../specs/003-health-assessment/technical-spec.md), [test plan](../../specs/003-health-assessment/test-plan.md), accepted ADR-0004/0009.
New boundary: [Accepted bounded ADR-0010](../../architecture/decisions/ADR-0010-private-pilot-browser-access.md).

## Completed authorized preparation

The owner replaces the personal external customer request with an organizational account, retains private hosting and asks for the easiest access. The protected request was updated under `LOCAL-INTAKE-20261002-01`, with no account details in Git. Email domain alone does not prove work/school identity; actual nonconsumer home-tenant/account/session proof remains required. No consumer policy amendment, public topology, invitation or assignment is pursued. Consultant and customer role requests remain unenrolled; detailed customer roles and licensing/CA still need proof/selection.

Current Microsoft docs and public candidate rates were reviewed. The recommendation is one owner-operated private Windows desktop through free Bastion Developer in the same VNet. Candidate source, exact rates and limits are in [evidence](../../docs/development/evidence/private-browser-access-preparation-20261002.json). No Azure operation, new runtime behavior, VM credential or policy change occurred. Initial independent read-only review confirms this is an owner-operated sequential synthetic test candidate, not simultaneous customer access or an RDS/customer desktop service. It requires local OS login, separate administrative/routine identities, Developer-source NSG proof, explicit desktop egress, app VNet ingress, DNS/cert trust and complete costs. Review performed no runtime/Azure tests; actual proposed packet receives final consistency review below.

## Approved bounded local technical scope

A distinct nondelegated workstation subnet/NIC/NSG, one Standard_B2s Windows VM with supported official OS/browser image and E10 LRS single-writer OS disk, Bastion Developer attachment to the same pilot VNet, private environment-default-domain DNS and the approved private BFF endpoint. All protected parameters require explicit values. No public VM IP, general SSH/RDP rule, peering, VPN, public app ingress, customer Azure role, broad service grants or automatic paid-tier fallback.

The foundation is currently disposed. The later exact composed template must include the recreated prerequisite environment and its total cost; a workstation cannot connect to resources that do not exist. Existing eight DNS zones do not establish the Container Apps environment-default-domain browser zone. Bind observed defaultDomain/staticIp, zone/VNet link and required records to actual environment metadata. Preserve HTTPS certificate validation and canonical host: no direct-IP/self-signed-warning bypass, hosts-file workaround or changed redirect path.

The existing NAT attaches only to Container Apps. A new private workstation subnet requires reviewed explicit outbound connectivity; NAT alone does not filter destinations. Prove the necessary DNS/platform/identity/update/certificate traffic while denying broker/control-plane/protected-data access. Neither implicit outbound nor broad Internet443 or paid firewall is selected as a fallback.

The BFF app requires VNet-scope ingress in an internal environment; existing bootstrap app-only external:false is insufficient. Select/prove the actual immediate ingress peer/header shape against the approved exact-IP v1 seam; subnet CIDR cannot be supplied as a proxy address. Production host, real provider/shared-key adapters, dedicated ring/key, manual SQL scopes and audit-outage preservation remain prerequisites to live sign-in. Network success cannot activate the permanently disabled diagnostic image.

## Protected review inputs

- Approved exact VM subnet, NSG ingress source and destination/port, app ILB destination, default-deny lateral traffic, DNS/platform and required identity/update/patch egress; effective-rule proof.
- Owner-only infrastructure/OS login and credential-handling decision; Developer local-login proof (Entra RDP requires Basic or higher), distinct setup administration and routine nonadministrator RDP use. No password may enter Git/tool logs. Owner performs browser credential setup; separate app sign-in/persona authority remains enforced.
- Exact official Windows image version/browser availability, SKU/region quota/capacity, OS disk/image minimum and price offer; synthetic-only workstation data and browser/clipboard cleanup.
- Actual external account home organization/work-school origin; exact immutable tenant/object IDs, sponsor, customer/project/environment assignment, Customer reviewer or Executive scope, lifecycle and MFA/CA proof. No email/UPN authorization keys.
- Exact build/private publish/pull, deployment artifacts, trusted proxy/key/audit bindings and refreshed priced session/ownership/disposal inventory, named cleanup operator and enforceable session-end deallocation/disposal mechanism (not guest OS shutdown); no blanket reuse of historical budget/session.

## Bounded implementation packets

| Packet | Owner/path | Requirement mapping | Completion evidence |
|---|---|---|---|
| PA01: template input/negative policy | Platform writing worker after approval; new `infra/bicep/modules/pilot-development-private-browser-access.bicep`, `tests/infrastructure/PrivateBrowserAccess/`; no shared root edits | ADR-0010, IP-HAS-002, G1/G3 | Compiled exact optional module; reject public IP, wrong region/SKU, paid Bastion fallback, missing exact reviewed parameters, broad ingress/lateral egress, wrong subnet and protection state; no real grant |
| PA02: browser DNS/network proof | Coordinator owns shared optional composition and operations docs; separate private input file | ADR-0004, IP-HAS-002/003, SEC-PILOT-008/009 | Environment-derived DNS/cert/host checks, VM-only app443 reachability and denied protected data/worker endpoints; exact validate/what-if and inventory |
| PA03: independently reviewed disposable session | Coordinator/operator with separate approval; independent verifier read-only | G1/G3/G7, TP-HAS-009/014/020 | Quota/current rate/delayed-charge checks, exact total priced approval, allowed portal/OS connection, denied external RDP/app and unauthorized callers, preservation and exact cleanup |

The repository owner accepted the reviewed design/local template packet on2026-10-02: “Move forward with bastion but plan Https portal with login after pilot is done.” Freeze the approval/work packet checkpoint and isolate writing workers with explicit path ownership. Coordinator integrates non-author review, canonical evidence and private board. Agent review never replaces owner decision or acceptance. Use fewer workers for dependent DNS/composition changes.

## Test and acceptance matrix — all runtime cases NOT VERIFIED

| ID | Level/case | Expected evidence/result |
|---|---|---|
| PA-T01 | Template/public boundary | VM has no public IP; app environment stays internal/publicNetworkAccess Disabled; no worker/environment route broadening; paid-tier/wrong-region mutation denies |
| PA-T02 | Private DNS and browser HTTPS | VM resolves exact environment app hostname to reviewed private ILB; correct TLS/host works; DNS mismatch/invalid cert/wrong host denies without bypass |
| PA-T03 | Management isolation | Owner portal authorization and reviewed OS login succeed; absent role/wrong credential/public RDP/whole-VNet lateral traffic fail; effective NSG/source captured |
| PA-T04 | Data/egress isolation | VM app443 plus exact essential DNS/platform/identity/update traffic allowed; database/key/evidence/registry/control-plane and worker direct access denied by reviewed boundary |
| PA-T05 | App/proxy authorization | Existing actual BFF positive protocol and unauthenticated/wrong-role/customer/external-unonboarded cases, spoofed headers/hops/peer and replay denials; connectivity alone grants no app authority |
| PA-T06 | Persona lifecycle | Same session cannot borrow another user MFA/roles; product revocation/lifecycle/version denies; sign out and clear operator test persona state; no shared customer OS accounts |
| PA-T07 | Key/audit dependencies | Two replicas/rotation/recovery/negative keys and unavailable audit/unknown COMMIT preserve approved denial/receipt contract; live admission remains blocked until actual outage preservation accepted |
| PA-T08 | Operational/cost recovery | Rejected connection does not upgrade SKU; quota/provider failure preserves disabled state; deallocation stops compute, remaining disk charges tracked; exact owned cleanup does not touch budgets/group/unrelated records |

Template checks: pinned Bicep compile and negative policy; JSON schema/links and Gitleaks; affected infrastructure CI. Runtime changes, if separately approved, require all applicable pinned restore/audit/format/build/unit/realPG/HTTPS/browser/architecture/container/security checks. No test passes from a planned case. Manual portal/OS credentials and developer desktop display are not an accessibility acceptance of the product; supported manual assistive-technology and full TP-HAS-013 remain separate. Cold OS/browser start, B2s burst-credit usability and private HTTPS smoke are recorded without inventing a performance SLA. No RDS or customer Windows session hosting is proposed; no customer receives this workstation credential.

## Rollout, rollback and cost

Select fresh names/parameters after review; preview only exact planned scope. Price the complete recreated foundation and this addition, not merely the VM. Examples from current public catalog: Windows B2s0.0496/hour =1.1904 for24 running hours; E10LRS9.60/month plus operations. VPN comparator VpnGw1AZ0.21/hour =156.24 for744hours before other charges. No full session allowance, quantity, region capacity or what-if is accepted here. Disk continues billing after deallocation; dispose exactly approved VM/NIC/disk/NSG/subnet/DNS/Bastion resources only after evidence preservation. The operator disconnects and revokes applicable sessions before workstation disposal; never destroy shared keys/audit data to roll back.

## Completion and human dependency

Preparation may close when sanitized inputs/options/current rates and independent review are recorded and document/secret checks execute. Design and local templates are accepted; exact access/spending inputs and the full composed session require a fresh owner decision before provisioning. Live G1/G3/G7, Milestone2, image promotion, protected customer tests, production release and simultaneous customer access are NOT VERIFIED.

The private board update remains blocked by automatic approval review of the official publishing helper credential/network path. Exact publishing approval was requested separately and is not inferred from Start work. Last confirmed BFF board snapshot4346e0f is stale for this private-access change; canonical records are authoritative.


## Executed preparation review and checks

Read-only non-author auth_transport review of packet checkpoint564e42e found one monthly-cost wording error:744hours Windows compute is USD36.9024, plus USD9.60 disk = USD46.5024 before operations/foundation. The sentence was corrected without any meter, runtime, template or decision change. Review otherwise found no unsafe selected default or deployment-readiness claim; exact input/live boundaries remain blocked. Coordinator executed changed JSON parsing,193 changed Markdown local-target checks, supplied-account exclusion and documentation-only scope checks, rate arithmetic, git diff --check and whole-checkout Gitleaks8.30.1 with no leaks. Final local checks are repeated after the wording/evidence correction. No infrastructure compilation/build or runtime test is claimed for this documentation-only proposal. Preparation is COMPLETE at checkpoint c02c6ba; the later owner approval below authorizes local templates, while all live cases remain pending.


## Accepted local execution ledger — 2026-10-02

PA01 VERIFIED (bounded local scope): one isolated writing worker owns only the new workstation/Bastion module and `tests/infrastructure/PrivateBrowserAccess/`. Coordinator owns shared composition, workflow, DNS module, operations instructions and all canonical records. Non-author read-only review is required before integration.

PA02 VERIFIED (local portion only): optional standalone access composition references an existing approved VNet/NAT/internal environment; derive browser DNS from actual environment metadata. Do not modify/replay the disposed foundation, bootstrap ingress or BFF host. Parameter examples remain deliberately incomplete and must be rejected before live deployment. Provider/what-if/effective-network/TLS/runtime evidence remain NOT VERIFIED.

Required local tests: pinned Bicep0.47.16 build/lint, compiled-resource policy plus unsafe mutations, protected input validation with synthetic fixtures, affected existing infrastructure checks, JSON/link checks, whole-checkout Gitleaks8.30.1 and diff scope. No public addresses, paid Bastion fallback, customer roles, VM extensions/credentials in outputs or broad outbound rules. Exact Developer source and necessary outbound IP/port inputs have no defaults; require review proof and reject broad rules. An input approval flag is not provider/network evidence.

PA03 BLOCKED FOR LIVE: exact inputs, official pinned image/capacity, owner credential handoff, complete quoted session and cleanup approval, deployable BFF composition and live identity/key/audit gates. No Azure write is authorized by local template verification.

The [post-pilot HTTPS portal plan](post-pilot-https-portal.md) is deferred until the pilot has an attributed completion decision; implementation/public exposure requires a separately reviewed product/technical/test packet and ADR.


## Verified local execution checkpoint

[Executed evidence](../../docs/development/evidence/private-browser-access-implementation-20261002.json) binds the final local source84b53eb, worker97cc60b and non-author auth_transport review. Build/lint of all21 Bicep files passed with zero diagnostics. New cases:49 workstation compiled mutations,48 input negatives,16 DNS mutations,16 composition mutations,19 provider metadata negatives and17 actual combined CLI denials =165; synthetic positive baselines passed. Existing private-services/PostgreSQL/compute/monitor/foundation-composition/inactive-Entra policies passed. JSON/local links, protected-account exclusion, diff and whole-checkout Gitleaks checks executed; final canonical closure repeats them. No new runtime dependency/migration or Azure action. Four additive draft/base conflicts are reconciled for configured combined CI; no hosted pass is claimed before execution.

Scope and output checker findings were corrected and independently reprobed before VERIFIED. No Windows update/agent policy was invented; official image defaults require live patch/browser readiness proof. Required input validation does not authorize deployment or prove a string review reference. Exact provider/effective network/OS/DNS/TLS/cost/cleanup and BFF/identity/key/audit cases remain NOT VERIFIED. Original preparation evidence remains historical. The private board is stale for this checkpoint because its specific publishing action remains rejected/pending approval.


## Hosted verification closure — 2026-10-02

Configured [bootstrap37080912801](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37080912801) and [package37080912872](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37080912872) are terminal PASS on immutable combined source `0dc58e8fe69eb501510a9a5b183bd32f48f66f63`, including Linux infrastructure/optional Bastion and both Windows collector jobs. The earlier pending observations describe their recording checkpoints. No runtime/template change is made by this evidence closure. The clean completed writing checkout was removed after its reviewed source was preserved in the integrated commits and retained branch. Historical GitGuardian digest incidents still need human disposition before merge; no scanner result was suppressed. Live PA03/G1–G9/Milestone2 remain NOT VERIFIED. Private BFF board publication remains blocked by the separately pending automatic-review credential/network action; canonical records are authoritative.
