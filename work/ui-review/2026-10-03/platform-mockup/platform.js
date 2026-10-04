// Injected into the recovered prototype closure by build.py. No external dependencies.
const sections = {
  projects: "Projects",
  sources: "Sources & baselines",
  assessments: "Assessments",
  outcomes: "Outcomes & maturity",
  ai: "AI workspace",
  fixes: "Recommendations",
  tasks: "Tasks & reviews",
  reports: "Reports",
  compare: "Compare runs",
  rules: "Rule catalog",
  migration: "Migration",
  portfolio: "Portfolio",
  settings: "Settings",
  audit: "Audit history",
  gallery: "Design archive",
};
Object.assign(names, sections);
const mock = {
  tab: {},
  chat: [],
  settings: {},
  tasks: {},
  review: [],
  wizard: 0,
};
try {
  mock.settings = JSON.parse(
    localStorage.getItem("iga-platform-mock-settings") || "{}",
  );
} catch {}
const escapeHtml = (v) =>
  String(v).replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
const pill = (v, cls = "") => `<span class="pill ${cls}">${v}</span>`;
const btn = (label, action, quiet = true) =>
  `<button class="action ${quiet ? "quiet" : ""}" data-action="${action}">${label}</button>`;
const link = (label, area) =>
  `<button class="textbutton" data-go="${area}">${label}</button>`;
const head = (title, desc, action = "") =>
  `<div class="pagehead"><div class="row between"><div><h1>${title}</h1><p class="muted">${desc}</p></div>${action}</div></div>`;
const stats = (items) =>
  `<div class="stats">${items.map(([v, l]) => `<div><b>${v}</b><span>${l}</span></div>`).join("")}</div>`;
const table = (headers, rows) =>
  `<div class="table-wrap"><table><thead><tr>${headers.map((x) => `<th scope="col">${x}</th>`).join("")}</tr></thead><tbody>${rows.map((row) => `<tr>${row.map((x) => `<td>${x}</td>`).join("")}</tr>`).join("")}</tbody></table></div>`;
const item = (title, desc, action = "") =>
  `<div class="list-item"><div class="row between"><h3>${title}</h3>${action}</div><p class="small muted">${desc}</p></div>`;
const tabs = (area, labels) => {
  const active = mock.tab[area] || labels[0];
  return `<div class="tabs" aria-label="${names[area]} sections">${labels.map((t) => `<button data-area-tab="${area}" data-value="${t}" aria-pressed="${active === t}">${t}</button>`).join("")}</div>`;
};
const phase = (n, text) =>
  `<div class="preview-banner"><b>Phase ${n} · future capability preview</b><br>${text}</div>`;
const detailButton = (text, key) =>
  `<button class="textbutton" data-inspect="${key}">${text}</button>`;

function projectsPage() {
  return (
    head(
      "Projects",
      "Customer engagements, environments, and source boundaries.",
      btn("New project", "project", false),
    ) +
    stats([
      ["3", "Sample projects"],
      ["1", "Active assessment"],
      ["2", "Pending reviews"],
      ["US", "Sample residency"],
    ]) +
    `<section class="panel">${table(
      ["Project / customer", "Source", "Environment", "Status", ""],
      [
        [
          '<b>Production health assessment</b><br><span class="small muted">Sample organization · consultant-led</span>',
          "One Identity Manager 10.x",
          "Production",
          pill("Draft assessment"),
          link("Open workspace →", "home"),
        ],
        [
          '<b>Quarterly control review</b><br><span class="small muted">Sample organization</span>',
          "One Identity Manager 10.x",
          "Staging",
          pill("Baseline ready"),
          detailButton("View engagement", "quarterly"),
        ],
        [
          '<b>Migration readiness</b><br><span class="small muted">Example customer · later-phase preview</span>',
          "SailPoint ISC",
          "Non-production",
          pill("Planning"),
          link("View migration →", "migration"),
        ],
      ],
    )}</section><div class="split" style="margin-top:20px"><section class="panel"><h2>Engagement scope</h2>${item("Production · health assessment", "Read-only assessment of roles, workflows, synchronization, queues, and customizations.", link("Review sources", "sources"))}${item("Assigned delivery team", "Consultant, customer reviewer, evidence authorizer, and risk owner have distinct responsibilities.", link("View access", "settings"))}</section><section class="panel"><h2>Customer boundaries</h2><p class="muted">Each project has its own evidence baseline, review history, approvals, and data policy.</p><div class="notice">All organizations and project states shown here are fictional.</div></section></div>`
  );
}

function evidence() {
  const t = mock.tab.evidence || "Relationships";
  return (
    head(
      "Evidence",
      "Explore object relationships, provenance, and explicit coverage gaps.",
    ) +
    tabs("evidence", ["Relationships", "Evidence register", "Coverage gaps"]) +
    (t === "Relationships"
      ? relationshipWorkspace()
          .replace("<h1>", '<h2 style="font-size:21px">')
          .replace("</h1>", "</h2>")
      : t === "Evidence register"
        ? `<section class="panel"><div class="toolbar"><h2>Normalized evidence references</h2><input class="search" data-table-search placeholder="Search evidence references" aria-label="Search evidence"></div>${table(
            [
              "Reference",
              "Finding / object context",
              "Category",
              "Evidence state",
              "",
            ],
            findings.map((f) => [
              f.reference,
              f.title,
              f.category,
              pill("Redacted · available"),
              `<button class="textbutton" data-evidence-finding="${f.id}">Explore relationship</button>`,
            ]),
          )}</section>`
        : `<section class="panel"><h2>Explicit evidence gaps</h2>${table(
            ["Gap", "Affected evidence", "Reason", "Assessment effect"],
            [
              [
                "GAP-001",
                "5 customization references",
                "Unavailable source evidence",
                "Excluded from health scoring",
              ],
              [
                "GAP-002",
                "4 protected excerpts",
                "Customer-side exclusion / redaction",
                "Provenance only",
              ],
              [
                "GAP-003",
                "4 operational records",
                "Source availability window",
                "Unsupported time range marked",
              ],
            ],
          )}<div class="notice">A missing record is a coverage limitation, not a confirmed unhealthy control.</div></section>`)
  );
}

function sourcesPage() {
  const t = mock.tab.sources || "Connection";
  return (
    head(
      "Sources & baselines",
      "Trace collected configuration to an immutable evidence baseline.",
      btn("Collect sample baseline", "collect", false),
    ) +
    tabs("sources", [
      "Connection",
      "Object inventory",
      "Baselines",
      "Collection history",
    ]) +
    (t === "Connection"
      ? `<div class="split"><section class="panel"><div class="row between"><h2>One Identity Manager</h2>${pill("Read-only")}</div><div class="diagram"><div class="diagram-block"><b>Customer environment</b>SQL Server · One Identity 10.x</div><span>→</span><div class="diagram-block"><b>Evidence baseline</b>Normalized · redacted</div></div>${table(
          ["Connection property", "Sample configuration"],
          [
            ["Method", "Dedicated read-only database account"],
            ["Permission check", pill("Passed · sample")],
            ["Operational lookback", "90 days · source availability applies"],
            ["Current baseline", "BASE-2026-10-02 · immutable"],
            ["Sensitive content", "Customer-side exclusion and redaction"],
          ],
        )}<div class="row" style="margin-top:20px">${btn("Review connection", "connection")}${btn("View collection scope", "collection-scope")}</div></section><section class="panel"><h2>Collection coverage</h2>${coverageBars()}<div class="empty-note">11% of objects have an explicit evidence gap. Gaps remain visible and are excluded from health scoring.</div>${link("Open assessment quality →", "reports")}</section></div>`
      : t === "Object inventory"
        ? `<section class="panel"><div class="toolbar"><h2>Collected objects</h2><input class="search" data-table-search aria-label="Search object inventory" placeholder="Search objects or modules"></div>${table(
            ["Object type", "Collected", "Assessed", "Evidence state", ""],
            [
              [
                "Roles",
                "148",
                "148",
                pill("Available"),
                detailButton("Inspect", "role"),
              ],
              [
                "Workflows",
                "42",
                "40",
                pill("2 gaps", "high"),
                detailButton("Inspect", "workflow"),
              ],
              [
                "Synchronization projects",
                "18",
                "16",
                pill("2 gaps", "high"),
                detailButton("Inspect", "synchronization"),
              ],
              [
                "Processes & queues",
                "76",
                "72",
                pill("4 gaps", "high"),
                detailButton("Inspect", "queues"),
              ],
              [
                "Scripts & customizations",
                "28",
                "23",
                pill("5 gaps", "high"),
                detailButton("Inspect", "scripts"),
              ],
            ],
          )}</section>`
        : t === "Baselines"
          ? `<section class="panel"><h2>Evidence baselines</h2>${table(
              ["Baseline", "Captured", "State", "Objects", ""],
              [
                [
                  "BASE-2026-10-02",
                  "02 Oct 2026 · 10:24",
                  pill("Current · locked"),
                  "312",
                  link("Explore evidence", "evidence"),
                ],
                [
                  "BASE-2026-09-25",
                  "25 Sep 2026 · 09:40",
                  pill("Approved comparison"),
                  "304",
                  link("Compare →", "compare"),
                ],
                [
                  "BASE-2026-09-18",
                  "18 Sep 2026 · 09:18",
                  pill("Historical"),
                  "301",
                  detailButton("View provenance", "baseline"),
                ],
              ],
            )}<div class="notice">Evidence expiration can leave historical assessment records with unavailable source evidence.</div></section>`
          : `<section class="panel"><h2>Collection history</h2>${table(
              ["Collection", "Status", "Duration", "Result"],
              [
                [
                  "02 Oct · snapshot",
                  pill("Completed with gaps"),
                  "12m 42s",
                  "312 objects · 13 evidence gaps",
                ],
                [
                  "25 Sep · snapshot",
                  pill("Completed"),
                  "11m 18s",
                  "304 objects · baseline locked",
                ],
                [
                  "18 Sep · targeted refresh",
                  pill("Completed"),
                  "3m 06s",
                  "Role and workflow metadata refreshed",
                ],
              ],
            )}</section>`)
  );
}
function coverageBars() {
  return [
    ["Roles", 100],
    ["Workflows", 95],
    ["Synchronization", 89],
    ["Queues", 95],
    ["Customizations", 82],
  ]
    .map(
      ([n, v]) =>
        `<div class="barrow"><span>${n}</span><div class="track"><span style="width:${v}%"></span></div><b>${v}%</b></div>`,
    )
    .join("");
}

