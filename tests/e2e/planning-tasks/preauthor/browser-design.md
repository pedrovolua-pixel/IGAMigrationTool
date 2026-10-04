# V14 browser verification preauthor packet

Status: semantic case design only; engineering contract and actual implementation pending. This packet does not assert runtime PASS.

| Case | Independent expected outcome | Trace |
| --- | --- | --- |
| Convert one selected complete reviewed option | Explicit reason creates one Planned task; full creation/current source, three original Unverified artifacts and planning-only warnings readable | TC14-T01/T07/T11 |
| Convert same identity again after advancing revision | Typed no-write AlreadyExists leads to actual coherent reread, no implicit reopen/reconfirm | TC14-T04/T07 |
| AlreadyExists after inspected withdrawal or Rejected source | Existing identity only, no history/binding change; absent-task creation still denied | TC14-T04/T11 |
| Selected withdrawal then re-review with unchanged package bytes | NeedsReconfirmation, original source preserved, status unchanged; explicit allowed reconfirm adds one binding event | TC14-T03/T07 |
| Unselected attestation-only change | Selected task remains CurrentPlan | TC14-T03 |
| Unrelated finding review/comment changes full source | Selected task becomes NeedsReconfirmation even if displayed option/artifact text is identical | TC14-T03/T07 |
| Completed/Cancelled source change and explicit reopen | Terminal status remains; reopen Planned does not make plan current; explicit reconfirm required | TC14-T03/T11 |
| Stale/Rejected maintenance | Comment all states; cancel only Planned/InProgress; InProgress→Planned; no start/complete/reconfirm/reopen on Rejected | TC14-T11 |
| SourceUnavailable | No mutation/replay authority; bounded proven metadata/history only; no fallback current content | TC14-T02/T12 |
| Lost committed response | Retain exact event/reason/source/vector/revision command; explicit Retry gets original historical receipt; reread current view; no duplicate event | TC14-T04/T07 |
| Malformed committed receipt | Uncertain state and error focus; explicit identical retry, no silent command replacement or duplicate | TC14-T07/T09 |
| Changed accepted UUID payload | Conflict, entered text retained, refresh/new explicit action; no prior-state restoration | TC14-T04/T07 |
| Held old read/receipt across run or option selection | Fulfilled/completed route plus processing barrier cannot restore old selection/view; frozen uncertain command remains scoped | TC14-T07 |
| Held receipt with actual same-run source refresh | New source epoch and revisioned vector retained after old completion; generation switch cannot mask test | TC14-T07 |
| Corrupt/coherently rehashed bad DTO | Closed/source/vector/history/receipt validation withholds current authority and supplied action URLs | TC14-T09/T12 |
| Hostile reason/comment/code | Exact React text nodes including controls/Unicode; no active injected nodes/events/forms/resources/download/export/network effects | TC14-T09 |
| Keyboard action and native disclosure | Actual Enter/Space conversion/reconfirm/status/reopen/comment, reason required, focus and labels verified | TC14-T10 |
| Desktop/mobile/320 | Complete readable rows without horizontal clipping; screenshots with initial caret; axe violations and incomplete separate | TC14-T10 |
| Ten historical profiles and disabled feature | No task authority, exact compatibility, existing findings/artifacts/guidance/AI/drafts/scores unchanged | TC14-T08/T11 |
| Telemetry negative corpus | No reason/comment/artifact/source values in original host operational log | TC14-T12 |

Isolation: sanitize explicit environment before loading browser libraries; route interception precedes navigation; exact owned URL/method allowlist; abort every unexpected request. Own only a free loopback host after coordinator handoff, close in nested finally, never terminate a pre-existing listener. All held operations get immediate release cleanup and explicit response-completion barriers. No provider, credentials, customer data, export or production endpoint. Manual screen reader/Windows/zoom/deployed sandbox and default Mac startup using an override remain NOT VERIFIED.
