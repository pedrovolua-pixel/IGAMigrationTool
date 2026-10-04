# Scripted source-page kernel

This internal net10.0 library implements the reviewed finite [source-page contract](../../../specs/001-data-ingestion/collector-source-page-v1-contract.md) using required named trusted ports. It performs no SQL connection, source reads, source writes, file/checkpoint persistence, baseline assembly or collector activation. It has no SqlClient dependency. ScriptDom comes from the existing pinned CollectorSafety reference.

The kernel handles one immutable query pair/page per call, opens a fresh connection generation, binds effective permissions to that exact active connection through the adapter registry, and validates schema, native payload bounds, minimization receipts, native-order receipts and final authority/impact/deadline/retention. It admits protected outputs only after successful disposal. Every denial/error discards typed values. Its receipt is an in-memory admission receipt; it does not authenticate persisted content or satisfy G2.

Production composition must supply reviewed source/customer authority, immutable signed artifact/policy mappings, effective SQL permission probes on the actual transport connection, durable warning receipts, native ordering/UID semantics, classification/content detectors and impact evidence. No permissive production adapters are shipped here. Physical provider and typed storage integration remain SP18/SP19. Historical CollectorHost/Safety APIs and disabled host activation are unchanged.

The own assertion executable lives at `tests/unit/CollectorSourcePages.Tests`. The coordinator owns solution/CI inclusion and canonical release records. No migration or configuration/activation change is needed for this unactivated library.