function assessmentsPage() {
  return (
    head(
      "Assessments",
      "Review runs, locked inputs, and completion states.",
      btn("New assessment", "assessment", false),
    ) +
    stats([
      ["72", "Current provisional score"],
      ["+3", "Change from prior run"],
      ["33", "Open findings"],
      ["6", "AI proposals to review"],
    ]) +
    `<section class="panel"><div class="toolbar"><h2>Assessment history</h2><input class="search" data-table-search placeholder="Search assessments" aria-label="Search assessments"></div>${table(
      [
        "Assessment",
        "Baseline / profile",
        "Health",
        "Completion",
        "Review",
        "",
      ],
      [
        [
          '<b>02 Oct 2026</b><br><span class="small muted">Production · current draft</span>',
          "BASE-10-02<br>Standard · catalog 1.4",
          "<b>72 / 100</b>",
          pill("Completed with gaps"),
          pill("6 proposed", "high"),
          link("Open →", "home"),
        ],
        [
          "25 Sep 2026",
          "BASE-09-25<br>Standard · catalog 1.4",
          "69 / 100",
          pill("Completed"),
          "Published",
          link("Compare →", "compare"),
        ],
        [
          "18 Sep 2026",
          "BASE-09-18<br>Standard · catalog 1.3",
          "64 / 100",
          pill("Completed"),
          "Published",
          detailButton("Run inputs", "run-inputs"),
        ],
      ],
    )}</section><div class="split" style="margin-top:20px"><section class="panel"><h2>Current run inputs</h2>${item("Immutable evidence baseline", "BASE-2026-10-02 · 312 objects", link("Open baseline", "sources"))}${item("Scoring and rule versions", "Standard profile · five equal category weights · catalog 1.4", link("Inspect catalog", "rules"))}${item("Customer outcomes", "8 desired outcomes · 3 need attention", link("Review outcomes", "outcomes"))}</section><section class="panel"><h2>Run progression</h2>${item("1 · Collect evidence", "Completed · baseline locked")}${item("2 · Assess and explain", "Completed with 13 explicit evidence gaps")}${item("3 · Consultant review", "6 AI proposals require review", link("Open reviews", "tasks"))}${item("4 · Publish report", "Draft · publication warnings remain", link("View report", "reports"))}</section></div>`
  );
}

function outcomesPage() {
  return (
    head(
      "Outcomes & maturity",
      "Understand whether configuration supports customer objectives.",
      btn("Add desired outcome", "outcome", false),
    ) +
    stats([
      ["8", "Desired outcomes"],
      ["5", "Meeting intent"],
      ["3", "Need attention"],
      ["3 / 5", "Illustrative maturity"],
    ]) +
    `<div class="split"><section class="panel"><h2>Outcome adherence</h2>${table(
      ["Desired outcome", "Adherence", "Evidence", ""],
      [
        [
          "Separate payment responsibilities",
          pill("Needs attention", "critical"),
          "3 linked role objects",
          detailButton("Review", "outcome-separation"),
        ],
        [
          "Provision accounts on time",
          pill("Partial", "high"),
          "Queue and workflow evidence",
          detailButton("Review", "outcome-provisioning"),
        ],
        [
          "Review privileged access",
          pill("Needs review", "high"),
          "1 AI proposal",
          detailButton("Review", "outcome-review"),
        ],
        [
          "Maintain traceable changes",
          pill("Meeting intent"),
          "Audit metadata",
          detailButton("Review", "outcome-audit"),
        ],
        [
          "Use supported configuration",
          pill("Meeting intent"),
          "Customization comparison",
          detailButton("Review", "outcome-supported"),
        ],
      ],
    )}</section><section class="panel"><h2>Maturity by domain</h2>${radar}<div class="caption">Illustrative maturity rubric · repeatable controls with remaining gaps in security and operations.</div>${link("Review scoring profile →", "settings")}</section></div><section class="panel" style="margin-top:20px"><h2>Customer intent and inferred outcomes</h2>${item("Customer-defined · separation of responsibilities", "Customer-approved intent takes precedence over AI inference.", link("Explore linked findings", "risk"))}${item("AI-inferred · timely account provisioning", "Candidate outcome · consultant and customer review required.", btn("Review inference", "inference"))}</section>`
  );
}

function aiPage() {
  const t = mock.tab.ai || "Analysis";
  return (
    head(
      "AI workspace",
      "Review evidence-grounded explanations, proposals, and analysis scope.",
      pill("Sample responses"),
    ) +
    tabs("ai", [
      "Analysis",
      "Proposed findings",
      "Analysis history",
      "Deep analysis",
    ]) +
    (t === "Analysis"
      ? `<div class="split"><section class="panel chat"><div class="row between"><h2>Assessment assistant</h2>${pill("Redacted evidence")}</div><div class="chat-message user">What is driving our security score?</div><div class="chat-message"><b>AI explanation · illustrative</b><p><strong>Observed:</strong> the sample role graph links Finance operations to AP approver and Payment approval. <button class="citation" data-action="ai-evidence">SAMPLE-EV-001 ↗</button></p><p><strong>Inference:</strong> inherited grants may combine responsibilities the customer intends to separate. Review the actual assignment context before confirming the finding.</p><p><strong>Suggestion:</strong> review the role boundary and consider the supported separation controls. <button class="citation" data-go="fixes">FIX-001 ↗</button></p></div>${mock.chat.map((m) => `<div class="chat-message user">${escapeHtml(m)}</div><div class="chat-message"><b>Sample response · predefined</b><p>${sampleAnswer(m)}</p><p class="small muted">This prototype uses scripted responses; no AI request was sent.</p></div>`).join("")}<div><div class="suggestions"><button data-prompt="Explain the queue delays">Explain queue delays</button><button data-prompt="What should we review first?">What should we review first?</button><button data-prompt="Show evidence gaps">Show evidence gaps</button></div><form id="ai-form" class="composer"><textarea id="ai-question" aria-label="Ask about this assessment" placeholder="Ask about the sample assessment…" required maxlength="1000"></textarea><button class="action" type="submit">Send sample query ↑</button></form></div></section><div class="stack"><section class="panel"><h2>Analysis context</h2>${item("Current assessment", "02 Oct 2026 · BASE-2026-10-02")}${item("Evidence boundary", "Normalized, redacted configuration only. Protected raw evidence is outside pilot AI scope.")}${item("Proposal state", "AI output remains proposed until qualified review.", link("Review 6 proposals", "tasks"))}</section><section class="panel"><h2>Sample usage budget</h2><div class="row between"><b>42% used</b><span class="small muted">Illustrative run budget</span></div><div class="track" style="margin:14px 0"><span style="width:42%"></span></div><p class="small muted">A budget gap is recorded if analysis stops before covering all eligible objects.</p>${link("Configure AI →", "settings")}</section></div></div>`
      : t === "Proposed findings"
        ? `<section class="panel"><h2>AI proposals awaiting review</h2>${findings
            .filter((f) => f.method === "AI-assisted")
            .map(
              (f) =>
                `<div class="list-item"><div class="row between"><div>${badge(f)} <b>${f.title}</b><p class="small muted">${f.category} · inferred · sample confidence ${f.confidence}</p></div><button class="action quiet" data-ai-finding="${f.id}">Review evidence</button></div></div>`,
            )
            .join("")}</section>`
        : t === "Analysis history"
          ? `<section class="panel">${table(
              ["Analysis", "Scope", "Result", "Review state"],
              [
                [
                  "02 Oct · automatic",
                  "Eligible redacted baseline",
                  "6 proposed findings",
                  "Awaiting consultant review",
                ],
                [
                  "25 Sep · automatic",
                  "Prior evidence baseline",
                  "5 proposed findings",
                  "4 confirmed · 1 rejected",
                ],
                [
                  "18 Sep · automatic",
                  "Prior evidence baseline",
                  "7 proposed findings",
                  "5 confirmed · 2 rejected",
                ],
              ],
            )}<div class="notice">Model, prompt, rule, and evidence versions remain attached to each analysis record.</div></section>`
          : phase(
              2,
              "Scoped consultant-initiated deep analysis is shown for design review.",
            ) +
            `<div class="split"><section class="panel"><h2>Prepare analysis scope</h2>${field("deep-scope", "Scope", "Role inheritance and privileged access", "text")}${field("deep-class", "Data classes", "Normalized role configuration", "select", ["Normalized role configuration", "Workflow metadata", "Operational excerpts"])}${field("deep-outcome", "Desired outcome", "Separate payment responsibilities", "text")}<div class="row" style="margin-top:20px">${btn("Preview scope and usage", "deep", false)}</div></section><section class="panel"><h2>Before analysis</h2>${item("Scope and usage", "A review shows evidence classes, estimated usage, and expected duration.")}${item("Protected evidence", "Raw retrieval needs separate customer authorization when necessary.")}${item("Review remains required", "Deep analysis produces proposals, not approved changes.")}</section></div>`)
  );
}
function sampleAnswer(q) {
  if (/queue|delay/i.test(q))
    return 'The sample operational evidence shows repeated retries in the Account sync workflow. This suggests a shared dependency may be concentrating queue delays. Correlation is an inference; review queue timing and workflow evidence before confirming a root cause. <button class="citation" data-go="evidence">Open evidence ↗</button>';
  if (/gap|coverage/i.test(q))
    return 'The sample baseline has 13 explicit evidence gaps. Workflow and customization coverage is incomplete. Missing evidence is shown separately from health findings and excluded from the health score. <button class="citation" data-action="quality">View assessment quality ↗</button>';
  return 'Start with the two Critical findings, then the seven High findings. Review all Critical and High AI proposals before treating them as confirmed. The role-inheritance finding has linked evidence and a proposed fix package. <button class="citation" data-ai-finding="risk-1">Review priority finding ↗</button>';
}

