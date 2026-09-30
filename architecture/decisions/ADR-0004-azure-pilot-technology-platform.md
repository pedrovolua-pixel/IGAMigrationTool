# ADR-0004: Azure pilot technology platform

Status: Accepted  
Date: 2026-09-29  
Decision owners: Technical owner, security owner, operations owner

## Context

The approved health-assessment design and ADR-0001 through ADR-0003 define the application shape, customer isolation boundary, and evidence/publication storage model, but intentionally leave the concrete technology platform open. Implementation planning needs a small, supportable stack for an interactive application, resumable background work, customer-isolated data, immutable artifacts, Microsoft Entra authentication, and direct OpenAI API calls with United States processing and required Zero Data Retention.

Provider resources, application-registration identifiers, project identifiers, endpoints, and credentials do not need to exist for this architectural decision. They are deployment configuration and pilot-readiness evidence. This ADR selects the platform pattern and the controls that those resources must implement.

## Decision drivers

- Integrate cleanly with the approved Microsoft Entra identity pattern and managed workload identities.
- Preserve the modular application and separately scalable worker shape in ADR-0001.
- Preserve the customer data-plane isolation and customer-scoped deletion/restore expectations in ADR-0002.
- Preserve the relational metadata and customer-scoped immutable artifact pattern in ADR-0003.
- Support jobs lasting minutes or hours with checkpoints, retries, dead-letter handling, and idempotency.
- Keep the pilot operationally smaller than a Kubernetes or microservice platform.
- Keep secrets out of source control and production environment files.
- Support United States deployment, private service connectivity for protected data services, tightly restricted public connectivity for the pilot work broker, observability, backup, and recovery.
- Avoid adding search, graph, cache, or orchestration products before measured need exists.

## Options considered

### Option A: Azure managed application platform

Use ASP.NET Core and .NET LTS for the backend and workers, React with TypeScript for the browser UI, and Azure-managed application, database, queue, object-storage, identity, secret, registry, and monitoring services.

- Advantages: strongest fit with Entra, managed identities, the approved asynchronous shape, and a small operations team; supports container portability; avoids cluster administration.
- Disadvantages: creates an Azure operational dependency and requires deliberate controls to prevent platform logs or diagnostics from capturing evidence.
- Risks: service limits, regional availability, and provider-specific infrastructure definitions must be validated before production sizing.

### Option B: Azure Kubernetes Service

- Advantages: maximum scheduling and network-policy control; broad workload portability.
- Disadvantages: cluster lifecycle, ingress, identity, policy, upgrades, security hardening, and observability add substantial pilot work.
- Risks: platform complexity could dominate delivery before scale demonstrates a Kubernetes need.

### Option C: Primarily serverless functions

- Advantages: simple event triggers and consumption-based scaling for short tasks.
- Disadvantages: the application contains long-running, stateful, checkpointed assessments and isolated renderers that do not fit one uniform function model.
- Risks: fragmented execution patterns and timeout/retry behavior could complicate idempotency and recovery.

### Option D: Another public cloud or portable self-managed stack

- Advantages: may reduce Azure-specific coupling and could satisfy the functional contracts.
- Disadvantages: weaker integration with the approved Entra and managed-identity direction and more cross-cloud identity, networking, and operations work.
- Risks: the portability benefit is speculative for the pilot while security and operations costs are immediate.

## Decision

Select **Option A, Azure managed application platform**, with the following pilot baseline:

- **Application:** one modular ASP.NET Core application on the current supported .NET LTS release, deployed in separate interactive/API and worker roles. Pin the exact SDK/runtime in the implementation plan and build artifacts.
- **Browser:** React and TypeScript. Use a backend-for-frontend pattern: the server performs OIDC authorization-code flow with PKCE and maintains an `HttpOnly`, `Secure` session cookie. Ordinary browser JavaScript does not receive Entra access or refresh tokens.
- **Compute:** Azure Container Apps for the web/API and continuously consuming worker roles. Use Azure Container Apps Jobs only for finite, separately sandboxed work such as report rendering, controlled exports, or maintenance jobs where the job execution model is a fit.
- **Pilot source collector:** the One Identity Manager pilot uses an Authenticode-signed, self-contained .NET 10 `win-x64` collector deployed inside the customer-controlled network near SQL Server, outside the Azure-hosted platform boundary. The MSI installs an unattended Windows Service and includes one-shot CLI mode for offline collection on Windows Server 2022 or 2025, including Server Core. It uses `Microsoft.Data.SqlClient` 7.1 with dedicated read-only Windows Integrated Security preferred and locally protected SQL authentication allowed as a fallback. It sends evidence through outbound-only HTTPS after device-certificate enrollment or produces the approved authenticated encrypted offline package. The pilot has no collector container, inbound listener, browser UI, remote self-update or cloud access to the SQL credential.
- **Future hosted connectors:** SaaS-to-SaaS acquisition for other products uses separately specified Azure-hosted connectors that call approved vendor APIs from the platform. Those connectors are not the One Identity pilot collector and are outside this pilot decision; each requires product-specific authentication, rate-limit, permission, residency, retention and failure controls before implementation.
- **Human identity:** one single-tenant confidential web application registration for the pilot UI/BFF, with explicit Entra app roles and web redirect URIs. Its confidential-client credential is a dedicated user-assigned BFF managed identity federated to that registration, subject to the deployment spike in IMP-DEC-006. Do not enable public-client or implicit flows. Add a distinct registration for Phase 1D MCP/API access only when that sub-phase is implemented and its client contract is approved.
- **Workload identity:** a separate managed identity for each workload class and privilege boundary. Do not reuse interactive delegated tokens for jobs. Use a client secret only where managed identity or workload federation is unavailable and separately approved.
- **Relational data:** Azure Database for PostgreSQL Flexible Server. Use one single-primary pilot server with high availability disabled, a separate control-plane database, and a separate database plus database role per customer data plane. Retain automated backups and point-in-time recovery under the approved 24-hour RPO, one-business-day RTO and maximum 35-day backup policy. Customer-specific recovery restores the server into an isolated recovery instance and imports only the authorized customer database. A customer may move to a dedicated or high-availability server without changing the application contract if assurance, scale, recovery evidence or production-readiness requirements justify it.
- **Durable work:** Azure Service Bus **Standard** queues/topics with peek-lock handling, dead-letter queues, duplicate detection where applicable, and application-level idempotency. PostgreSQL is the source of truth for work state; a transactional outbox and reconciliation process safely publish or re-publish delivery signals. Queue messages contain only opaque customer/project/run/work identifiers, schema version, attempt metadata and integrity/correlation values—never evidence payloads, user content, tokens, connection data or credentials.
- **Artifacts:** Azure Blob Storage with separate customer containers and authorization scopes, immutable content digests, versioning, encryption, lifecycle rules, and deletion tombstones. Time-based WORM policies apply only to artifact classes whose approved retention permits them; they must not prevent an authorized customer deletion.
- **Secrets and keys:** Azure Key Vault accessed with managed identities. Production and shared environments do not use committed or manually distributed `.env` files. Local development may use an ignored `.env.local` or .NET user-secrets containing only development values.
- **AI:** the official OpenAI .NET client and Responses API for the already approved `gpt-6-sol` model. OpenAI base URL, project identifier, model, and credential references are runtime configuration. The provider project remains disabled until US regional processing and Zero Data Retention are verified.
- **Images and deployment:** Azure Container Registry and Bicep infrastructure-as-code. Environments use independently deployable parameter files and secret references; secrets are never Bicep parameters stored in the repository.
- **Observability:** OpenTelemetry instrumentation exported to Azure Monitor/Application Insights and Log Analytics, with payload filtering and redaction before export.
- **Internal contracts:** versioned REST/JSON operations and durable work-message schemas. Do not add GraphQL for the pilot.
- **Deliberate omissions:** no Kubernetes, Redis, external search engine, graph database, workflow engine, or service mesh until measured requirements justify one. Use PostgreSQL indexing, full-text capabilities where adequate, relational relationship tables, and explicit application state machines first.
- **Report renderer:** Prince 17 for Linux x86-64 is the selected PDF/UA engine, subject to commercial licensing and a passing accessibility/security/determinism spike before Phase 1C PDF is enabled. It runs only as an isolated Container Apps Job with no network, platform/customer credentials, database, canonical API or arbitrary filesystem access. A read-only Prince vendor license file is the sole narrow credential-like exception and cannot authorize any platform service. Markdown remains independently available when PDF is disabled or fails.

