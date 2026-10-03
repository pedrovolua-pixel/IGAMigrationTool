# Independent Phase1B evaluation source checks

Scope: frozen cycle05 contract `8b70c02bccf4ea40a4c0ba576caad41a62972e35`, EV05-S001–S014. Expected.cs literal counts, identities, severity and root-group SHA256 values were committed before consuming the new module implementation. Root-group literals were calculated independently with Python from the unchanged source formula, not adapter output.

This executable creates a fresh exclusively owned fictional database. Set `IGA_EVALUATION_SOURCE_INDEPENDENT_DATABASE` to a loopback connection at127.0.0.1/localhost:55433, useriga_synthetic, database prefix `iga_synthetic_phase1b_evalsource_independent_`. The database must not already exist. No existing database, cluster, role or shared settings are changed. Fixture setup calls existing public owning modules and the pure fake provider outside transactions; capture itself never calls a provider or seeds reviews.

Run Release with .NET10.0.401. The optional `EvaluationSourceProject` MSBuild property points to the separately authored module during review; its default relative path is for integrated source. Checks compare owning source values, literal independent expectations, independently reconstructed canonical bytes/hash, complete row snapshots and real PostgreSQL lock waits. The ordinary supplied transaction is required because owning AI verification takes FOR UPDATE locks; no-row-change proves nonmutation. No HTTP/UI/accessibility or live acceptance claim is made.

Native executions, failures, source/binary hashes and independently reviewed results are coordinator-preserved evidence. Full M08/Phase1C, two real independent environments, actual qualified reviewer/evidence authority, source-backed sampling/reviews, delivery and production remain unverified.