function fixesPage() {
  return (
    head(
      "Recommendations",
      "Review options, dependencies, and validation before planning work.",
      btn("Group sample package", "package", false),
    ) +
    stats([
      ["12", "Recommendations"],
      ["4", "Fix packages"],
      ["3", "Quick wins"],
      ["0", "Executed changes"],
    ]) +
    `<div class="split"><section class="panel"><h2>Prioritized fix packages</h2>${item("FIX-001 · Separate conflicting role inheritance", "Critical · 3 affected objects · Medium effort · consultant review needed", btn("Review package", "fix-role"))}${item("FIX-002 · Review privileged access ownership", "High · 6 affected objects · Small effort", btn("Review package", "fix-owner"))}${item("FIX-003 · Reduce recurring workflow retries", "High · 2 workflows · Medium effort", btn("Review package", "fix-queue"))}${item("FIX-004 · Consolidate repeated custom logic", "Medium · 3 scripts · Large effort", btn("Review package", "fix-script"))}</section><section class="panel"><h2>Recommended sequence</h2><div class="steps"><div class="step"><b>1 · Confirm</b>Review evidence</div><div class="step"><b>2 · Plan</b>Assign prerequisites</div><div class="step"><b>3 · Validate</b>Reassess evidence</div></div><div class="empty-note">Packages contain guidance and review-only examples. A finding is remediated only after validated evidence shows the correction.</div>${item("Quick win · assign missing role owners", "Proposed action · customer responsibilities must be confirmed.", btn("Create sample task", "task"))}${item("Compensating control · targeted review", "Review responsibilities while the role redesign is planned.", btn("View option", "compensating"))}</section></div>`
  );
}

const taskData = [
  {
    id: "T-014",
    title: "Confirm conflicting role inheritance",
    state: "To review",
    owner: "Consultant",
    severity: "Critical",
    desc: "Review ROLE-014 → ROLE-031 and customer intent.",
  },
  {
    id: "T-015",
    title: "Review AI privilege proposal",
    state: "To review",
    owner: "Customer reviewer",
    severity: "High",
    desc: "Confirm or refine the evidence-backed proposal.",
  },
  {
    id: "T-009",
    title: "Plan role boundary correction",
    state: "In progress",
    owner: "Consultant",
    severity: "Critical",
    desc: "FIX-001 · prerequisites and validation plan.",
  },
  {
    id: "T-006",
    title: "Confirm synchronization owner",
    state: "Complete",
    owner: "Customer reviewer",
    severity: "Medium",
    desc: "Ownership reference reviewed against sample evidence.",
  },
];
function tasksPage() {
  const t = mock.tab.tasks || "Board";
  return (
    head(
      "Tasks & reviews",
      "Move from evidence to a documented review and work plan.",
      btn("New sample task", "task", false),
    ) +
    tabs("tasks", ["Board", "Review queue", "Accepted risks"]) +
    (t === "Board"
      ? `<div class="board">${["To review", "In progress", "Complete"]
          .map(
            (s) =>
              `<section class="board-column"><div class="row between"><h2>${s}</h2><span class="small muted">${taskData.filter((x) => (mock.tasks[x.id] || x.state) === s).length}</span></div>${taskData
                .filter((x) => (mock.tasks[x.id] || x.state) === s)
                .map(
                  (x) =>
                    `<button class="task-item" data-task="${x.id}">${pill(x.severity, x.severity === "Critical" ? "critical" : x.severity === "High" ? "high" : "")}<b>${escapeHtml(x.title)}</b><span class="small muted">${x.id} · ${x.owner}</span><p class="small muted" style="margin-top:8px">${x.desc}</p></button>`,
                )
                .join("")}</section>`,
          )
          .join(
            "",
          )}</div><div class="row" style="margin-top:20px">${btn("Export sample tasks CSV", "csv")}<span class="small muted">External task integrations are deferred beyond the pilot.</span></div>`
      : t === "Review queue"
        ? `<section class="panel"><h2>Required finding reviews</h2>${findings
            .filter((f) => f.status === "Proposed")
            .map(
              (f) =>
                `<div class="list-item"><div class="row between"><div>${badge(f)} <b>${f.title}</b><p class="small muted">AI-assisted · ${f.category} · proposed</p></div><button class="action quiet" data-ai-finding="${f.id}">Open review</button></div></div>`,
            )
            .join("")}</section>`
        : `<section class="panel"><h2>Residual-risk decisions</h2>${table(
            ["Finding", "Decision owner", "State", "Review date", ""],
            [
              [
                "Dormant account retains access",
                "Customer risk owner",
                pill("Accepted risk"),
                "15 Dec 2026",
                detailButton("View rationale", "accepted-risk"),
              ],
              [
                "Growing processing backlog",
                "Customer risk owner",
                pill("Accepted risk"),
                "30 Nov 2026",
                detailButton("View controls", "accepted-risk"),
              ],
            ],
          )}<div class="empty-note">Risk acceptance retains the score penalty and recommendations. Consultants cannot accept customer residual risk.</div></section>`)
  );
}

function reportsPage() {
  const t = mock.tab.reports || "Executive";
  return (
    head(
      "Reports",
      "Review a consistent assessment version across audiences and outputs.",
      btn("Preview publication", "publish", false),
    ) +
    tabs("reports", [
      "Executive",
      "Practitioner",
      "Auditor",
      "Assessment quality",
      "Publication history",
    ]) +
    (t === "Assessment quality"
      ? `<section class="panel"><h2>Assessment quality and limitations</h2>${stats(
          [
            ["89%", "Evidence coverage"],
            ["13", "Evidence gaps"],
            ["6", "Unreviewed AI proposals"],
            ["0", "Sample rule failures"],
          ],
        )}${coverageBars()}${table(
          ["Limitation", "Count", "Effect"],
          [
            [
              "Unavailable configuration references",
              "5",
              "Explicit evidence gaps · excluded from scoring",
            ],
            [
              "Redacted customer-side evidence",
              "4",
              "Provenance available · raw content not shown",
            ],
            [
              "Operational source lookback",
              "4",
              "Coverage bounded by source availability",
            ],
            [
              "AI proposal review",
              "6",
              "Provisional score until qualified review",
            ],
          ],
        )}<div class="notice">Quality and coverage describe confidence in the assessment; they are separate from environment health.</div></section>`
      : t === "Publication history"
        ? `<section class="panel">${table(
            ["Version", "Status", "Published by", "Acknowledgment", ""],
            [
              [
                "02 Oct · draft",
                pill("Draft"),
                "—",
                "Not requested",
                btn("Review warnings", "publish"),
              ],
              [
                "25 Sep · v1",
                pill("Published · immutable"),
                "Sample consultant",
                "Acknowledged · customer risk owner",
                detailButton("View delivery record", "delivery"),
              ],
            ],
          )}${item("Report sharing", "Pilot links are redacted, revocable, passcode-protected, and expire within 24 hours.", btn("Preview share settings", "share"))}</section>`
        : `<div class="toolbar"><div class="row">${pill("Draft")}${pill("ASSESS-10-02")}<span class="small muted">BASE-10-02 · Standard · catalog 1.4</span></div><div class="row">${btn("Download sample Markdown", "markdown")}${btn("Print / PDF", "print")}</div></div><article class="report-paper"><div class="eyebrow">Sample organization / One Identity Manager</div><h2 style="font-size:25px">${t === "Executive" ? "Environment health assessment" : t === "Practitioner" ? "Practitioner findings & remediation" : "Assessment traceability & decisions"}</h2><p class="muted">02 October 2026 · Draft · illustrative report</p>${
            t === "Executive"
              ? `<div class="report-kpi"><b>72</b><span>/ 100 · provisional health</span></div><h2>Priority risks</h2><p>Two Critical and seven High findings require attention. The strongest immediate opportunity is to review conflicting role inheritance and privileged-access ownership.</p><h2>Health by category</h2>${radar}<h2>Recommended next steps</h2><p>Confirm the highest-priority findings, agree on role boundaries, and plan the linked fix packages. Reassess after customer-side changes are validated.</p>`
              : t === "Practitioner"
                ? `<h2>Finding register</h2>${findings
                    .slice(0, 5)
                    .map((f) =>
                      item(
                        f.title,
                        `${f.severity} · ${f.status} · ${f.method}`,
                        `<button class="textbutton" data-ai-finding="${f.id}">Evidence →</button>`,
                      ),
                    )
                    .join(
                      "",
                    )}<h2>Fix packages and validation</h2><p>FIX-001 proposes supported role separation, dependency review, and before/after evidence comparison. Examples remain review-only until a consultant verifies them.</p>${link("Open all recommendations", "fixes")}`
                : `<h2>Locked methodology</h2>${table(
                    ["Input", "Version"],
                    [
                      ["Evidence baseline", "BASE-2026-10-02"],
                      [
                        "Rule catalog",
                        "1.4 · deterministic and AI methods identified",
                      ],
                      [
                        "Scoring profile",
                        "Standard · five equal category weights",
                      ],
                      ["Desired outcomes", "Customer intent v2"],
                      [
                        "Review record",
                        "Confirmed, proposed, and accepted-risk states retained",
                      ],
                    ],
                  )}<h2>Decisions and provenance</h2><p>Six AI proposals await qualified review. Two accepted-risk decisions retain their owners, rationale, controls, and review dates.</p>${link("View audit history →", "audit")}`
          }<h2>Limitations</h2><p>13 explicit evidence gaps remain. AI proposals are unconfirmed. This draft uses fictional data for interface review.</p>${link("Open assessment-quality report", "reports")}</article>`)
  );
}