The pilot deploys into one United States Azure region supporting every selected service and the required connectivity and OpenAI residency design. No pilot recovery region, cross-region standby, or geo-redundant database backup is required. Protected data services use private connectivity where supported. Service Bus Standard does not support Service Bus Private Link or virtual-network integration, so its public service endpoint is an explicit pilot exception governed by the controls below. The exact single region is an implementation-plan decision backed by service availability, capacity and customer-residency evidence.

## Rationale

Azure-managed services satisfy the approved identity and workload boundaries without requiring a cluster platform. ASP.NET Core provides one language/runtime for the BFF, domain application and workers, while .NET also supports a separately packaged customer-side collector without placing that collector inside Azure. React keeps the interactive reporting UI independent of server rendering decisions. Container Apps provides separately scalable hosted deployment roles, while PostgreSQL, Service Bus, Blob Storage, Key Vault, and managed identity cover the already documented persistence, work, artifact, and secret contracts.

Database-per-customer and container-per-customer isolation is stronger than shared rows while remaining affordable for a two-environment pilot. The recovery procedure is less direct than a dedicated server per customer, so it must be proven against the approved recovery objective before pilot acceptance.

## Consequences

### Positive

- The platform directly supports Entra, managed identity, Key Vault, private endpoints for protected data services, and Azure monitoring.
- The application remains a modular monolith while risky and long-running work scales independently.
- Containerized application roles preserve a practical migration path to another scheduler if later justified.
- Customer database and blob boundaries are explicit and testable.
- The initial stack avoids premature distributed-system and specialist datastore complexity.

### Negative and trade-offs

- Application operations and infrastructure definitions become Azure-specific.
- A pooled PostgreSQL server has a shared failure and recovery domain even though customer databases and roles are separate.
- With PostgreSQL high availability disabled, a server or availability-zone failure can interrupt the entire pilot until Azure service recovery or the approved restore procedure completes. The pilot explicitly accepts planned downtime within its approved recovery objectives; production availability is not implied.
- A region-wide Azure outage has no pilot recovery-time objective. It invokes a manual best-effort business disaster plan; regional resilience requires a separately approved production architecture.
- Customer-only restore from a pooled server requires an isolated server restore followed by authorized database extraction.
- Service Bus Standard uses shared capacity, has less predictable throughput than Premium, and cannot place its broker endpoint behind Private Link or virtual-network integration.
- The Service Bus public endpoint adds a network boundary that must remain narrowly allowlisted and continuously verified.
- Prince 17 adds commercial license cost and a separately patched renderer image. PDF enablement remains gated on accessibility, sandbox and deterministic-output evidence.

## Security, operations, and cost impact

- Private ingress/endpoints for protected data services, egress restrictions, TLS, managed identities, least-privilege database roles, and customer-specific storage scopes must be expressed in Bicep and verified in deployment tests.
- The BFF reduces browser token exposure but makes session storage, CSRF defense, revocation, cookie lifetime, and recent-authentication policy server responsibilities.
- Key Vault stores credential material; ordinary environment variables hold non-secret settings or platform-provided secret references. Container/App configuration must not expose secret values through deployment output or diagnostics.
- Service Bus Standard uses its public Azure endpoint. Bicep must disable local/SAS authentication, require Entra workload identities and least-privilege data roles, require the approved TLS minimum, deny unapproved networks, and restrict access to the Container Apps environment's controlled static outbound address where Standard-tier IP rules support it. Public access is not a reason to permit anonymous, key-based, broad-network or developer-workstation access.
- Service Bus consumers must renew locks for bounded work, checkpoint durable progress in PostgreSQL, tolerate duplicate delivery, and dead-letter poison work without evidence payloads. The outbox/reconciliation process must recover a database commit followed by publish failure, and idempotent consumers must recover redelivery after a receive-side failure.
- Broker telemetry includes connection/authentication denial, queue depth/age, lock loss, retry, dead-letter, expiry and reconciliation lag but no message body or customer evidence.
- PostgreSQL high availability is disabled for the pilot. Automated backup/PITR retention must remain within the approved maximum of 35 days; recovery and deletion-tombstone replay require drills before pilot use.
- Cost estimates must include Container Apps minimum replicas, Service Bus Standard base/operations, controlled static egress, PostgreSQL compute/storage/backups, private networking for protected data services, logging volume, Blob versions, Key Vault operations and OpenAI use. The pilot estimate excludes a second region.

## Migration and reversibility

Introduce the services through Bicep modules and versioned environment parameters. Keep domain code behind repository interfaces for work dispatch, blob access, AI dispatch, and identity policy; do not leak Azure SDK types into core domain contracts. Build OCI images and standard OpenTelemetry signals.

