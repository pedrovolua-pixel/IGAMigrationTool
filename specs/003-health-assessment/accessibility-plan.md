# Accessibility Plan: Health-Assessment Pilot

Status: Approved  
Target: WCAG 2.2 AA  
Owner: Accessibility reviewer  
Last updated: 2026-09-28

## Scope

The plan covers assessment configuration/progress, coverage and quality views, dashboards, filters, saved views, graphs, side panels, evidence views, finding review, comments/dispositions, risk acceptance, recommendations, fix packages, tasks, comparisons, publication warnings, report acknowledgment, expiring-link reports, PDF and Markdown. MCP has no visual interface but its structured results must preserve labels and non-color status meaning.

## Interaction requirements

- All actions and disclosures are keyboard operable with visible focus and logical order.
- Opening a side panel moves focus to its heading or first actionable control; closing returns focus to the invoking element even after filtering/lazy loading.
- Dialogs trap focus only while modal, expose name/role/description, support Escape when safe, and return focus correctly.
- Destructive or consequential actions state impact, scope and required authority and provide accessible validation/error summaries.
- Dynamic progress, completion, error, review requirement and publication warning changes use appropriate live-region behavior without excessive announcements.
- Virtualized/lazy-loaded lists expose total/loaded position where known, stable keyboard navigation and a non-virtualized accessible path for assistive technology when needed.
- Drag-only graph/layout interactions have equivalent controls; no timed interaction is required without an extension/control.

## Information and visual requirements

- Severity, confidence, lifecycle state, score status, gap state and review state use text/icon labels in addition to color.
- Text/controls/focus indicators meet WCAG 2.2 AA contrast and target-size expectations.
- Heading hierarchy, landmarks, table captions/headers, form labels/instructions and error associations are semantic.
- Graphs have a table or structured-text equivalent containing the same material nodes, edges, filters, values and current selection.
- Scores include numeric value, named status, basis, assessed categories and gap/quality caveat.
- Confidence percentage and band remain distinct from severity.
- Charts do not rely on hover; tooltips are keyboard/touch accessible and dismissible.
- Language is understandable to executive and IGA practitioner audiences; abbreviations are expanded on first use in each report context.

## Workflow acceptance scenarios

1. Configure and start a run entirely by keyboard, review locked inputs/warnings and find progress/error details with a screen reader.
2. Navigate dashboard KPIs, apply/remove filters, open a graph equivalent, inspect a finding/evidence panel and return without losing context.
3. Review, reject/confirm, comment and resolve a finding with required-field errors announced and focus placed correctly.
4. Attempt unauthorized risk acceptance and understand why customer authority is required; authorized flow exposes all owner/rationale/control/expiry fields.
5. Publish with incomplete Critical/High review and coverage; warnings are prominent, read before confirmation and not color-only.
6. Compare assessments and identify new/worsened/resolved/reopened findings through text/table output.
7. Open PDF and Markdown outputs and locate version, headings, tables, findings, quality limitations and evidence references.
8. Use the passcode report flow with labeled inputs, generic but actionable errors, attempt/expiry state and no inaccessible CAPTCHA dependency.

## PDF and Markdown

- PDF uses tagged structure, document title/language, logical reading order, bookmarks for major sections, tagged tables/lists, meaningful link text, alternative text or long descriptions and no essential content embedded only as an image.
- Markdown uses stable headings/identifiers, descriptive links, tables with headers, plain-text status labels and structured conventions required by NFR-EXP-9.
- Graph images in PDF include an adjacent equivalent table/text summary. Decorative images are marked decorative.
- Rendered artifacts are manually checked with at least one screen reader and keyboard-only workflow; PDF requires an accessibility checker plus manual reading-order/table verification.

## Verification matrix

| Area | Automated | Manual | Required evidence |
|---|---|---|---|
| Semantic UI and common WCAG rules | Accessibility scanner in component/integration/E2E tests | Accessibility reviewer | Reports with zero untriaged serious/critical violations |
| Keyboard/focus | Focus assertions where stable | Keyboard walkthrough | Recorded scenario results and defects |
| Screen reader | Limited role/name/state assertions | At least one desktop screen reader/browser combination; mobile if supported | Scenario notes and output announcements |
| Graph/table equivalence | Data-contract comparison | Practitioner/accessibility review | Same material node/edge/value/filter coverage |
| Color/contrast/zoom/reflow | Automated contrast where possible | 200%/400% zoom and reflow review | Screenshots/results without lost content/function |
| PDF | PDF checker/tag assertions | Reading order, tables, links, headings, alt text | Checked pilot report artifact |
| Markdown | Structure/link linter | Screen-reader/plain-text inspection | Stable-ID and content-parity report |
| Lazy loading | DOM/accessibility-tree assertions | Keyboard/screen-reader large-list scenario | Position/context preserved at approved scale |

## Environment coverage

The approved pilot support matrix is managed, fully patched Windows 11 Enterprise x64 on a Microsoft-supported General Availability release with current patched Microsoft Edge Stable, Google Chrome Stable and Firefox ESR. Each release candidate records the exact Windows edition/version/build/updates, browser versions, assistive-technology versions and artifact versions used for acceptance.

- Run every workflow acceptance scenario with current stable NVDA and Edge Stable.
- Run targeted compatibility verification with NVDA and Firefox ESR and with the Windows-build Narrator version and Edge Stable.
- Run keyboard-only and 200%/400% zoom/reflow verification in Edge Stable, Chrome Stable and Firefox ESR.
- Run Windows contrast-theme verification in Edge Stable and Firefox ESR.
- Verify published PDFs in current patched Adobe Acrobat Reader with NVDA; browser-integrated PDF readers are smoke coverage only.
- Exclude outdated or preview browser channels, Internet Explorer mode, mobile/tablet browsers, Safari/macOS, ChromeOS and Linux desktop from pilot support. These exclusions do not waive WCAG 2.2 A/AA requirements on supported essential flows.

## Defect policy

Any WCAG 2.2 A/AA failure on an essential pilot flow blocks acceptance. A non-essential isolated issue requires documented impact, workaround, owner and authorized acceptance; this plan does not accept such risk. Automated tool success alone is not accessibility approval.

## Open decisions

- Prince 17 is selected as the PDF/UA renderer under IMP-DEC-004. It remains disabled until representative reports pass automated PDF/UA validation plus manual reading-order, table, link, heading, alternative-text and screen-reader verification.
- Decide whether any graph type cannot provide full equivalent table/text content; if so, remove it from pilot scope rather than ship an inaccessible material view.

## Approval

Accessibility reviewer: Repository owner  
Product owner: Repository owner  
Date: 2026-09-28