function comparePage() {
  return (
    head(
      "Compare runs",
      "Separate source changes from changes in rules, profiles, and review decisions.",
      btn("Select comparison", "comparison"),
    ) +
    stats([
      ["69 → 72", "Provisional health"],
      ["4", "Resolved sample findings"],
      ["2", "New sample findings"],
      ["1", "Worsened sample finding"],
    ]) +
    `<div class="split"><section class="panel"><h2>Category health</h2><div class="legend"><span><i class="dot" style="background:#aab8d9"></i>25 Sep 2026</span><span><i class="dot"></i>02 Oct 2026</span></div><div class="compare-chart">${[
      ["Security", 54, 58],
      ["Correctness", 75, 78],
      ["Performance", 65, 67],
      ["Maintainability", 81, 81],
      ["Operations", 70, 76],
    ]
      .map(
        ([n, a, b]) =>
          `<div class="compare-row"><b>${n}</b><div class="bars"><div class="track"><span style="width:${a}%"></span></div><div class="track"><span style="width:${b}%"></span></div></div><span>+${b - a}</span></div>`,
      )
      .join(
        "",
      )}</div>${trend}</section><section class="panel"><h2>What changed</h2>${item("Source configuration", "Role owner references changed for 4 objects.", detailButton("Inspect delta", "source-delta"))}${item("Evidence availability", "8 additional objects captured in the current baseline.", link("View baselines", "sources"))}${item("Review disposition", "4 findings validated against new evidence.", link("View reviews", "tasks"))}${item("Rules and profile", "Catalog 1.4 and Standard profile remained consistent.")}</section></div><section class="panel" style="margin-top:20px"><h2>Finding changes</h2>${table(
      ["Finding", "Change", "Current state", ""],
      [
        [
          "Inherited administrative entitlement",
          pill("New", "high"),
          "Confirmed",
          link("Open findings", "risk"),
        ],
        [
          "Slow provisioning workflow",
          pill("Worsened", "high"),
          "Confirmed",
          detailButton("Inspect", "queue-change"),
        ],
        [
          "Missing role ownership",
          pill("Validated"),
          "Closed · new evidence validated",
          detailButton("Timeline", "validation"),
        ],
      ],
    )}</section>`
  );
}

function rulesPage() {
  const t = mock.tab.rules || "Catalog";
  return (
    head(
      "Rule catalog",
      "Trace assessment methods to versioned rules and authoritative references.",
    ) +
    tabs("rules", ["Catalog", "Version history", "Custom rules"]) +
    (t === "Custom rules"
      ? phase(3, "Custom-rule authoring is a later-phase design preview.") +
        `<section class="panel"><h2>Draft customer rule</h2>${field("rule-name", "Rule name", "Role ownership must be documented", "text")}${field("rule-category", "Category", "Security", "select", categoryNames)}${field("rule-condition", "Condition preview", "Owner reference is absent on a privileged role", "text")}<div class="row" style="margin-top:20px">${btn("Preview rule validation", "custom-rule", false)}</div><div class="notice">Authoring is a mockup only. No rule is added to the pilot catalog.</div></section>`
      : t === "Version history"
        ? `<section class="panel">${table(
            ["Catalog", "Released", "Change", "Assessments"],
            [
              [
                "1.4",
                "25 Sep 2026",
                "Role inheritance checks and operational evidence refinements",
                "Current and prior run",
              ],
              [
                "1.3",
                "18 Sep 2026",
                "Synchronization coverage update",
                "Historical baseline",
              ],
            ],
          )}<div class="empty-note">New catalogs do not automatically reassess historical evidence. Replay locks the chosen rule, evidence, and profile versions.</div></section>`
        : `<section class="panel"><div class="toolbar"><h2>Catalog 1.4</h2><input class="search" data-table-search placeholder="Search rules" aria-label="Search rules"></div>${table(
            ["Rule", "Category", "Method", "Status", ""],
            [
              [
                "SEC-014 · conflicting role inheritance",
                "Security",
                "Deterministic",
                pill("Active"),
                detailButton("View rule", "rule-security"),
              ],
              [
                "SEC-AI-008 · privileged owner review",
                "Security",
                "AI-assisted",
                pill("Active"),
                detailButton("View rule", "rule-ai"),
              ],
              [
                "OPS-006 · retry loop concentration",
                "Operations",
                "Deterministic",
                pill("Active"),
                detailButton("View rule", "rule-ops"),
              ],
              [
                "COR-011 · provisioning conflict",
                "Correctness",
                "Deterministic",
                pill("Active"),
                detailButton("View rule", "rule-correctness"),
              ],
              [
                "MNT-AI-003 · repeated customization",
                "Maintainability",
                "AI-assisted",
                pill("Active"),
                detailButton("View rule", "rule-ai"),
              ],
            ],
          )}</section>`)
  );
}

function migrationPage() {
  const t = mock.tab.migration || "Readiness";
  return (
    head(
      "Migration workspace",
      "Review source-to-destination intent, dependencies, and validation.",
    ) +
    phase(
      4,
      "Migration execution is outside the health pilot. These screens illustrate planning and governed execution flows.",
    ) +
    tabs("migration", [
      "Readiness",
      "Mappings",
      "Scope decisions",
      "Validation",
      "Handoff",
    ]) +
    (t === "Readiness"
      ? `<div class="split"><section class="panel"><h2>Sample migration engagement</h2><div class="diagram"><div class="diagram-block"><b>SailPoint ISC</b>Read-only source</div><span>→</span><div class="diagram-block"><b>One Identity Manager</b>Non-production destination preview</div></div>${stats(
          [
            ["4", "Use-case groups"],
            ["7", "Dependencies"],
            ["3", "Scope decisions"],
            ["0", "Executed changes"],
          ],
        )}${table(
          ["Preparation", "State", ""],
          [
            [
              "Destination discovery",
              pill("Reviewed"),
              detailButton("View capability inventory", "destination"),
            ],
            [
              "Connector prerequisites",
              pill("Needs consultant input", "high"),
              btn("Review prerequisites", "prerequisites"),
            ],
            [
              "Identity matching",
              pill("2 ambiguities", "high"),
              btn("Review matching", "matching"),
            ],
            [
              "Customer scope approval",
              pill("Pending"),
              btn("Preview approval", "scope-approval"),
            ],
          ],
        )}</section><section class="panel"><h2>Sequence</h2>${item("1 · Roles and access profiles", "Review native mappings and effective-access equivalence.")}${item("2 · Certifications", "Future campaigns only; active rounds remain in the source.")}${item("3 · Joiner / Mover / Leaver", "Preserve conditions, dependencies, and unsupported-script decisions.")}${item("4 · Validation and handoff", "Compare governance behavior, record exceptions, and obtain required acceptance.")}</section></div>`
      : t === "Mappings"
        ? `<section class="panel">${table(
            ["Source object", "Proposed destination", "Coverage", ""],
            [
              [
                "Finance role",
                "Business role + assignments",
                pill("Review needed"),
                btn("Inspect mapping", "mapping-role"),
              ],
              [
                "Payment access profile",
                "Requestable service item",
                pill("Conditional", "high"),
                btn("Inspect mapping", "mapping-access"),
              ],
              [
                "Quarterly certification",
                "Attestation policy",
                pill("Future campaigns"),
                btn("Inspect mapping", "mapping-cert"),
              ],
              [
                "Joiner workflow",
                "Process orchestration",
                pill("Prerequisite blocked", "high"),
                btn("Inspect mapping", "mapping-jml"),
              ],
            ],
          )}</section>`
        : t === "Scope decisions"
          ? `<section class="panel"><h2>Object disposition</h2>${table(
              ["Object", "Proposed disposition", "Rationale", ""],
              [
                [
                  "Finance role",
                  "Migrate",
                  "Preserve approved access intent",
                  btn("Review decision", "scope-role"),
                ],
                [
                  "Legacy custom script",
                  "Redesign",
                  "Prefer supported native behavior",
                  btn("Review decision", "scope-script"),
                ],
                [
                  "Active certification round",
                  "Keep in source",
                  "Complete active round before later campaign mapping",
                  btn("Review decision", "scope-cert"),
                ],
              ],
            )}<div class="notice">Suggestions, consultant refinements, and customer approvals remain distinct.</div></section>`
          : t === "Validation"
            ? `<section class="panel"><h2>Non-production validation preview</h2>${table(
                ["Validation dimension", "Sample grade", "Decision"],
                [
                  [
                    "Entitlement equivalence",
                    "92%",
                    "Review remaining differences",
                  ],
                  [
                    "Identity and reviewer matching",
                    "Ambiguous",
                    "Blocked until resolved",
                  ],
                  ["Workflow behavior", "Not run", "Prerequisites outstanding"],
                  [
                    "Recovery plan",
                    "Draft",
                    "Operation-specific review required",
                  ],
                ],
              )}<div class="row" style="margin-top:20px">${btn("Inspect validation criteria", "validation-criteria")}${btn("Preview acceptance record", "acceptance")}</div><div class="empty-note" style="margin-top:20px">Scope and reviewer mismatches can block acceptance. Production execution requires explicit customer approval and tested recovery.</div></section>`
            : `<section class="panel"><h2>Reviewed handoff package</h2>${item("Mapping and decisions", "Source baseline, destination capabilities, approved scope, and rationale.")}${item("Implementation recipes", "Prerequisites, reviewed configuration examples, validation, and recovery guidance.")}${item("Acceptance and warnings", "Non-production acceptance, unresolved differences, and production boundary.")}${btn("Preview package contents", "handoff", false)}</section>`)
  );
}

