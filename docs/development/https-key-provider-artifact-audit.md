# HTTPS-LP02: Microsoft shared-key provider artifact audit

Status: **EVIDENCE RECORDED — candidate artifacts; dependency integration and production readiness blocked**
Date: 2026-10-03 UTC
Baseline: `d8155ee899fa17db9e155a7c97048a69f4e7a552`
Scope: [approved local cycle LP02](../../plans/active/https-production-local-cycle01.md), [key contract](https-production-key-contract-proposal.md), accepted local PC-D02. Owner approval covers the preferred Microsoft provider strategy and bounded tests; unresolved quantities, integrity authority, roles and lifecycle were not supplied by that approval.

## Result and compatibility limit

Stable candidates are **Azure.Extensions.AspNetCore.DataProtection.Blobs 1.5.4** and **Azure.Extensions.AspNetCore.DataProtection.Keys 1.6.4**. NuGet flat-container version indexes list these as latest non-prerelease versions at retrieval; registration marks both listed, published respectively `2026-08-18T03:24:28.703Z` and `2026-08-18T03:23:52.613Z`. Both archives include `net10.0`, `net8.0` and `netstandard2.0` assemblies. Their `net10.0` dependency groups require Azure.Core>=1.61.0, respectively Azure.Storage.Blobs>=12.26.0 / Azure.Security.KeyVault.Keys>=4.10.0, and Microsoft.AspNetCore.DataProtection>=10.0.10. [Blob package](https://www.nuget.org/packages/Azure.Extensions.AspNetCore.DataProtection.Blobs/1.5.4), [Keys package](https://www.nuget.org/packages/Azure.Extensions.AspNetCore.DataProtection.Keys/1.6.4).

Repository SDK10.0.401 and installed Microsoft.NETCore.App/Microsoft.AspNetCore.App10.0.12 were confirmed by `dotnet --list-runtimes`. Candidate metadata closure uses DataProtection10.0.12 to align with that runtime. **No NuGet restore, dependency installation, package reference, lockfile or code change was made.** Selecting a compatible TFM from metadata is not successful compilation, binary dispatch, transport, concurrency or deployed compatibility proof.

The existing BffFoundation lock has Azure.Core1.50.0, Azure.Identity1.17.2 and System.ClientModel1.8.0. These provider minima would change the resolved host graph, including MSAL/client-model dependencies. The archive graph below excludes the rest of the host graph and Azure.Identity; it is not its future production lock and does not approve upgrades. Full locked restore/audit, graph conflict/framework asset review and required actual SDK adapter tests remain prerequisites.

## Direct artifact and immutable source receipt

Both provider `.nuspec` repository records and verified GitHub release-tag references point to **`c88fa3e69af1cec1bcdf3302338fcd642ac0baa5`** in Microsoft's [Azure SDK repository](https://github.com/Azure/azure-sdk-for-net/tree/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5). Tags observed: `Azure.Extensions.AspNetCore.DataProtection.Blobs_1.5.4` and `Azure.Extensions.AspNetCore.DataProtection.Keys_1.6.4`; use the commit, not a mutable tag, as the source binding. This verifies published metadata/tag correspondence; no reproducible binary-to-source rebuild or SourceLink verification was performed.

| Archive | Raw file SHA256 | Raw SHA512 / official NuGet catalog match |
|---|---|---|
| Blobs1.5.4,82940 bytes | `6c39822eddc5575a56d9c3c58b27edddaed3434a39aabe7d3d39c1e65608efc1` | First row of inventory; exact match to [Blob catalog](https://api.nuget.org/v3/catalog0/data/2026.08.18.03.28.16/azure.extensions.aspnetcore.dataprotection.blobs.1.5.4.json) |
| Keys1.6.4,90014 bytes | `c832e18aad5d074e9bb3c711dc4a59c77c1ba20cc7e80cc9c28865bba375122a` | Second row; exact match to [Keys catalog](https://api.nuget.org/v3/catalog0/data/2026.08.18.03.26.54/azure.extensions.aspnetcore.dataprotection.keys.1.6.4.json) |

The archive SHA512 below hashes downloaded `.nupkg` bytes. It must not be confused with `dotnet nuget verify`'s displayed signature-content hash: Blobs=`5D9McHLGA7riU1z2Mtpt3nPq9rAcsc11hIm3Cb/In+wB2w+Osyvd03EjAS08c3UqiNIFOsAXCo37srPT6NH8uA==`; Keys=`7uKgk1WzELt5Pbp09liae8HBL3XOe0x/+DGYYC5DCiCKGRxrGi0BfmVCqzvzXGMOxITL4THEYf1GklzmAuJRTQ==`. No generated lockfile content hash is claimed.

## Candidate minimum dependency archive inventory

A read-only Python manifest traversal selected the closest listed compatible group (net10 then lower net/standard), recursively took maximum declared stable lower bounds, and explicitly aligned DataProtection10.0.12. It downloaded **32** final candidate archives and read their manifests; it is not NuGet's resolver and does not model the existing host's direct/transitive precedence or framework asset suppression. Ranges/edges are retained below as metadata, not a production lock. All packages declare MIT; four older manifests lack an SPDX field but contain MIT `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`. Distribution notice review is still required.

Archives are reproducibly addressable using the official [NuGet flat-container API](https://api.nuget.org/v3-flatcontainer/azure.extensions.aspnetcore.dataprotection.blobs/index.json): lowercase ID/version in `https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.{version}.nupkg`. Provider/version indexes, registration/catalog JSON, manifests and bytes were inspected, with temporary files only under `/tmp/iga-key-artifact-readonly`; their continued availability is not assumed.

| Package | Exact candidate version | Inspected dependency group | Raw archive SHA512 (base64) |
|---|---|---|---|
| Azure.Extensions.AspNetCore.DataProtection.Blobs | 1.5.4 | net10.0 | `JDvpzefQ9VgGoG5RhOQIqWMatA3bL5pGOS31Zw4Ud0GR1iZusn9oEh22EM5U5a/ECSRGbZcqM9DRoS1syrq7uQ==` |
| Azure.Extensions.AspNetCore.DataProtection.Keys | 1.6.4 | net10.0 | `kFNPVVGxXqcXkZpCOu2gDZ23haxDXi4BeDxDje22BbMMXg+o8VWDn30K6pUbs64m+7tRsLIllSqFFlYMdF8xhg==` |
| Microsoft.AspNetCore.DataProtection | 10.0.12 | net10.0 | `rReNxlDcD8C5+NAJxRdopm+q9EtLhMYqQ6Yft6HYx+4oQO3hhbCWlIVGhG7gI/3L6lMvf56LGDhSTDp32eRIfQ==` |
| Azure.Core | 1.61.0 | net10.0 | `da59t4E320Tx6ZcpllO9tMwjBb6A1E7IYHH4TS+IaAhGg3vdq6PhgHKQuuxZtJwIDa9stupo3MSTDj6HpdIFfg==` |
| Azure.Storage.Blobs | 12.26.0 | net8.0 | `Bg0+A1pI0ZMCykKHYZWz7sdmtWArJ0qOZzxvxNNbrT+Dkj6J3jXZiNwatXDoIAJltBuEEIZ5gU3zZ4Oz8tTZTw==` |
| Azure.Security.KeyVault.Keys | 4.10.0 | net10.0 | `dGzRy2VWlbud2a1+lz9ldkE/G0N9i0tuonJNZgu/CE4LtkEdD3slq4EEHgeqic3nMCic7cpAlcOd02hE0u+LdQ==` |
| Microsoft.AspNetCore.DataProtection.Abstractions | 10.0.12 | net10.0 | `oX467LwpyZxPVhtqHhDnZ9/U7tZ4yLAfJ+VBW5NccKbR/h2a8kAVn+AvzYMxPJtb3RQKkULpJWOh0AGL4Ei95A==` |
| Microsoft.AspNetCore.Cryptography.Internal | 10.0.12 | net10.0 | `FFf+3D4u3YlJpnAPkqX+GU0UlE2k7JEUis2a2oSof5VTxFILFM5CZ6rOHTADzFEHAkPi6xE65Bqb8D22UgY5uA==` |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.12 | net10.0 | `kTf3x6w7DOuNj6kS0kL8oEGSSXJnqiPm3G+ddUfVB743EaO5Np8CX3r9pQp0ovBY7/J+KY0JLbwuhC02nFQO1g==` |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.12 | net10.0 | `F31e9IMQm70ywZhUsXWzAx5x51989XK6CJ9FxPK5MpD/nxRzu6qAt8MT0ZfV7wl+38v60dx2GZny6cs0LJc+OQ==` |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | net10.0 | `6U8g6MRgR1JhFCr4XnlPJIjgKh9Fhh30wwUnuFvnaeJqpgx6c1J4BA5RnPNaMm7vE1zN/QOR+nTSTNswgInX2w==` |
| Microsoft.Extensions.Options | 10.0.12 | net10.0 | `AVPXba8q1GXeRiVr4SVu4YX0iBU+Tn59ObvD2uWkkWOwKoVPZ+HQMpen7TCiA9xpwHGMNqt381z1iWA6nokngQ==` |
| System.Security.Cryptography.Xml | 10.0.12 | net10.0 | `8FU+Ca3HyQ0vbpBgXv9G0cfvMXqZgo6L6754gpHQMP/N4+Yd8yzRRGhfyujozThG4msSwBP5BQJsj8ywn0IGQA==` |
| Microsoft.Bcl.AsyncInterfaces | 10.0.9 | .NETStandard2.1 | `Sl4UmlXUwmjkKRakppsT2vYzgVdlJbmJFWCne1k7d0xRpfXoj+SkYAxDVnXm+AmZMDP6NZZtCxN7YYN9N7KMhw==` |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.12 | net10.0 | `TZ4PJHUuUpM8ck2v6La2r23Hbvs5BAuWZKKn7/S10SBpHAAYXiRrfoBuOF3MCUH4ds7Tys7uScFCwK30a8crZg==` |
| Microsoft.Identity.Client | 4.84.2 | net8.0 | `EauAN5fyTYltbQ/2MyZwG6MYpCJMeHvX2W/nFcjhkbFhKmGtjsUv2OdvYmXs+A0CdAVBx9GD7LVLTv3mkd4ALQ==` |
| Microsoft.Identity.Client.Extensions.Msal | 4.84.2 | net8.0 | `YJX7cgwP6C4E5Oq2MwfTvIXDDuyGet9TLtqdjpWBcTuMUlA6mKu6W98VziTG6ultA6RbKY3FxOGrirJf3Ycg+w==` |
| System.ClientModel | 1.15.0 | net10.0 | `XbkmhYnoF2ZfePaJU2LdRsBaBpG52gw8B8fvSPiV1YTPlAAb+WAKl4/UYOYMf765dRKY8C9eqyXZX90rV+85vg==` |
| System.Memory.Data | 10.0.9 | net10.0 | `3INOktOWda5HpHOWLFv8mrLeqXhZZQXANXC/KIpwO1jsoVnmEIXOCGCGr1VScQlhZ7EUTVWvYGe9OIetZA/R8A==` |
| Azure.Storage.Common | 12.25.0 | net8.0 | `o1+VcIQeB4drt9Y2u9WJwDQw4ac2lPtqvfO+Wu5tH8M9u0Gn+kHKUInDaOUtqLcWZX8b0UJmcXplkU38Znj1fg==` |
| Microsoft.Extensions.Diagnostics.Abstractions | 10.0.12 | net10.0 | `8i5OD/g0Xj8Q1TizgLzs6h/M7sF+CUT2JzgNuUnFBf5qHF0/jk84HCnk4mpG+t0QyFKdRB4aJ+/mLEpLhJ5sVg==` |
| Microsoft.Extensions.FileProviders.Abstractions | 10.0.12 | net10.0 | `uvXk5vVWju5qjSCOQCiuXi0bDoy7kI9Ds1XRpIPl0PTpgWA+sAanC6j3fR2H6lw9KHHdwdgp4oCUvN417a4Saw==` |
| Microsoft.Extensions.Primitives | 10.0.12 | net10.0 | `G4hMZ9wJv75ufnnBkHAHbqiywfMGfeyfz/aiBZoLeg5vVgEqZPPAT/nLFb/Pk+/mGCAIRi3i0WfGHTcVDN9lCg==` |
| System.Security.Cryptography.Pkcs | 10.0.12 | net10.0 | `sltiX/QUYgLYAR8EqUGoW+vkteD4916Uy9DZxXXMG0EirRprYTieYug8Y+BQTKgPXQIVvkW7p3LWiNwPG1oJuw==` |
| Microsoft.IdentityModel.Abstractions | 8.14.0 | net9.0 | `F174v3i2PzwyfmgNXPdyHXTynpZGDmhrIrTmfCZPsDanqb6hRz9KW0hzN6t6VgyGG1HtGqdEd38wM2ImKwGotA==` |
| System.Diagnostics.DiagnosticSource | 6.0.1 | net6.0 | `gKD5vzp6/bKNnwDh8wH+6ss5w0/krI9Vo5I3fi4Bj7VG/D/Fbi/kM23qIit6s/S6tYoLjYbrGMcZUe8uHHUniQ==` |
| System.Security.Cryptography.ProtectedData | 4.5.0 | .NETStandard2.0 | `s/MFK8BvgtejZj5a8qAPDXEGBtWkpFX98qwQP/wqVa9CgIC7LrKHxDFdlN5x6afGAb0ZVBVGIZlCdBLnO2QjZw==` |
| System.IO.Hashing | 8.0.0 | net8.0 | `hCsd/0sdy0hGifJEqTsfZfqU2nCC0Rc2plCA5mk1PN1qlcoYIUuaeclLY1/ozsEwV7Db8Na41npKearKXHU07A==` |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | net6.0 | `1AVzAb5OxJNvJLnOADtexNmWgattm2XVOT3TjQTN7Dd4SqoSwai1CsN2fth42uQldJSQdz/sAec0+TzxBFgisw==` |
| System.Memory | 4.5.0 | .NETStandard2.0 | `ULODiqdq81e9YfQS/GL4P5zgek90jyPykAV4t1Sp4sjID3/Ebi18lbvOKm+81biHR5a5FdFRXSU/JVc6nxr/7A==` |
| System.Buffers | 4.4.0 | .NETStandard2.0 | `Ii2bedd4HVzddupdU35n3ygohUPlNn7MDimBOYcwWNce2NizQ1fCSaQJY1Tzv80aMqOGpVcU4wZr/Xe50xcTwg==` |
| System.Numerics.Vectors | 4.4.0 | .NETStandard2.0 | `gdRrUJs1RrjW3JB5p82hYjA67xoeFLvh0SdSIWjTiN8qExlbFt/RtXwVYNc5BukJ/f9OKzQQS6gakzbJeHTqHg==` |

### Repository metadata and direct dependency edges

The following compact records preserve each manifest's selected group's exact dependency strings and declared Git repository/commit. `none` means absent manifest provenance; it is a proof gap, not an inferred source commit. A=Microsoft [Azure SDK](https://github.com/Azure/azure-sdk-for-net), D=[dotnet/dotnet](https://github.com/dotnet/dotnet), R=[dotnet/runtime](https://github.com/dotnet/runtime), M=[Microsoft MSAL](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet), I=[Microsoft IdentityModel](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet). Source inspection below covers only the provider and actual guard-call dependencies.

| Package | Declared repository/commit | Selected dependency minima/ranges |
|---|---|---|
| Azure.Extensions.AspNetCore.DataProtection.Blobs | A:`c88fa3e69af1cec1bcdf3302338fcd642ac0baa5` | Azure.Core=1.61.0; Azure.Storage.Blobs=12.26.0; Microsoft.AspNetCore.DataProtection=10.0.10 |
| Azure.Extensions.AspNetCore.DataProtection.Keys | A:`c88fa3e69af1cec1bcdf3302338fcd642ac0baa5` | Azure.Core=1.61.0; Azure.Security.KeyVault.Keys=4.10.0; Microsoft.AspNetCore.DataProtection=10.0.10 |
| Microsoft.AspNetCore.DataProtection | D:`95017c711e6afc1085133d440e42b4bd78155701` | Microsoft.AspNetCore.DataProtection.Abstractions=10.0.12; Microsoft.AspNetCore.Cryptography.Internal=10.0.12; Microsoft.Extensions.DependencyInjection.Abstractions=10.0.12; Microsoft.Extensions.Hosting.Abstractions=10.0.12; Microsoft.Extensions.Logging.Abstractions=10.0.12; Microsoft.Extensions.Options=10.0.12; System.Security.Cryptography.Xml=10.0.12 |
| Azure.Core | A:`3830815f87881cce7af68dd9dd4126cbd90e197b` | Microsoft.Bcl.AsyncInterfaces=10.0.9; Microsoft.Extensions.Configuration.Abstractions=10.0.9; Microsoft.Extensions.Hosting.Abstractions=10.0.9; Microsoft.Identity.Client=4.84.2; Microsoft.Identity.Client.Extensions.Msal=4.84.2; System.ClientModel=1.15.0; System.Memory.Data=10.0.9 |
| Azure.Storage.Blobs | A:`d20724f6dafe371d302a891661716383c074b3a5` | Azure.Storage.Common=12.25.0; Azure.Core=1.47.3 |
| Azure.Security.KeyVault.Keys | A:`6662fe91f2d1f7230d7a8fed2b6db2a17a0bd692` | Azure.Core=1.54.0 |
| Microsoft.AspNetCore.DataProtection.Abstractions | D:`95017c711e6afc1085133d440e42b4bd78155701` | none |
| Microsoft.AspNetCore.Cryptography.Internal | D:`95017c711e6afc1085133d440e42b4bd78155701` | none |
| Microsoft.Extensions.DependencyInjection.Abstractions | D:`95017c711e6afc1085133d440e42b4bd78155701` | none |
| Microsoft.Extensions.Hosting.Abstractions | D:`95017c711e6afc1085133d440e42b4bd78155701` | Microsoft.Extensions.Configuration.Abstractions=10.0.12; Microsoft.Extensions.DependencyInjection.Abstractions=10.0.12; Microsoft.Extensions.Diagnostics.Abstractions=10.0.12; Microsoft.Extensions.FileProviders.Abstractions=10.0.12; Microsoft.Extensions.Logging.Abstractions=10.0.12 |
| Microsoft.Extensions.Logging.Abstractions | D:`95017c711e6afc1085133d440e42b4bd78155701` | Microsoft.Extensions.DependencyInjection.Abstractions=10.0.12 |
| Microsoft.Extensions.Options | D:`95017c711e6afc1085133d440e42b4bd78155701` | Microsoft.Extensions.DependencyInjection.Abstractions=10.0.12; Microsoft.Extensions.Primitives=10.0.12 |
| System.Security.Cryptography.Xml | D:`95017c711e6afc1085133d440e42b4bd78155701` | System.Security.Cryptography.Pkcs=10.0.12 |
| Microsoft.Bcl.AsyncInterfaces | D:`901ca941248413c79832d2fdbd709da0c4386353` | none |
| Microsoft.Extensions.Configuration.Abstractions | D:`95017c711e6afc1085133d440e42b4bd78155701` | Microsoft.Extensions.Primitives=10.0.12 |
| Microsoft.Identity.Client | M:`bad7cae331c1d9e699ca2150a5fecb108ea54916` | Microsoft.IdentityModel.Abstractions=8.14.0; System.Diagnostics.DiagnosticSource=6.0.1 |
| Microsoft.Identity.Client.Extensions.Msal | M:`bad7cae331c1d9e699ca2150a5fecb108ea54916` | Microsoft.Identity.Client=4.84.2; System.Security.Cryptography.ProtectedData=4.5.0 |
| System.ClientModel | A:`6366473d5584b140bda80ddd1120c2f67e5d2658` | Microsoft.Extensions.Configuration.Abstractions=10.0.9; Microsoft.Extensions.Hosting.Abstractions=10.0.9; Microsoft.Extensions.Logging.Abstractions=10.0.9; System.Memory.Data=10.0.9 |
| System.Memory.Data | D:`901ca941248413c79832d2fdbd709da0c4386353` | none |
| Azure.Storage.Common | A:`d20724f6dafe371d302a891661716383c074b3a5` | Azure.Core=1.47.3; System.IO.Hashing=8.0.0 |
| Microsoft.Extensions.Diagnostics.Abstractions | D:`95017c711e6afc1085133d440e42b4bd78155701` | Microsoft.Extensions.DependencyInjection.Abstractions=10.0.12; Microsoft.Extensions.Options=10.0.12 |
| Microsoft.Extensions.FileProviders.Abstractions | D:`95017c711e6afc1085133d440e42b4bd78155701` | Microsoft.Extensions.Primitives=10.0.12 |
| Microsoft.Extensions.Primitives | D:`95017c711e6afc1085133d440e42b4bd78155701` | none |
| System.Security.Cryptography.Pkcs | D:`95017c711e6afc1085133d440e42b4bd78155701` | none |
| Microsoft.IdentityModel.Abstractions | I:`c8f7d87bcda35557a68f6cb9c55856a2ee733856` | none |
| System.Diagnostics.DiagnosticSource | R:`5edef4b20babd4c3ddac7460e536f86fd0f2d724` | System.Runtime.CompilerServices.Unsafe=6.0.0 |
| System.Security.Cryptography.ProtectedData | none | System.Memory=4.5.0 |
| System.IO.Hashing | R:`5535e31a712343a63f5d7d796cd874e563e5ac14` | none |
| System.Runtime.CompilerServices.Unsafe | R:`4822e3c3aa77eb82b2fb33c9321f923cf11ddde6` | none |
| System.Memory | none | System.Buffers=4.4.0; System.Numerics.Vectors=4.4.0; System.Runtime.CompilerServices.Unsafe=4.5.0 |
| System.Buffers | none | none |
| System.Numerics.Vectors | none | none |

## Signatures, license and vulnerability evidence

Executed SDK10.0.401 `dotnet nuget verify /tmp/iga-key-artifact-readonly/*.nupkg --all --verbosity normal` on downloaded archives using normal macOS certificate services. Output records **27 successfully verified**, including both provider archives, and **5 failures**. The original shell display wrapper returned0; it did not preserve the verifier's aggregate exit, so no aggregate command success is claimed. A focused normal-macOS repeat on System.Memory4.5.0 returned **exit1**, with NU3037 expiry and NU3028 timestamp-chain `ExplicitDistrust`. A sandbox-only repeat returned exit1/NU3003 platform verification failure; it is not artifact integrity evidence.

| Failed candidate archive | Recorded normal-macOS verifier failure |
|---|---|
| System.Buffers4.4.0; System.Numerics.Vectors4.4.0 | Repository signature expiry plus distrusted timestamp chain |
| System.Memory4.5.0; System.Security.Cryptography.ProtectedData4.5.0; System.Runtime.CompilerServices.Unsafe6.0.0 | Author/repository signature expiry plus distrusted timestamp chain |

This is a **verification blocker**, not demonstrated package tampering or a new vulnerability finding. No trust-store change, signature bypass, automatic dependency upgrade or inferred production exception was used. The future actually resolved graph needs approved-platform signature verification; minimum-graph packages may be superseded/unselected by real host resolution, which was not executed here.

Read and combined the official [NuGet vulnerability index](https://api.nuget.org/v3/vulnerabilities/index.json) pages: [base](https://api.nuget.org/v3-vulnerabilities/2026.09.26.05.43.06/vulnerability.base.json), updated `2026-09-26T05:43:06.7863496Z`, and [update](https://api.nuget.org/v3-vulnerabilities/2026.09.26.05.43.06/2026.10.02.23.43.48/vulnerability.update.json), updated `2026-10-02T23:43:48.5527772Z`. Checked each candidate ID/version against all reported inclusive/exclusive ranges: **zero matching advisories**. The feed contains historical DataProtection, Blob, MSAL, Crypto.Xml and Pkcs advisories, outside these candidate versions. This is dated feed evidence, not “no vulnerabilities” or the required full host NuGetAudit. [Microsoft API semantics](https://learn.microsoft.com/en-us/nuget/api/vulnerability-info).

## Immutable guard feasibility: source evidence only

| Inspected binding | Evidence and boundary |
|---|---|
| Provider Blob repository | [Immutable source](https://github.com/Azure/azure-sdk-for-net/blob/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Blobs/src/AzureBlobXmlRepository.cs): internal sealed repository; synchronous DownloadTo and Upload; cached ETag/304;404 and zero-length become empty XML; conditional merge retries409/412 up to five attempts. SDK network retries are separate. |
| Blob injection | [Extension source](https://github.com/Azure/azure-sdk-for-net/blob/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Blobs/src/AzureStorageBlobDataProtectionBuilderExtensions.cs): public BlobClient and factory overloads attach the supplied client. Internal repository is not a public wrapper API. |
| Actual read dispatch | [BlobBaseClient12.26 source](https://github.com/Azure/azure-sdk-for-net/blob/d20724f6dafe371d302a891661716383c074b3a5/sdk/storage/Azure.Storage.Blobs/src/BlobBaseClient.cs): called synchronous `DownloadTo(Stream, BlobRequestConditions, StorageTransferOptions, CancellationToken)` is public virtual, marked `EditorBrowsable(Never)`. Overriding only async or modern-options overload does not intercept this call. |
| Actual write dispatch | [BlobClient12.26 source](https://github.com/Azure/azure-sdk-for-net/blob/d20724f6dafe371d302a891661716383c074b3a5/sdk/storage/Azure.Storage.Blobs/src/BlobClient.cs): called public virtual legacy Upload stream/header/metadata/conditions/progress/tier/transfer/token overload is also hidden from IntelliSense, delegates to staged upload. A generic “Upload override” is insufficient evidence. |
| Resolver injection | [Keys extension](https://github.com/Azure/azure-sdk-for-net/blob/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Keys/src/AzureDataProtectionKeyVaultKeyBuilderExtensions.cs): public IKeyEncryptionKeyResolver/factory overloads; supplied resolver is used for encrypted XML and decryptor activation. |
| Wrap/unwrap identity | [Encryptor](https://github.com/Azure/azure-sdk-for-net/blob/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Keys/src/AzureKeyVaultXmlEncryptor.cs) resolves versionless configuration, records returned concrete KeyId and wraps with RSA-OAEP; [decryptor](https://github.com/Azure/azure-sdk-for-net/blob/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Keys/src/AzureKeyVaultXmlDecryptor.cs) resolves the stored kid. Historical unwrap is not newest-version substitution. |
| Resolver caveats | [KeyResolver4.10 source](https://github.com/Azure/azure-sdk-for-net/blob/6662fe91f2d1f7230d7a8fed2b6db2a17a0bd692/sdk/keyvault/Azure.Security.KeyVault.Keys/src/Cryptography/KeyResolver.cs): metadata GET403 can yield a force-remote client; diagnostics attach `az.keyvault.key.id`. Neither fallback nor diagnostic identifier export is allowed by the proposed guard contract. |

**Inference:** a narrow injected BlobClient subclass and resolver decorator appear feasible on these exact public virtual/interface APIs, without replacing Microsoft's repository/crypto. Hidden legacy overloads create upgrade sensitivity. No package-based compilation/reflection/dispatch harness or binary disassembly was executed; compatibility/support across upgrades is not inferred from public visibility alone. This is source-supported feasibility, **not completed SDK interception proof**.

Required next SDK tests must demonstrate these mechanics:

- Existing mode converts404 into a closed non-RequestFailedException failure before the repository can swallow it, stages successful downloads for bounded XML/inventory validation before copying to the caller stream, and admits304 only against a previously validated matching inventory/ETag. Empty/invalid content and lost validated cache deny. Chosen limits remain unresolved.
- Existing-mode Upload requires concrete non-wildcard IfMatch and rejects IfNoneMatch/unconditional creation before network I/O; bootstrap remains a separately reviewed capability. Preserve conditions/content through staged SDK upload. Conflict rereads must pass the same read guard. Deletion between read/update fails CAS and cannot turn into create on retry.
- Local subclass guards do not individually see every SDK range request/network retry. Test actual SDK transport, staged read/write, retry, conflict, unknown acknowledgment and concurrent deletion; inspect terminal content, not only headers. No CAS claim detects privileged deletion/recreation with an older valid inventory.
- Resolver decorator validates exact configured vault/key: versionless only for new wrapping; concrete same-path version for historical unwrap and resolved KeyId; reject foreign IDs, absent metadata or versionless fallback without another credential/destination. To deny read403 before force-remote use, supply tested guarded metadata transport/client behavior as well as resolver result validation. A decorator alone has no status proof and is insufficient.
- SDK-generated Activities/errors/HTTP diagnostics need tested suppression/redaction before export. No plaintext, XML, wrapping bytes, tokens, cookies or actual protected IDs may enter ordinary logs/status. Application-only logging tests are insufficient.

## Evidence digests and remaining prerequisites

SHA256 receipts bind the temporary audit outputs; the package URLs/hashes and immutable source URLs above permit reread after temporary files disappear. Temporary tools/archives were not committed.

| Evidence | SHA256 |
|---|---|
| candidate-closure.json | `6f97c34fa2d727277fc732e89e8d97a876838e4ba112e0930fa3cb50712fdac0` |
| signature-verification.txt | `00cf1337e2cc349797a6df966e27bf1009c872ae9a823227630adf51aea0a881` |
| vulnerabilities-index.json | `ab489a2e0f44e13aeb239510e49be10224e9349b8c30781cb4a17254167b50f3` |
| vulnerabilities-base.json | `c65988e60203108ee85b44b5c3a4d2f5efc78e0f56cf508bdc6b02c991a7c098` |
| vulnerabilities-update.json | `44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a` |

No SDK artifact dependency is installed or approved for production by this record. Unresolved: actual host resolution/locks and security audit; the five signature failures and four absent-source metadata records; compiled guard overload dispatch and all KEY-001–018 required evidence; trusted monotonic inventory/witness persistence and authority; bootstrap/rotation/lifetime/deletion/backup policy; network/operation deadlines/readiness freshness; exact managed identity/actions/scopes/private DNS; durable audit outage preservation and production-host/resource/CA/ingress closure. Source/tag/license checks close none of those live gates.

Document-only completion checks: UTF-8, local links, manifest inventory consistency, secret scan and git whitespace are recorded by the worker handoff. Runtime build/tests, package restore, lock generation, Azure/tenant/key calls and grants were not executed. Coordinator owns canonical plan/status/private-board updates and independent review. No release or paid session follows.
