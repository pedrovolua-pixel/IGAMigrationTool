# Synthetic proposal fixture unit host

This package-free executable checks only the cycle08 internal synthetic output contract under FR-HAS-31, AC-HAS-8, IP-HAS-007 and TP-HAS-008. Fixed fictional fixture values are test data; the packet's internal constructor is reached through **test-only reflection** to isolate output validation from the separately authored packet builder. No production constructor, permission, provider, resolver, run integration or activation is added.

`golden-snapshot.json` contains independently authored literal ASCII snapshot bytes without a newline. `golden-digest.txt` binds those exact bytes. `golden-oracle.py` constructs its own literal representation and computes its own SHA256; it imports no module helpers. The executable also checks the literal bytes/digest, closed root/proposal/statement shapes, all statement categories, exact packet/run/member citations, conflict requirements, array and text limits, duplicate/foreign/null values, hostile inert strings, canonical array ordering, detachment and explicit zero proposed results.

Run the pinned SDK's locked audited restore, focused formatting verification and Release build before executing the unit DLL. Execute the Python oracle independently. `execution.json` and `execution.log` record the sealed worker check results and source bindings without supplied output or packet payloads. The coordinator's integration suite uses the actual packet builder and independently reviews this module.

These tests do not verify real authorization/redaction, production budgets/retries/retention, model quality, provider availability, live injection behavior, rendering, scoring, review or publication. Full AI acceptance remains open.