function portfolioPage() {
  return (
    head("Health portfolio", "Read-only summaries of customers who opted in.") +
    phase(
      2,
      "Vendor portfolio is a post-pilot summary view. Customer evidence and assessment drilldowns are excluded.",
    ) +
    stats([
      ["3", "Opted-in sample customers"],
      ["74", "Sample mean health"],
      ["1", "Assessment in progress"],
      ["5", "Critical sample findings"],
    ]) +
    `<section class="panel">${table(
      [
        "Customer",
        "Latest completed assessment",
        "Health",
        "Modules",
        "Findings",
      ],
      [
        [
          "Sample organization",
          "02 Oct 2026 · completed with gaps",
          "72 / 100",
          "Roles · synchronization · operations",
          "Conflicting role inheritance · Critical · Confirmed<br>Privileged owner review · Critical · Proposed",
        ],
        [
          "Example services",
          "01 Oct 2026 · completed",
          "81 / 100",
          "Roles · workflows",
          "Missing role owner · High · Confirmed",
        ],
        [
          "Demo manufacturing",
          '30 Sep 2026 · completed<br><span class="small muted">Newer run in progress · 03 Oct</span>',
          "69 / 100",
          "Roles · synchronization",
          "Retry concentration · Critical · Confirmed",
        ],
      ],
    )}<div class="notice">Summary snapshot only · no customer-detail links or cross-customer exports.</div></section>`
  );
}

function field(id, label, value, type = "text", options = [], help = "") {
  const v = mock.settings[id] ?? value;
  return `<div class="form-field"><label for="${id}">${label}${help ? `<small>${help}</small>` : ""}</label>${type === "select" ? `<select id="${id}" data-setting="${id}">${options.map((x) => `<option ${v === x ? "selected" : ""}>${x}</option>`).join("")}</select>` : type === "checkbox" ? `<input id="${id}" data-setting="${id}" type="checkbox" ${v ? "checked" : ""}>` : `<input id="${id}" data-setting="${id}" type="${type}" value="${escapeHtml(v)}" ${type === "number" ? 'min="0"' : ""}>`}</div>`;
}
function settingsPage() {
  const labels = [
    "General",
    "Assessment",
    "AI",
    "Access",
    "Data policy",
    "Notifications",
    "Integrations",
    "Appearance",
  ];
  const t = mock.tab.settings || "General";
  let body = "";
  if (t === "General")
    body = `<h2>Project settings</h2>${field("project-name", "Project name", "Production health assessment")}${field("customer-name", "Customer", "Sample organization")}${field("environment", "Environment", "Production", "select", ["Production", "Staging", "Development"])}${field("source", "Source product", "One Identity Manager 10.x", "select", ["One Identity Manager 10.x"])}${field("owner", "Engagement owner", "Sample consultant")}`;
  if (t === "Assessment")
    body = `<h2>Assessment profile</h2>${field("profile", "Profile", "Standard assessment", "select", ["Standard assessment", "Security focused · sample"])}<h3 style="margin:20px 0 8px">Category weights</h3>${categoryNames.map((c) => field("weight-" + c, c + " weight (%)", 20, "number")).join("")}<p class="small muted">Standard uses equal category weights. Evidence gaps are excluded from health scoring.</p>${field("lookback", "Operational lookback (days)", 90, "number")}${field("catalog", "Rule catalog", "1.4", "select", ["1.4", "1.3"])}${field("outcome-use", "Include desired outcomes", true, "checkbox")}`;
  if (t === "AI")
    body = `<h2>AI analysis controls</h2>${field("ai-enabled", "Automatic general AI", true, "checkbox", [], "Eligible redacted evidence only")}${field("ai-scope", "Category scope", "All eligible categories", "select", ["All eligible categories", "Security only", "Operations only"])}${field("ai-run-budget", "Run budget (sample units)", 100, "number")}${field("ai-period-budget", "Period budget (sample units)", 500, "number")}${field("ai-user-budget", "Per-user budget (sample units)", 100, "number")}<div class="empty-note">When a budget is exhausted, record an explicit AI-coverage gap. A consultant override is audited. Protected raw AI retrieval is deferred in the pilot.</div>${item("Deep analysis", "Phase 2 · scoped preview and separate raw-evidence authorization.", link("Review preview", "ai"))}`;
  if (t === "Access")
    body = `<h2>Project roles</h2>${table(
      ["Sample user", "Role", "Authority", ""],
      [
        [
          "Sample consultant",
          "Consultant",
          "Assessment, review, profiles, publication",
          btn("Inspect role", "role-consultant"),
        ],
        [
          "Customer reviewer",
          "Qualified reviewer",
          "Confirm or refine authorized findings",
          btn("Inspect role", "role-reviewer"),
        ],
        [
          "Customer risk owner",
          "Risk owner",
          "Residual-risk decisions and acknowledgment",
          btn("Inspect role", "role-risk"),
        ],
        [
          "Evidence authorizer",
          "Customer authorizer",
          "Protected raw-evidence access decisions",
          btn("Inspect role", "role-evidence"),
        ],
        [
          "Sample auditor",
          "Auditor",
          "Read approved records and provenance",
          btn("Inspect role", "role-auditor"),
        ],
      ],
    )}<div class="row" style="margin-top:20px">${btn("Preview assignment", "access-assignment")}</div><div class="notice">All roles are scoped by customer, project, environment, assessment, action, and evidence category.</div>`;
  if (t === "Data policy")
    body = `<h2>Customer data policy · illustrative values</h2>${field("residency", "Residency", "United States", "select", ["United States"])}${field("retention", "Sample retention duration (days)", 365, "number")}${field("retention-start", "Retention start event", "Assessment completion", "select", ["Assessment completion", "Collection time", "Engagement closure"])}${field("raw-mode", "Raw-evidence processing", "Customer-side or temporary redacted processing", "select", ["Customer-side or temporary redacted processing", "Customer-side only"])}${field("export-policy", "Export review", "Customer-authorized redacted reports", "select", ["Customer-authorized redacted reports", "Customer review required"])}<div class="empty-note">These are sample choices, not approved customer policy defaults. Credentials are prohibited evidence; protected government identifiers stay customer-side or are redacted.</div>${btn("Review deletion impact", "deletion")}`;
  if (t === "Notifications")
    body = `<h2>In-product notifications</h2>${field("notify-complete", "Assessment completion", true, "checkbox")}${field("notify-failure", "Collection or assessment failure", true, "checkbox")}${field("notify-review", "Required reviews", true, "checkbox")}${field("notify-critical", "New or worsened Critical / High findings", true, "checkbox")}${field("notify-rules", "New rule catalog available", true, "checkbox")}<p class="small muted">External notification channels are deferred beyond the pilot.</p>`;
  if (t === "Integrations")
    body = `<h2>Read-only MCP</h2>${field("mcp-enabled", "Authorized health-results access", true, "checkbox")}${table(
      ["Available", "Excluded"],
      [
        [
          "Assessment status, coverage, scores",
          "Starting analysis or changing dispositions",
        ],
        ["Findings and recommendations", "Risk acceptance and publication"],
        [
          "Protected evidence references",
          "Raw-evidence access and task creation",
        ],
      ],
    )}<div class="row" style="margin-top:16px">${btn("Preview identity scope", "mcp")}</div><h2 style="margin-top:30px">Task handoff</h2>${item("CSV export", "Pilot handoff for in-product consultant tasks.", btn("Export sample tasks", "csv"))}${item("Jira · GitHub · ServiceNow", "Later-phase integration preview.", btn("Review future integration", "external"))}`;
  if (t === "Appearance")
    body = `<h2>Workspace preferences</h2>${field("theme", "Theme", "Light", "select", ["System", "Light", "Dark"])}${field("default-area", "Default area", "Overview", "select", ["Overview", "Risk analysis", "AI workspace", "Tasks & reviews"])}${field("density", "Information density", "Comfortable", "select", ["Comfortable", "Compact"])}${field("motion", "Reduced motion", false, "checkbox")}${field("home-view", "Saved view", "Assessment overview", "select", ["Assessment overview", "Security priorities", "Consultant review queue"])}<div class="notice">Density and reduced-motion preferences apply to this mockup when saved.</div>`;
  return (
    head(
      "Settings",
      "Configure the sample workspace and review administrative flows.",
    ) +
    tabs("settings", labels) +
    `<section class="panel"><form id="settings-form">${body}<div class="savebar"><span id="settings-status" class="small muted">Preview settings · saved only in this browser</span><div class="row">${btn("Reset sample settings", "reset-settings")}<button class="action" type="submit">Save preview settings</button></div></div></form></section>`
  );
}