Rollback deploys a previous compatible image and Bicep revision without rolling back immutable domain records. Work-message versions and database expand/migrate/contract rules must support mixed-version draining. A customer database can move from the pooled server to a dedicated PostgreSQL server through backup/export and validated cutover. A later scheduler or queue replacement remains possible behind the accepted work contract.

## Validation

- A platform spike deploys web, worker, Service Bus, PostgreSQL, Blob, Key Vault, managed identities, and observability in a non-production US environment from Bicep.
- Identity tests demonstrate correct issuer/audience validation, app-role plus product-policy enforcement, CSRF protection, session revocation, and no browser-visible Entra tokens.
- The deployed BFF proves federated-identity authorization-code redemption and session creation with multiple replicas, assertion renewal, wrong-trust denial, trust-removal response and no assertion/token leakage before Entra sign-in is enabled.
- Authorization tests demonstrate database-role, Blob-container, queue-worker, and managed-identity cross-customer denial.
- Reliability tests demonstrate duplicate delivery, lock loss, retry, dead-letter, checkpoint resume, and safe worker draining.
- Network tests demonstrate that Service Bus denies local/SAS credentials, unauthenticated access, wrong managed identities and traffic outside the approved egress boundary; configuration tests fail deployment if those controls drift.
- Outbox tests demonstrate commit-before-publish recovery, duplicate publication, reconciliation after broker outage, message expiry and dead-letter replay without duplicate domain results.
- Load tests prove Standard shared capacity meets the eight-hour assessment target with Pilot A and Pilot B active; failure promotes a review of Premium or another broker rather than silently weakening the target.
- Backup tests restore the pooled PostgreSQL server into isolation, recover one customer database, replay deletion/access tombstones, and meet the approved recovery targets.
- Availability tests stop or make the non-HA database unavailable, verify safe API/worker failure and same-region recovery without corrupting or duplicating work, and demonstrate that the approved RPO/RTO can be met for recoverable in-region incidents. A region-wide outage is covered by a business-disaster tabletop and does not claim the pilot RTO.
- Data-lifecycle tests demonstrate artifact versioning plus authorized purge and prove that immutability rules do not obstruct deletion policy.
- The AI path remains disabled until the exact OpenAI project supplies verified US regional processing and Zero Data Retention evidence.
- The Prince 17 spike passes the approved accessibility plan, PDF/UA validation, deterministic digest and sandbox tests before PDF is enabled.
- Load and cost tests demonstrate the approved interactive and eight-hour assessment targets at pilot scale.

## References

- `architecture/decisions/ADR-0001-pilot-application-shape.md`
- `architecture/decisions/ADR-0002-pilot-tenant-isolation.md`
- `architecture/decisions/ADR-0003-immutable-evidence-and-publication-storage.md`
- `docs/security/health-assessment-identity-session-design.md`
- `docs/security/health-assessment-ai-data-controls.md`
- [Microsoft: Azure Container Apps architecture best practices](https://learn.microsoft.com/en-us/azure/well-architected/service-guides/azure-container-apps)
- [Microsoft: Azure Container Apps jobs](https://learn.microsoft.com/en-us/azure/container-apps/jobs)
- [Microsoft: Managed identities in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity)
- [Microsoft: Azure Service Bus message loss and duplication](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-message-loss-and-duplicates)
- [Microsoft: Azure Service Bus network security](https://learn.microsoft.com/en-us/azure/service-bus-messaging/network-security)
- [Microsoft: Azure Database for PostgreSQL backup and restore](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore)
- [Microsoft: Immutable storage for Azure Blob Storage](https://learn.microsoft.com/en-us/azure/storage/blobs/immutable-storage-overview)
- [Microsoft: Azure Monitor OpenTelemetry](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable)
- [OpenAI: GPT-6 Sol](https://developers.openai.com/api/docs/models/gpt-6-sol)
- [OpenAI: API data controls and residency](https://developers.openai.com/api/docs/guides/your-data)

## Approval

Accepted by: Repository owner  
Date: 2026-09-29  
Amended by: Repository owner on 2026-09-29 to select a single-region pilot, move regional-outage recovery to the manual business disaster plan, approve the Windows Service/CLI collector profile, select Prince 17 as the gated PDF renderer, and select federated managed identity for the BFF confidential client.
