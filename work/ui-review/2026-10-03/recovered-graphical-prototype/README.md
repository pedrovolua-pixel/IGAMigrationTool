# Recovered graphical prototype

Open <http://127.0.0.1:5210/>. Today's application UI comparison remains at <http://127.0.0.1:5207/>.

The owner identified `file:///private/tmp/iga-overlay-style.html` and **Research Top UI Patterns and Mockups** as the preferred UI reference. That file is a stylesheet fragment, not the complete page. The research chat's final accepted prototype is `risk-drilldown-review.html`, with the complete standalone rendering at `/private/tmp/iga-relationships-panel.html`. Both are intact and have been copied here byte-for-byte, along with the referenced stylesheet and initial graphical concept. The manifest records their original paths and hashes.

The accepted direction in that chat was a chart-led Overview, graphical Risk analysis, overlay panels with no blur, and Relationships plus selected-object evidence in the same panel. An explicit Expand workspace action is optional. This is a sample-data design prototype; its illustrative scores and relationships are not live assessment results.

The earlier comparison previews on5208 and5209 were application UI baselines and did not identify this design. Their repository recovery notes are superseded for this request. The original prototype was outside the application source tree; today's UI-W3 application edits did not overwrite its files. No application rollback was applied in this recovery.

Verification executed: all four preserved files match their originals byte-for-byte; the localhost response matches the complete standalone file; server syntax passed; POST, PUT, PATCH and DELETE return405. Browser checks passed Overview → priority risks → finding overlay → Relationships → selected-object evidence → Close → Overview. The backdrop's computed filter is `none`. Screenshots: `overview.jpg` and `relationships-overlay.jpg`. The prototype tab is retained as a deliverable.

The server runs detached and only binds localhost:5210. The PID and log are stored beside this document. Restart if stopped or after a computer restart:

```sh
/private/tmp/node-v24.21.0-darwin-arm64/bin/node work/ui-review/2026-10-03/recovered-graphical-prototype/preview.mjs
```

This recovery preserves the original visual and interaction design without changing it. No new product behavior, permissions, dependencies, migrations, production release or pilot gate change is included.