function auditPage() {
  return (
    head(
      "Audit history",
      "Attributable evidence, review, policy, and publication events.",
    ) +
    `<section class="panel"><div class="toolbar"><h2>Activity timeline</h2><input class="search" data-table-search placeholder="Search events or actors" aria-label="Search audit history"></div>${table(
      ["Time", "Actor", "Event", "Reference", ""],
      [
        [
          "02 Oct · 12:10",
          "Sample consultant",
          "Reviewed proposed finding",
          "FIND-008",
          detailButton("Details", "audit-review"),
        ],
        [
          "02 Oct · 11:34",
          "Assessment service",
          "Locked scoring inputs",
          "ASSESS-10-02",
          detailButton("Details", "run-inputs"),
        ],
        [
          "02 Oct · 10:24",
          "Collection service",
          "Captured immutable baseline",
          "BASE-10-02",
          detailButton("Details", "baseline"),
        ],
        [
          "25 Sep · 16:20",
          "Customer risk owner",
          "Acknowledged report version",
          "REPORT-09-25-v1",
          detailButton("Details", "delivery"),
        ],
        [
          "25 Sep · 15:42",
          "Sample consultant",
          "Published redacted report",
          "REPORT-09-25-v1",
          detailButton("Details", "publication"),
        ],
        [
          "24 Sep · 14:18",
          "Customer evidence authorizer",
          "Reviewed evidence-access scope",
          "SCOPE-014",
          detailButton("Details", "authorization"),
        ],
      ],
    )}</section>`
  );
}
const screenshots = [
  [
    "overview.png",
    "Original graphical overview",
    "The recovered landing-page direction.",
  ],
  ["risk-analysis.png", "Risk analysis", "Severity and category charts."],
  [
    "relationships-overlay-desktop.png",
    "Relationships overlay",
    "Graph and object evidence together.",
  ],
  [
    "overlay-no-blur-desktop.png",
    "Overlay without blur",
    "Original contextual panel treatment.",
  ],
  [
    "relationships-overlay-mobile.png",
    "Mobile relationships",
    "Compact relationship inspector.",
  ],
  [
    "drilldown-evidence.png",
    "Evidence workspace",
    "Expanded evidence exploration.",
  ],
  [
    "evidence.png",
    "Original evidence concept",
    "The initial graph-led evidence screen.",
  ],
  [
    "drilldown-desktop.png",
    "Initial finding drilldown",
    "Earlier inspector composition for comparison.",
  ],
  [
    "drilldown-mobile.png",
    "Initial mobile drilldown",
    "Earlier compact finding layout.",
  ],
  [
    "overlay-no-blur-mobile.png",
    "Mobile overlay without blur",
    "The original compact overlay treatment.",
  ],
];
function galleryPage() {
  return (
    head(
      "Design archive",
      "Browse the original screenshots from the work directory.",
      `<a class="action quiet" href="http://127.0.0.1:5210/" target="_blank" rel="noopener">Open recovered original ↗</a>`,
    ) +
    `<div class="shots">${screenshots.map(([file, title, desc]) => `<article class="shot"><button data-shot="${file}" aria-label="Enlarge ${title}"><img src="screenshots/${file}" alt="${title}" loading="lazy"><h3>${title}</h3><p class="small muted">${desc}</p></button></article>`).join("")}</div>`
  );
}
const extraPages = {
  projects: projectsPage,
  sources: sourcesPage,
  assessments: assessmentsPage,
  outcomes: outcomesPage,
  ai: aiPage,
  fixes: fixesPage,
  tasks: tasksPage,
  reports: reportsPage,
  compare: comparePage,
  rules: rulesPage,
  migration: migrationPage,
  portfolio: portfolioPage,
  settings: settingsPage,
  audit: auditPage,
  gallery: galleryPage,
};

function toast(msg) {
  const el = document.querySelector("#mock-toast");
  el.textContent = msg;
  clearTimeout(mock.toastTimer);
  mock.toastTimer = setTimeout(() => (el.textContent = ""), 4500);
}
function navigate(area) {
  if (!names[area]) return;
  state.page = area;
  state.selected = null;
  if (area === "evidence") {
    state.selected = "risk-1";
    state.node = "role";
  }
  root.classList.remove("menu-open");
  render();
  if (location.hash !== "#" + area) history.pushState(null, "", "#" + area);
  window.scrollTo(0, 0);
  save();
}
function openFinding(id) {
  state.page = "risk";
  state.selected = id;
  state.returnId = null;
  state.tab = "Relationships";
  state.node = "role";
  root.classList.remove("menu-open");
  render();
  focus('[data-detail-tab="Relationships"]');
  save();
}
const dialog = document.querySelector("#mock-dialog");
function modal(title, body, footer = "") {
  dialog.innerHTML = `<div class="dialog-head"><h2>${escapeHtml(title)}</h2><button data-dialog-close aria-label="Close dialog">✕</button></div><div class="dialog-body">${body}</div><div class="dialog-footer"><button data-dialog-close>Close</button>${footer}</div>`;
  dialog.showModal();
}
const modalButton = (label, action) =>
  `<button class="primary" data-modal-action="${action}">${label}</button>`;
function download(name, body, type) {
  const url = URL.createObjectURL(new Blob([body], { type }));
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
  toast("Sample file downloaded.");
}
const details = {
  synchronization: [
    "Synchronization evidence",
    "18 sample projects · 2 gaps",
    "Metadata traces the project to its workflows and target-system references. Two sample configuration references are unavailable.",
  ],
  queues: [
    "Operational evidence",
    "Job Queue / DBQueue",
    "Sample records cover the available 90-day window. Retry and latency results remain linked to collection time, exclusions, and source availability.",
  ],
  scripts: [
    "Customization evidence",
    "28 customizations · 5 gaps",
    "Normalized script metadata and supported out-of-the-box comparisons. Secret-bearing content and protected identifiers are excluded.",
  ],
  destination: [
    "Destination inventory",
    "One Identity Manager · non-production",
    "Installed modules, native connectors, schemas, and supported change mechanisms must be discovered before mappings are approved.",
  ],
  "mapping-role": [
    "Role mapping preview",
    "Finance role → business role and assignments",
    "Review criteria-derived membership, effective entitlements, identity matching, and existing-object disposition. Ambiguous references become consultant tasks.",
  ],
  "mapping-access": [
    "Access-profile mapping",
    "Payment access → requestable service item",
    "Check source-to-target cardinality, request behavior, provisioning policy, and entitlement equivalence. Missing destination capabilities need an explicit decision.",
  ],
  "mapping-cert": [
    "Certification mapping",
    "Future campaign → attestation policy",
    "Preserve campaign scope, reviewers, settings, and validation. Active campaign rounds remain in SailPoint. Fatal scope or reviewer mismatches block acceptance.",
  ],
  "mapping-jml": [
    "Lifecycle mapping",
    "Joiner workflow → process orchestration",
    "Review trigger, conditions, dependencies, custom rules, unsupported systems, and workflow holds. In-flight source work remains distinct.",
  ],
  "scope-approval": [
    "Scope approval preview",
    "Customer decision required",
    "Disposition, rationale, warnings, and the chosen baseline are included in an attributable approval record. This prototype does not approve scope.",
  ],
  "scope-role": [
    "Object disposition",
    "Finance role · proposed migration",
    "Preserve approved access intent. Source evidence, proposed mapping, consultant refinement, and customer decision remain traceable.",
  ],
  "scope-script": [
    "Object disposition",
    "Legacy script · proposed redesign",
    "Supported native behavior is preferred when it meets the intended outcome. Review unsupported logic and dependencies before a customer decision.",
  ],
  "scope-cert": [
    "Campaign disposition",
    "Active round · keep in source",
    "Complete the in-flight round in SailPoint. Future campaigns may be mapped after scope and reviewer equivalence are reviewed.",
  ],
  "validation-criteria": [
    "Validation criteria",
    "Non-production evidence",
    "Compare behavioral equivalence, effective entitlements, membership, reviewer matching, provisioning effects, and recovery. Fatal mismatches prevent acceptance.",
  ],
  acceptance: [
    "Acceptance record preview",
    "Non-production customer acceptance",
    "Record the reviewed baseline, criteria, grades, mismatches, warnings, and decision owner. Production has a separate explicit approval boundary.",
  ],
  "outcome-provisioning": [
    "Outcome adherence",
    "Timely account provisioning",
    "Sample queue evidence suggests delays. Compare workflow completion timing to customer-approved intent before confirming a deviation.",
  ],
  "outcome-review": [
    "Outcome adherence",
    "Privileged-access review",
    "AI inference remains proposed. Confirm ownership, review evidence, and the intended privileged-access control.",
  ],
  "outcome-audit": [
    "Outcome adherence",
    "Traceable changes",
    "Sample audit metadata supports the stated outcome. Evidence time, source, and retained history remain visible.",
  ],
  "outcome-supported": [
    "Outcome adherence",
    "Supported configuration",
    "Compare normalized customization metadata against authoritative out-of-the-box behavior. Unsupported checks remain explicit gaps.",
  ],
  role: [
    "Role evidence",
    "ROLE-014 · Finance operations",
    "Normalized sample role metadata. Inherits AP approver, which links to Payment approval.",
  ],
  workflow: [
    "Workflow evidence",
    "WF-006 · Account sync",
    "Two sample workflow records have unavailable configuration references; they remain explicit gaps.",
  ],
  baseline: [
    "Baseline provenance",
    "BASE-2026-10-02",
    "Captured 02 Oct 2026 at 10:24. Immutable sample baseline, 312 objects, redacted normalized evidence, 13 explicit gaps.",
  ],
  "run-inputs": [
    "Locked assessment inputs",
    "ASSESS-2026-10-02",
    "Baseline BASE-2026-10-02; catalog 1.4; Standard profile; desired-outcome version 2; AI versions retained with proposals.",
  ],
  "accepted-risk": [
    "Accepted-risk record",
    "Customer risk owner · fictional decision",
    "Rationale: temporary operating constraint. Compensating control: targeted access review. Next review: 15 Dec 2026. Score penalty and recommendations remain.",
  ],
  delivery: [
    "Report acknowledgment",
    "REPORT-2026-09-25-v1",
    "Acknowledged by the sample customer risk owner through a redacted report link. Acknowledgment does not imply acceptance of every finding.",
  ],
  "outcome-separation": [
    "Desired-outcome review",
    "Separate payment responsibilities",
    "Customer-defined intent. Linked sample role evidence suggests a conflict; review actual assignments and the inherited permission path.",
  ],
  "rule-security": [
    "Deterministic rule",
    "SEC-014 · catalog 1.4",
    "Checks inherited grants against recorded separation intent. Evidence, version, and applicability stay attached to each result.",
  ],
  "rule-ai": [
    "AI-assisted rule",
    "Proposed until qualified review",
    "The rule asks for evidence-grounded inference from redacted configuration. Missing context must be stated and output remains proposed.",
  ],
  "source-delta": [
    "Configuration delta",
    "4 role-owner references changed",
    "Changes are attributed to the source baseline rather than rule or profile updates. Before and after values are fictional redacted metadata.",
  ],
  "role-consultant": [
    "Consultant authority",
    "Assigned engagement only",
    "Configure and start assessments, review proposals, plan tasks, and publish. Customer residual-risk acceptance and protected evidence authorization remain separate.",
  ],
  "role-risk": [
    "Customer risk-owner authority",
    "Authorized customer scope",
    "Residual-risk acceptance requires an owner, rationale, compensating controls, and a review or expiry date within one year.",
  ],
  "role-evidence": [
    "Evidence authorizer",
    "Customer-only responsibility",
    "Protected raw-evidence authorization is separate from consultant assessment authority.",
  ],
  "role-reviewer": [
    "Qualified customer reviewer",
    "Granted assessment scope",
    "Confirm or refine AI proposals within the granted customer scope.",
  ],
  "role-auditor": [
    "Auditor authority",
    "Read-only",
    "Read approved reports, provenance, rules, dispositions, risk decisions, and audit history.",
  ],
};
function inspect(key) {
  const d = details[key] || [
    "Sample detail",
    key.replaceAll("-", " "),
    "This fictional record illustrates the linked evidence, provenance, owner, and review context for this area. No customer data is loaded.",
  ];
  modal(
    d[0],
    `<p><b>${d[1]}</b></p><p>${d[2]}</p><p style="color:#59677c;font-size:12px">Sample assessment · BASE-2026-10-02 · design review only</p>`,
  );
}

function action(key) {
  switch (key) {
    case "assessment":
      mock.wizard = 0;
      assessmentWizard();
      break;
    case "project":
      modal(
        "Create sample project",
        '<label>Project name<input id="new-project-name" value="New health assessment"></label><label>Source<select><option>One Identity Manager 10.x</option><option>SailPoint ISC · future engagement</option></select></label><label>Environment<select><option>Non-production</option><option>Production · read-only health assessment</option></select></label>',
        modalButton("Create preview project", "create-project"),
      );
      break;
    case "collect":
      modal(
        "Collect a sample baseline",
        "<p><b>One Identity Manager · read-only</b></p><p>Roles, workflows, synchronization, queue metadata, and customizations will be represented in a new immutable baseline.</p><p>Operational lookback: 90 days, bounded by source availability. Exclusions and redaction remain in scope.</p>",
        modalButton("Simulate collection", "collect"),
      );
      break;
    case "connection":
      modal(
        "Connection configuration",
        "<label>Connection method<select><option>Dedicated read-only SQL Server account</option><option>Customer-side collector</option></select></label><p>Credentials stay in the appropriate secret store. The prototype does not request or store them.</p>",
        modalButton("Preview permission check", "connection"),
      );
      break;
    case "collection-scope":
      modal(
        "Collection scope",
        "<p>✓ Roles and access metadata<br>✓ Workflows and processes<br>✓ Synchronization projects<br>✓ Job Queue / DBQueue operational evidence<br>✓ Script and customization metadata</p><p>Secrets excluded. Protected identifiers remain customer-side or redacted.</p>",
      );
      break;
    case "outcome":
      modal(
        "Add desired outcome",
        '<label>Desired outcome<input id="outcome-name" value="Review all privileged access"></label><label>Source of intent<select><option>Customer-defined</option><option>AI-inferred · proposed</option></select></label><label>Success criterion<textarea>Privileged assignments have an owner and documented review.</textarea></label>',
        modalButton("Add sample outcome", "outcome"),
      );
      break;
    case "inference":
      modal(
        "Review inferred outcome",
        "<p><b>Timely account provisioning</b></p><p>AI inference from workflow and queue metadata. Customer intent has not been confirmed.</p><label>Review note<textarea>Confirm the provisioning objective with the customer.</textarea></label>",
        modalButton("Save sample review", "review"),
      );
      break;
    case "deep":
      modal(
        "Deep-analysis scope preview",
        "<p><b>Phase 2 · sample only</b></p><p>Scope: role inheritance and privileged access<br>Data: normalized, redacted role configuration<br>Illustrative usage: 15–30 units<br>Illustrative duration: 2–5 minutes</p><p>Protected raw retrieval requires separate customer authorization. Output remains proposed.</p>",
        modalButton("Simulate analysis proposal", "deep"),
      );
      break;
    case "ai-evidence":
      openFinding("risk-1");
      break;
    case "quality":
      mock.tab.reports = "Assessment quality";
      navigate("reports");
      break;
    case "fix-role":
    case "fix-owner":
    case "fix-queue":
    case "fix-script":
      modal(
        "Fix-package review",
        `<p><b>${key === "fix-role" ? "FIX-001 · Separate role inheritance" : key === "fix-owner" ? "FIX-002 · Privileged-access ownership" : key === "fix-queue" ? "FIX-003 · Workflow retries" : "FIX-004 · Repeated custom logic"}</b></p><p><b>Proposed option:</b> prefer supported configuration that meets the customer outcome.</p><p><b>Prerequisites:</b> confirm intent, identify affected objects, and agree on an owner.</p><p><b>Validation:</b> compare the role graph and effective permissions in new evidence.</p><p><b>Recovery:</b> consultant-reviewed customer-side recovery guidance; no changes are executed here.</p><label>Consultant review note<textarea>Review the dependencies before task planning.</textarea></label>`,
        modalButton("Create sample consultant task", "fix-task"),
      );
      break;
    case "package":
      modal(
        "Group a fix package",
        '<label>Package name<input value="Role governance improvements"></label><p>Group related findings by common root cause, affected objects, prerequisites, and validation.</p>',
        modalButton("Save sample package", "package"),
      );
      break;
    case "task":
      modal(
        "New consultant task",
        '<label>Title<input id="task-title" value="Review role boundary prerequisites"></label><label>Assigned role<select><option>Consultant</option><option>Customer reviewer</option></select></label><label>Linked package<select><option>FIX-001 · Role inheritance</option><option>FIX-003 · Workflow retries</option></select></label>',
        modalButton("Create sample task", "task"),
      );
      break;
    case "csv":
      download(
        "sample-consultant-tasks.csv",
        "id,title,state,owner\n" +
          taskData
            .map((x) =>
              [x.id, x.title, mock.tasks[x.id] || x.state, x.owner]
                .map((v) => '"' + v.replaceAll('"', '""') + '"')
                .join(","),
            )
            .join("\n"),
        "text/csv",
      );
      break;
    case "markdown":
      download(
        "sample-assessment-draft.md",
        "# Sample health assessment — draft\n\nFictional data for UI review.\n\n- Health: 72/100 (provisional)\n- Baseline: BASE-2026-10-02\n- Rule catalog: 1.4\n- Profile: Standard\n- Findings: 33; 6 AI proposals awaiting review\n- Evidence gaps: 13\n\n## Priority\nReview conflicting role inheritance and privileged-access ownership.\n\nNo customer report was published.",
        "text/markdown",
      );
      break;
    case "print":
      window.print();
      break;
    case "publish":
      modal(
        "Publication review",
        "<p><b>Draft → immutable report version</b></p><p>Warnings: 6 AI proposals remain unreviewed; 13 evidence gaps remain explicit.</p><p>Audience: redacted customer report. Baseline BASE-10-02, catalog 1.4, Standard profile.</p><label>Publication note<textarea>Sample consultant acknowledges the listed limitations.</textarea></label>",
        modalButton("Simulate publication", "publish"),
      );
      break;
    case "share":
      modal(
        "Redacted report sharing",
        "<label>Link expiry<select><option>24 hours</option><option>12 hours</option><option>1 hour</option></select></label><p>Passcode protection · revocable link · raw evidence excluded · access audited.</p>",
        modalButton("Generate example delivery record", "share"),
      );
      break;
    case "comparison":
      modal(
        "Choose comparison baseline",
        "<label>Compare current run against<select><option>25 Sep 2026 · immediate prior</option><option>18 Sep 2026 · approved baseline</option></select></label><p>Configuration, evidence, rule, profile, outcome, and disposition changes remain separate.</p>",
        modalButton("Apply sample comparison", "comparison"),
      );
      break;
    case "custom-rule":
      modal(
        "Custom-rule validation preview",
        "<p><b>Phase 3 · authoring preview</b></p><p>Category: Security<br>Applicability: privileged roles<br>Sample result: 4 missing owner references</p><p>Rule code, version, evidence requirements, and review would be retained before catalog activation.</p>",
      );
      break;
    case "reset-settings":
      mock.settings = {};
      localStorage.removeItem("iga-platform-mock-settings");
      applyPreferences();
      render();
      toast("Sample settings reset.");
      break;
    case "mcp":
      modal(
        "Read-only MCP identity scope",
        "<p>Named identity: sample-auditor<br>Customer: Sample organization<br>Environment: Production<br>Capabilities: status, scores, coverage, findings, recommendations, evidence references.</p><p>No analysis, disposition, risk acceptance, publication, raw evidence, or task actions.</p>",
      );
      break;
    case "external":
      modal(
        "Future task integration",
        "<p>Jira, GitHub, and ServiceNow handoff is deferred beyond the pilot. In-product consultant tasks and CSV export are shown in this mockup.</p>",
      );
      break;
    case "deletion":
      modal(
        "Deletion impact preview",
        "<p>Sample impact: 3 historical evidence baselines. Retained assessment records would explicitly show unavailable underlying evidence.</p><p>Actual deletion requires applicable customer policy and authority. This preview deletes nothing.</p>",
      );
      break;
    case "access-assignment":
      modal(
        "Access assignment preview",
        '<label>Sample user<input value="Customer reviewer"></label><label>Role<select><option>Qualified customer reviewer</option><option>Auditor · read-only</option></select></label><p>Scope: Sample organization / Production assessment.</p>',
        modalButton("Save sample assignment", "access"),
      );
      break;
    case "notifications":
      modal(
        "Notifications",
        "<p><b>Assessment completed with gaps</b><br>02 Oct · 13 evidence gaps remain.</p><p><b>6 AI proposals require review</b><br>Critical and High proposals take priority.</p><p><b>Rule catalog 1.4 available</b><br>Historical assessments remain unchanged.</p>",
        modalButton("Open review queue", "notifications"),
      );
      break;
    case "prerequisites":
      modal(
        "Migration prerequisites",
        "<p>□ Confirm installed destination modules<br>□ Review native connector availability<br>□ Resolve 2 identity-matching ambiguities<br>□ Approve source-object dispositions<br>□ Review validation and recovery plan</p>",
      );
      break;
    case "matching":
      modal(
        "Identity matching preview",
        "<p>2 source identities have multiple candidate destination matches. These references become consultant tasks; the system must not silently choose a match.</p>",
      );
      break;
    case "handoff":
      modal(
        "Handoff package contents",
        "<p>01 · Approved scope and object dispositions<br>02 · Source and destination inventories<br>03 · Mapping and implementation recipes<br>04 · Prerequisites and consultant tasks<br>05 · Validation criteria and evidence<br>06 · Warnings, acceptance, and recovery guidance</p><p>Phase 4 preview · no executable package generated.</p>",
      );
      break;
    default:
      inspect(key);
  }
}
function assessmentWizard() {
  const titles = ["Select evidence", "Configure assessment", "Review run"];
  const content = [
    `<label>Evidence baseline<select><option>BASE-2026-10-02 · current</option><option>BASE-2026-09-25 · prior</option></select></label><p>One Identity Manager 10.x · 312 sample objects · 13 explicit gaps.</p>`,
    `<label>Profile<select><option>Standard · equal category weights</option><option>Security focused · sample</option></select></label><label>Rule catalog<select><option>1.4</option></select></label><label>AI mode<select><option>Automatic · eligible redacted evidence</option><option>Disabled · record AI coverage gap</option></select></label>`,
    `<p>Source: One Identity Manager 10.x<br>Baseline: BASE-2026-10-02<br>Profile: Standard<br>Catalog: 1.4<br>AI: automatic general analysis · redacted evidence</p><p>This simulation starts no real assessment.</p>`,
  ];
  modal(
    `New assessment · ${mock.wizard + 1} of 3`,
    content[mock.wizard],
    modalButton(
      mock.wizard === 2 ? "Simulate assessment" : "Continue",
      "wizard",
    ),
  );
}
function applyPreferences() {
  document.documentElement.style.colorScheme =
    (mock.settings.theme || "Light") === "Light"
      ? "light"
      : mock.settings.theme === "Dark"
        ? "dark"
        : "light dark";
  root.style.colorScheme = "inherit";
  document.documentElement.style.setProperty(
    "--mock-motion",
    mock.settings.motion ? "0s" : ".18s",
  );
  root.classList.toggle("compact", mock.settings.density === "Compact");
  let style = document.querySelector("#preference-style");
  if (!style) {
    style = document.createElement("style");
    style.id = "preference-style";
    document.head.append(style);
  }
  style.textContent =
    (mock.settings.motion
      ? "*{animation:none!important;transition:none!important}"
      : "") +
    (mock.settings.density === "Compact"
      ? "#iga-visual-review td{padding:9px 10px}#iga-visual-review .list-item{padding:11px 0}"
      : "");
}

root.addEventListener("click", (e) => {
  const b = e.target.closest("button");
  if (!b) return;
  if (b.type !== "submit") e.preventDefault();
  if (b.dataset.areaTab) {
    mock.tab[b.dataset.areaTab] = b.dataset.value;
    render();
  } else if (b.dataset.action) {
    action(b.dataset.action);
  } else if (b.dataset.inspect) {
    inspect(b.dataset.inspect);
  } else if (b.dataset.evidenceFinding) {
    state.selected = b.dataset.evidenceFinding;
    state.node = "role";
    mock.tab.evidence = "Relationships";
    render();
    window.scrollTo(0, 0);
  } else if (b.dataset.aiFinding) {
    openFinding(b.dataset.aiFinding);
  } else if (b.dataset.prompt) {
    mock.chat.push(b.dataset.prompt);
    render();
    focus("#ai-question");
  } else if (b.dataset.task) {
    const task = taskData.find((x) => x.id === b.dataset.task);
    modal(
      task.title,
      `<p>${task.id} · ${task.owner}</p><p>${task.desc}</p><label>Sample task status<select id="task-state">${["To review", "In progress", "Complete"].map((s) => `<option ${(mock.tasks[task.id] || task.state) === s ? "selected" : ""}>${s}</option>`).join("")}</select></label><label>Review note<textarea>Evidence and prerequisite review.</textarea></label>`,
      modalButton("Save sample task", "save-task:" + task.id),
    );
  } else if (b.dataset.shot) {
    const shot = screenshots.find((x) => x[0] === b.dataset.shot);
    modal(
      shot[1],
      `<img src="screenshots/${shot[0]}" alt="${shot[1]}"><p>${shot[2]}</p>`,
    );
  } else if (b.dataset.menu) {
    root.classList.toggle("menu-open");
  } else if (b.dataset.page || b.dataset.go) {
    if (b.textContent.includes("Configure AI")) mock.tab.settings = "AI";
    if (b.textContent.includes("Review 6 proposals"))
      mock.tab.tasks = "Review queue";
    if (b.textContent.includes("assessment-quality"))
      mock.tab.reports = "Assessment quality";
    render();
    root.classList.remove("menu-open");
    history.replaceState(null, "", "#" + state.page);
    window.scrollTo(0, 0);
  }
});
root.addEventListener("input", (e) => {
  if (e.target.matches("[data-table-search]")) {
    const q = e.target.value.toLowerCase();
    e.target
      .closest(".panel")
      .querySelectorAll("tbody tr")
      .forEach((tr) => (tr.hidden = !tr.textContent.toLowerCase().includes(q)));
  }
  if (e.target.dataset.setting) {
    const status = root.querySelector("#settings-status");
    if (status) status.textContent = "Unsaved preview changes";
  }
});
root.addEventListener("submit", (e) => {
  e.preventDefault();
  if (e.target.id === "ai-form") {
    const input = root.querySelector("#ai-question");
    const q = input.value.trim();
    if (q) {
      mock.chat.push(q);
      render();
      focus("#ai-question");
    }
  }
  if (e.target.id === "settings-form") {
    root
      .querySelectorAll("[data-setting]")
      .forEach(
        (el) =>
          (mock.settings[el.dataset.setting] =
            el.type === "checkbox"
              ? el.checked
              : el.type === "number"
                ? Number(el.value)
                : el.value),
      );
    if (
      (mock.tab.settings || "General") === "Assessment" &&
      categoryNames.reduce(
        (n, c) => n + (Number(mock.settings["weight-" + c]) || 0),
        0,
      ) !== 100
    ) {
      toast("Category weights must add up to 100%.");
      return;
    }
    localStorage.setItem(
      "iga-platform-mock-settings",
      JSON.stringify(mock.settings),
    );
    applyPreferences();
    root.querySelector("#settings-status").textContent =
      "Saved in this browser · preview only";
    toast("Preview settings saved.");
  }
});
dialog.addEventListener("click", (e) => {
  const b = e.target.closest("button");
  if (!b) return;
  if (b.hasAttribute("data-dialog-close")) {
    dialog.close();
    return;
  }
  const key = b.dataset.modalAction;
  if (!key) return;
  if (key === "wizard") {
    if (mock.wizard < 2) {
      mock.wizard++;
      dialog.close();
      assessmentWizard();
    } else {
      dialog.close();
      navigate("assessments");
      toast("Sample assessment prepared. No run was started.");
    }
    return;
  }
  if (key.startsWith("save-task:")) {
    mock.tasks[key.split(":")[1]] = dialog.querySelector("#task-state").value;
    dialog.close();
    render();
    toast("Sample task status updated.");
    return;
  }
  if (key === "notifications") {
    dialog.close();
    mock.tab.tasks = "Review queue";
    navigate("tasks");
    return;
  }
  if (key === "task" || key === "fix-task") {
    const title =
      dialog.querySelector("#task-title")?.value.trim() ||
      "Review role boundary prerequisites";
    taskData.push({
      id: "T-" + String(taskData.length + 16),
      title,
      state: "To review",
      owner: "Consultant",
      severity: "Medium",
      desc: "Sample task linked to FIX-001.",
    });
    dialog.close();
    navigate("tasks");
    toast("Sample task added to the review board.");
    return;
  }
  dialog.close();
  toast(
    {
      collect: "Sample collection preview complete. No database was contacted.",
      connection: "Sample permission check passed. No connection was made.",
      publish: "Publication flow reviewed. No report was published.",
      share: "Example delivery record prepared. No link was sent.",
      deep: "Sample analysis proposal prepared. No AI request was sent.",
      outcome: "Sample outcome flow reviewed.",
      review: "Sample review recorded for this session.",
      access: "Sample assignment flow reviewed. Permissions are unchanged.",
      comparison: "Sample comparison selected.",
      package: "Sample package flow reviewed.",
      "create-project":
        "Project creation flow reviewed. No real project was created.",
    }[key] || "Sample flow reviewed.",
  );
});
window.addEventListener("hashchange", () => navigate(location.hash.slice(1)));
document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") root.classList.remove("menu-open");
});
applyPreferences();
