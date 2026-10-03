# HTTPS-AP02: actual shared-key provider host graph evidence

Status: **LOCAL RESTORE EVIDENCE COMPLETE; INTEGRITY AND SDK DISPATCH BLOCKED**
Date: 2026-10-03 UTC; execution began `2026-10-03 14:18:05 UTC`
Tracked source: `b186d121419236e15e7fa184a22c6bac4d2db939`
Authority: [approved cycle02 AP02/AP-T04](../../plans/active/https-production-local-cycle02.md), preferred local PC-D02, [previous minimum artifact audit](https-key-provider-artifact-audit.md). This closes the actual graph observation only. It installs no dependency/configuration/lock/code into the repository, creates no Azure resource/grant, and approves no production package graph.

## Executed outcome

An immutable tracked-source archive was extracted into `/tmp/iga-key-resolved-copy`. Only that disposable copy's BffFoundation project received exact package references `[1.5.4]` for `Azure.Extensions.AspNetCore.DataProtection.Blobs` and `[1.6.4]` for `.Keys`. Original Microsoft.Identity.Web `[4.15.0]`, project references, framework and all other source were retained. Pinned SDK10.0.401 / Microsoft.NETCore.App and Microsoft.AspNetCore.App10.0.12 were used.

**Actual audited restore exited0**, with no reported warning/error and four restored projects. The BffFoundation `net10.0` lock contains **37 selected packages plus3 project references**. Independent explicit normal macOS signature verification of those37 archives exited **1**: **36 verified; System.Security.Cryptography.ProtectedData4.5.0 failed**. A successful restore does not establish signature success.

No SDK harness, reflection/dispatch experiment, runtime/key test or actual Azure/credential call ran. Experimental build and code formatting were **NOT EXECUTED**, because the selected artifact's integrity remained unresolved; no binary compatibility or warning-free experimental build is claimed. The completed document receives whitespace/link/inventory/secret checks separately. Normal repository baseline tests are coordinator evidence and do not validate this experimental graph.

## Isolation and reproducible command receipt

`git archive --format=tar b186d121419236e15e7fa184a22c6bac4d2db939` supplied only tracked source, with no `.git`, ignored credentials or untracked files. Archive SHA256=`70799fb48cc67caa96251468cfb04dcb37915e4f143335a9db0c3ebc43992def`. Original BffFoundation.csproj SHA256=`b8b0536294800119d99d63e1603685dcf82ec1b6bedc63530a603936a7faaa6c`; disposable edited project SHA256=`816a9b1b72fec97ce841b03863fc1959c324947545b932f743fd09b551564df3`.

Caches/home were isolated: DOTNET_ROOT=`/tmp/iga-dotnet-10.0.401`, DOTNET_CLI_HOME=`/tmp/iga-key-resolved-cli-home`, NUGET_PACKAGES=`/tmp/iga-key-resolved-packages`, NUGET_HTTP_CACHE_PATH=`/tmp/iga-key-resolved-http-cache`; MSBUILDDISABLENODEREUSE=1, DOTNET_CLI_TELEMETRY_OPTOUT=1. A temporary external NuGet configuration cleared package/audit sources and selected only `https://api.nuget.org/v3/index.json`. No trust settings or signature requirement were weakened. Existing user package sources were not selected. All experimental package references, generated locks/assets, archives and logs remain under `/tmp` only.

Restore command, from the disposable copy (exact invocation retained in the hashed execution receipt):

```text
/tmp/iga-dotnet-10.0.401/dotnet restore src/server/hosts/BffFoundation/BffFoundation.csproj --force-evaluate --configfile /tmp/iga-key-resolved-evidence/nuget-public.config --verbosity minimal -m:1 -nr:false /p:UseSharedCompilation=false /p:NuGetAudit=true /p:NuGetAuditMode=all /p:NuGetAuditLevel=low
```

`--force-evaluate` intentionally generated an **experimental** lock; this was not a locked-mode restore of approved new dependencies. Existing source `TreatWarningsAsErrors` and NuGetAudit settings were retained. The first SDK invocation with isolated CLI home printed “Installed an ASP.NET Core HTTPS development certificate.” No `--trust`, trusted-root operation, cleanup/removal or certificate-based test was requested/executed; the incidental first-run message is retained and certificate state/trust was not independently inspected. Later verification set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1.

## Difference from minimum inventory and existing host

The earlier32-archive manifest walk was explicitly not NuGet resolution. Actual project.assets records `packagesToPrune` for framework-supplied DataProtection, Extensions, Memory, Unsafe and related libraries. It contains no package-pruning entry for ProtectedData. Thus four earlier failed minimum-graph archives are absent from the actual host; **ProtectedData4.5.0 remains selected**. DataProtection comes from the existing framework reference, not a new10.0.12 package pin. The SDK/framework's own artifact verification is not established by this package-only check.

| Comparison | Observed actual outcome |
|---|---|
| Earlier minimum graph DataProtection/Abstractions/Cryptography.Internal10.0.12, Extensions10.0.12, Crypto.Xml10.0.12 | Not selected as NuGet packages; framework-supplied pruning |
| Earlier DiagnosticSource6.0.1, Unsafe6.0.0, Memory4.5.0, Buffers4.4.0, Numerics.Vectors4.4.0 | Not selected as packages |
| Earlier MSAL4.84.2 / IdentityModel.Abstractions8.14.0 / Pkcs10.0.12 | Actual4.90.0 /8.22.0 /10.0.10 respectively |
| Compared with this source's original BFF lock | Added six packages: both providers, KeyVault.Keys4.10.0, Blobs12.26.0, Storage.Common12.25.0, IO.Hashing8.0.0 |
| Existing host packages changed by this experiment | Azure.Core1.50.0→1.61.0; Bcl.AsyncInterfaces8.0.0→10.0.9; MSAL.Extensions4.83.1→4.84.2; ClientModel1.8.0→1.15.0; Memory.Data8.0.1→10.0.9 |

Microsoft.Identity.Web.Certificateless/TokenCache already require MSAL4.90.0 in the current source graph; this experiment did not invent or directly upgrade it. Azure.Identity remains1.17.2. IdentityPolicy, IdentitySessions and IdentityAuthority are the three project references; their restored locks are separately hashed below. No production graph promotion follows from a satisfiable dependency resolution.

## Exact resolved package and archive inventory

`contentHash` is the **NuGet-generated lock value, distinct from raw archiveSHA256**. Raw SHA256 hashes each retained `.nupkg` file including signatures; no algorithm or normalization claim is made for the generated lock value. Provider source binding remains `c88fa3e69af1cec1bcdf3302338fcd642ac0baa5`; the earlier audit's immutable public source links cover both unchanged provider versions and their Blob12.26/KeyVault4.10 guard dependencies. Microsoft [Blob1.5.4](https://www.nuget.org/packages/Azure.Extensions.AspNetCore.DataProtection.Blobs/1.5.4) and [Keys1.6.4](https://www.nuget.org/packages/Azure.Extensions.AspNetCore.DataProtection.Keys/1.6.4) archives were selected exactly. Every selected archive and its nuspec repository/license/dependency metadata was read; all declare MIT or include the MIT license file. No binary/source reproducibility or distribution-notice approval is claimed.

| Package | Resolved version | Lock contentHash | Raw archive SHA256 |
|---|---|---|---|
| Azure.Extensions.AspNetCore.DataProtection.Blobs | 1.5.4 | `5D9McHLGA7riU1z2Mtpt3nPq9rAcsc11hIm3Cb/In+wB2w+Osyvd03EjAS08c3UqiNIFOsAXCo37srPT6NH8uA==` | `6c39822eddc5575a56d9c3c58b27edddaed3434a39aabe7d3d39c1e65608efc1` |
| Azure.Extensions.AspNetCore.DataProtection.Keys | 1.6.4 | `7uKgk1WzELt5Pbp09liae8HBL3XOe0x/+DGYYC5DCiCKGRxrGi0BfmVCqzvzXGMOxITL4THEYf1GklzmAuJRTQ==` | `c832e18aad5d074e9bb3c711dc4a59c77c1ba20cc7e80cc9c28865bba375122a` |
| Microsoft.Identity.Web | 4.15.0 | `hjpuga1sZMiqcL6qnqduWC0b+6nHkVfRAhjkQLP8s6J8UBbK8KaBtgffGjYxcWb4hUAZCJPZOIaCowcILwCUyQ==` | `606a5c31d694b30c2fb9e0ff8aa3dccbf96f12e9992007b13cc4a48311c871ac` |
| Azure.Core | 1.61.0 | `sns4J9zz7NCpfNYz9rMIly5/TjDkNsTnPAr4nNr0BUBzn6tn6RNOEHOKb7A7DhJj/dEFMSevp3Ye36cUiNmsfg==` | `9d0aef39084d4cbfcbe01358df6db6dfc047873ddb7207222554173b3fd6bd0b` |
| Azure.Identity | 1.17.2 | `WLI9tc1NwOzvncfSfut5w/qv4f4ZC+l4JK0XXNZCMjZn6MDAgytZsNAJBI3MJs+XcPGBikedYv/dLMXryLQBeg==` | `e51c0a7c9e0229242fcafdc74af668843d4fe1c69c7af63e717947ea628fe396` |
| Azure.Security.KeyVault.Certificates | 4.6.0 | `e2ATU/n2ZDL/S8A8EdrcfKEvKc2BojCrrSpmM+JKnrSTQS32x/W0Ldu8utk+epLKwXvSJRSWtlgdo7X8hG1mCg==` | `59ce4ac19a8155c8e88bfd2bce8482f4acee95ca5286a5eed3ff9f0965a8e7f5` |
| Azure.Security.KeyVault.Keys | 4.10.0 | `R5iW7mH0EXdl3wqLQ7qglFQXl5JPGmmYXxDxKFT/TraCw6yRhlt5/G3NCc0Wb98P2P3lRZEI8lqF3BY0m9RkNA==` | `c9c2b8a7017ea94fb10581bb0b57dc0656c99d922e189aa9e5a15a04c892da0a` |
| Azure.Security.KeyVault.Secrets | 4.6.0 | `vwPceoznuT6glvirZcXlaCQrh1uzTSxpZUi2hRFNumHiS3hVyqIXI5fgWiLtlBzwqPJMTr0flUoSvGKjXXQlfg==` | `ef68b39294bf2879ee8a8b8bf426b86aeb9e07fd3ae9197fee9e0bc97a409134` |
| Azure.Storage.Blobs | 12.26.0 | `EBRSHmI0eNzdufcIS1Rf7Ez9M8V1Jl7pMV4UWDERDMCv513KtAVsgz2ez2FQP9Qnwg7uEQrP+Uc7vBtumlr7sQ==` | `27aefbe1613ac65aec9a192613575aa64ae12ba7450726314872ecd4e40f938f` |
| Azure.Storage.Common | 12.25.0 | `MHGWp4aLHRo0BdLj25U2qYdYK//Zz21k4bs3SVyNQEmJbBl3qZ8GuOmTSXJ+Zad93HnFXfvD8kyMr0gjA8Ftpw==` | `e1e32fe283ae98ee85171d1532cb88afab16b68baee1fe806a379b4d08e18fd4` |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 | `0BgDfT1GoZnzjJOBwx5vFMK5JtqsTEas9pCEwd1/KKxNUAqFmreN60WeUoF+CsmSd9tOQuqWedvdBo/QqHuNTQ==` | `5494448bfa610e69f6b682624efc856d43b4a971bf11fe10b5b7951cb349370d` |
| Microsoft.AspNetCore.Authentication.OpenIdConnect | 10.0.0 | `6ATONu+5A2oh/vzmoFhf3cuQcclMaWGHrb1kvjVsYtml+gzuWD48MmbsItM4xAUQkJZ2t8XFmbGp8pZLPxKneA==` | `d8063382616f5dcd5853fb1d43f73d7cba4cd86b3900cb196f122ab9535925bc` |
| Microsoft.Bcl.AsyncInterfaces | 10.0.9 | `Aq7M8lG6BrHeatqKqncVm9m55ec34k6nnfPRLe/PvGg+b/pAsRM2ejofeKmCLjTXMa+5NGXm382f8CG/D5WDow==` | `82bcf210d6c573b0add1d8265204a7d53af79dd3357dd9501a24812f362d0bd7` |
| Microsoft.Bcl.Cryptography | 10.0.2 | `LG9Yll3B5aNpxv0+D47g6LiOiKBIlodhcHdQwcYzo8VeexFLGqx5ymetmA2aBRyo9cCcWsQWrFsdbsr8LvmWDw==` | `f8e62d716b1dd6a65c11769d61e038b7afa9c800e0d403d00b1a4a52d3f4d363` |
| Microsoft.Identity.Abstractions | 12.7.0 | `NU2nvct8vCnIxp9bbmp2sthVzvY+U1IvmEVUhaRN81WdwtYhCpQTBTlMR0h74LDdbvU267BR5x98pCR5o779Fg==` | `df72b634a36dfd5f46db8f75dc7a3fa141365b6a42b49f76959e01dd2a4696f8` |
| Microsoft.Identity.Client | 4.90.0 | `oc5A0jmu083T3OW1JVYMrM1rekIL8SkBRB/byMMQjFuYlRZsMh3wDeCwwrz9sGI6XlP5BIqO7XWgjPpF3reF5A==` | `f17744f920e9f890f0922ca9069256b13c1c35af4fc1fca8cb20ac140bb3c8cf` |
| Microsoft.Identity.Client.Extensions.Msal | 4.84.2 | `+D98LaU3dOu/Nzs6kgbBJapMvH7iwYcAeC99XwSkpmEadsPv6rozecOl9P6m4A3RfpOuPt9e6J6TwwUUp3+M4w==` | `b9611017286cb42bbd5f3c6e55ddf6eca648fba8426d795f4030d6e10adf3c27` |
| Microsoft.Identity.Web.Certificate | 4.15.0 | `vMpHOg8dyPr+AUxxYKkGR4GGIUOunl1lopCb7kJDgP47M0IOfsY6LCJK3SqS4KgilshFcJ6XLkups8KD966kUQ==` | `948d3d328af9e6bef9899d4c80c8760bd2b8f0599b45f273a50b322943eb9219` |
| Microsoft.Identity.Web.Certificateless | 4.15.0 | `sVhiJtbVNoRtPIeo3t5QnUsIA9wyjjrqDwCsubCr0dh0yfiLy3guHzB1CIPvAyPOGETrXwDQBt1JA4lWQc+ROg==` | `10769d28ec678bcc32390d3f6e094a17c1d0bc792e92ddab013d1bb3097780d6` |
| Microsoft.Identity.Web.Diagnostics | 4.15.0 | `Ax35Gg+ggVZ9ApauJSnoNduUUxt9zuuVF3ZxTQVHaimCV5N+meU7C3sqpznNXfF8ek21OyikN5qtM0bOL2uKxA==` | `f6d5ff4759053088b24f0c89305685bd96a568004ec73228d43752325a08496a` |
| Microsoft.Identity.Web.TokenAcquisition | 4.15.0 | `HWuNtTC7uW5BvqnI1/8Cx7Fda9soFESBVzQBaipklIABwvdyOgVR+7vu97uRNxcGxgWc40WEJJ2Su3eDK8ZhDg==` | `ffd723ebf7fd620eec95e57e6fc5a92c2130874d05fcb15170d49f9692cd6f56` |
| Microsoft.Identity.Web.TokenCache | 4.15.0 | `RMy/SLOxxs42ZJ2vRHqQ+EDEoQZKUGd8n0Gq02xMTo/1vtVJ+jx5DmBTUhIBApi/rKsMsEjYyIMrENaGkqoDCA==` | `6829270e1c47504d8288e4f7cf6db5c9500d45034e6a4876a129a1a178103c94` |
| Microsoft.IdentityModel.Abstractions | 8.22.0 | `LU3V3owsu4vGpCg2kyL7SsQEuHwcoJ8FSNBqzLADzCf3/PcKUTcx5Plsd51DoTJMfK/WigXV/03UhaN5JXE6uQ==` | `edb0451dd621fe77ed497edda1d523d7b481d236f6866ba6c988a19f9de4640c` |
| Microsoft.IdentityModel.JsonWebTokens | 8.22.0 | `kv6peMLjALZLDAy2H3F77KjVRdwiscn2p/g3ui2chcbuEcAX2MpAbyDcYnJ7Vyh8jZ1aJWrniUMCDWoOgnu4NQ==` | `f135bce7ff1c1d465a42b55bebc618cef1779ba976d1a709f8908ea003f41512` |
| Microsoft.IdentityModel.Logging | 8.22.0 | `G9Tl0yXSlr2pkXv4EpXjO16M4q6oo9N/od+gNyOusZ8yM8LZg1H3f/QOMFuOJiV6znzY5MkAREU97JRRnqpEQw==` | `c94b8b8eb35ffe4a957f55b6b71c7618a734b6a33a22a3a5709927a3ca5584ff` |
| Microsoft.IdentityModel.LoggingExtensions | 8.22.0 | `2z2lb8jbkEe3iNwXqVflzLPv8pUw/QJGKrcRqeUeIKwjx2o/SxLNl5/XLDeASH1oq4VnoxxKdHivKrmmog4HtQ==` | `811f30e3f5a8c3d00f9f35f07c72dbe47b59cf07bb526bb10e2bc08e2183ae83` |
| Microsoft.IdentityModel.Protocols | 8.22.0 | `q7VTnFzKHOUy1L7M6FOTD0nUboDobJIPb+Sl13bQ4O0lYtpTJ9wemcuZaDYOmT/3ZKyUUCFuFjt5UReYC2dlQA==` | `b54db36f452c32267243784424419332f29effba482c42a4dfe607a35a10e1dd` |
| Microsoft.IdentityModel.Protocols.OpenIdConnect | 8.22.0 | `RAStsQi0eIj2gcA6G1lVx2H39v9eCJty9bLymx4oeLzmEISxfa6MxTaZkbsp+Plccz0Uiv8r7efiaG10i3WHkg==` | `8d9174dc5d6957c5e239d790183fed43c163f940fda8e73c36c8e4649d0eaebc` |
| Microsoft.IdentityModel.Tokens | 8.22.0 | `i4lywKKUuVmheCUA+w/q8QNPReNI0qanHI9hhz48AFqD1ljyb8sxPL2RbXOGiPV13XdJ4kxieL9ukS7tD43LxA==` | `708dd5a0319d9678992f8c6cab4de1e7df5b0cbbbef2d95013d3db6d2ce1abc9` |
| Microsoft.IdentityModel.Validators | 8.22.0 | `8lMJuI0r46dVa648IUAUVXRc6ioGdvGB8zD+S+9DWHrXYdK1SWae9TsMWYwWeRJEZBErwZgVlPrzxDk71VYiZA==` | `424eac991681210dd6964dcf6400672a211ef1efc5b80fe08581d4f24c307322` |
| Npgsql | 10.0.3 | `7nb5YzXuvWWJxB0J8DiyL3we+X4FOctZrt0fIBnucOIaIevFEEwGQVZKtiu9olXdlNAK1eNgqSral6r/jlhI4w==` | `75d0970923a8c9fcbbd37e4ebe72fee0b10362a1e36723e86777df1b6728316d` |
| System.ClientModel | 1.15.0 | `y6zQLInrpX+oGL1gXM04DrUSu1O1esWxt4rD/zrujTnzIFChn/noD1OiVVpTrU8ZvglwqO2Uw038svUR3SiAOg==` | `34eee2404aab29bed4c5755a98a6ba94d812980fd9559aed67bc1871b45e1b02` |
| System.IdentityModel.Tokens.Jwt | 8.22.0 | `CpXGfNhLl6EgYaOC9XYsc1p7Ci9HtAy0soHJDSBNGse647al4tTq9RDr+LQsrF4Ls79Dx7VfzN34km0W4DWPow==` | `7ffcada814314a6dd7c736094eae1c2db982065f5d7df17cba89caefe93efaf0` |
| System.IO.Hashing | 8.0.0 | `ne1843evDugl0md7Fjzy6QjJrzsjh46ZKbhf8GwBXb5f/gw97J4bxMs0NQKifDuThh/f0bZ0e62NPl1jzTuRqA==` | `b33386b744cd068e9d11d0b781fe87f9ef585b7370d30a1ae949c218618ae5c1` |
| System.Memory.Data | 10.0.9 | `YweovHiLUoEdbr2Xv8eRqCSkTu1vMms4a6xBHaQiMSFVy32PL+hXv6Vhol2NWOmLHjaVgTyhJ3R52U6JxS6w3Q==` | `606fe3c0534308ff550f7e40a75f2d406c9098f95b49d03b52dde08a53128257` |
| System.Security.Cryptography.Pkcs | 10.0.10 | `KG8t5RHczdgB3IWvRndpI0nE1DSo7ikDYSDbITxfgRFyIvnYiKzuPjaZOsPnwFQ1aOGUl9sEulJul9wlHLEUoA==` | `c2439050e4f3bd559f2978179fbd2e0815c673a4ee83da1cbaa0bad8dcff6b6f` |
| System.Security.Cryptography.ProtectedData | 4.5.0 | `wLBKzFnDCxP12VL9ANydSYhk59fC4cvOr9ypYQLPnAj48NQIhqnjdD2yhP8yEKyBJEjERWS9DisKL7rX5eU25Q==` | `67e5f5676944acb2fb627b768c5b3392eebf220ae780edd5d5b49f6530621487` |

### Actual dependency edges

The following are dependency lower-bound constraints recorded by the actual lock after SDK pruning. Here `Package=version` preserves the lock notation and means `Package >= version`, not the selected resolved version of that edge; selected package versions are in the inventory above. Direct references retain their exact requested ranges stated above. Empty means no remaining package edge in this resolved target. This table and the hashes bind the host-specific graph rather than the previous minimum manifest traversal.

| Package | Actual remaining dependency version constraints |
|---|---|
| Azure.Extensions.AspNetCore.DataProtection.Blobs | Azure.Core=1.61.0; Azure.Storage.Blobs=12.26.0 |
| Azure.Extensions.AspNetCore.DataProtection.Keys | Azure.Core=1.61.0; Azure.Security.KeyVault.Keys=4.10.0 |
| Microsoft.Identity.Web | Microsoft.Identity.Web.Certificate=4.15.0; Microsoft.Identity.Web.Certificateless=4.15.0; Microsoft.Identity.Web.TokenAcquisition=4.15.0; Microsoft.Identity.Web.TokenCache=4.15.0; Microsoft.IdentityModel.Protocols.OpenIdConnect=8.22.0; Microsoft.IdentityModel.Validators=8.22.0; System.IdentityModel.Tokens.Jwt=8.22.0 |
| Azure.Core | Microsoft.Bcl.AsyncInterfaces=10.0.9; Microsoft.Identity.Client=4.84.2; Microsoft.Identity.Client.Extensions.Msal=4.84.2; System.ClientModel=1.15.0; System.Memory.Data=10.0.9 |
| Azure.Identity | Azure.Core=1.50.0; Microsoft.Identity.Client=4.83.1; Microsoft.Identity.Client.Extensions.Msal=4.83.1 |
| Azure.Security.KeyVault.Certificates | Azure.Core=1.37.0 |
| Azure.Security.KeyVault.Keys | Azure.Core=1.54.0 |
| Azure.Security.KeyVault.Secrets | Azure.Core=1.37.0 |
| Azure.Storage.Blobs | Azure.Core=1.47.3; Azure.Storage.Common=12.25.0 |
| Azure.Storage.Common | Azure.Core=1.47.3; System.IO.Hashing=8.0.0 |
| Microsoft.AspNetCore.Authentication.JwtBearer | Microsoft.IdentityModel.Protocols.OpenIdConnect=8.0.1 |
| Microsoft.AspNetCore.Authentication.OpenIdConnect | Microsoft.IdentityModel.Protocols.OpenIdConnect=8.0.1 |
| Microsoft.Bcl.AsyncInterfaces | none |
| Microsoft.Bcl.Cryptography | none |
| Microsoft.Identity.Abstractions | none |
| Microsoft.Identity.Client | Microsoft.IdentityModel.Abstractions=8.14.0 |
| Microsoft.Identity.Client.Extensions.Msal | Microsoft.Identity.Client=4.84.2; System.Security.Cryptography.ProtectedData=4.5.0 |
| Microsoft.Identity.Web.Certificate | Azure.Identity=1.17.2; Azure.Security.KeyVault.Certificates=4.6.0; Azure.Security.KeyVault.Secrets=4.6.0; Microsoft.Identity.Abstractions=12.7.0; Microsoft.Identity.Web.Certificateless=4.15.0; Microsoft.Identity.Web.Diagnostics=4.15.0 |
| Microsoft.Identity.Web.Certificateless | Microsoft.Identity.Client=4.90.0; Microsoft.IdentityModel.JsonWebTokens=8.22.0; Microsoft.IdentityModel.LoggingExtensions=8.22.0 |
| Microsoft.Identity.Web.Diagnostics | none |
| Microsoft.Identity.Web.TokenAcquisition | Microsoft.AspNetCore.Authentication.JwtBearer=10.0.0; Microsoft.AspNetCore.Authentication.OpenIdConnect=10.0.0; Microsoft.Identity.Abstractions=12.7.0; Microsoft.Identity.Web.Certificate=4.15.0; Microsoft.Identity.Web.Certificateless=4.15.0; Microsoft.Identity.Web.TokenCache=4.15.0; Microsoft.IdentityModel.Logging=8.22.0; Microsoft.IdentityModel.LoggingExtensions=8.22.0; Microsoft.IdentityModel.Protocols.OpenIdConnect=8.22.0; System.IdentityModel.Tokens.Jwt=8.22.0 |
| Microsoft.Identity.Web.TokenCache | Microsoft.Identity.Client=4.90.0; Microsoft.Identity.Web.Diagnostics=4.15.0; System.Security.Cryptography.Pkcs=10.0.10 |
| Microsoft.IdentityModel.Abstractions | none |
| Microsoft.IdentityModel.JsonWebTokens | Microsoft.IdentityModel.Tokens=8.22.0 |
| Microsoft.IdentityModel.Logging | Microsoft.IdentityModel.Abstractions=8.22.0 |
| Microsoft.IdentityModel.LoggingExtensions | Microsoft.IdentityModel.Abstractions=8.22.0 |
| Microsoft.IdentityModel.Protocols | Microsoft.IdentityModel.Tokens=8.22.0 |
| Microsoft.IdentityModel.Protocols.OpenIdConnect | Microsoft.IdentityModel.Protocols=8.22.0; System.IdentityModel.Tokens.Jwt=8.22.0 |
| Microsoft.IdentityModel.Tokens | Microsoft.Bcl.Cryptography=10.0.2; Microsoft.IdentityModel.Logging=8.22.0 |
| Microsoft.IdentityModel.Validators | Microsoft.IdentityModel.Protocols=8.22.0; Microsoft.IdentityModel.Protocols.OpenIdConnect=8.22.0; Microsoft.IdentityModel.Tokens=8.22.0; System.IdentityModel.Tokens.Jwt=8.22.0 |
| Npgsql | none |
| System.ClientModel | System.Memory.Data=10.0.9 |
| System.IdentityModel.Tokens.Jwt | Microsoft.IdentityModel.JsonWebTokens=8.22.0; Microsoft.IdentityModel.Tokens=8.22.0 |
| System.IO.Hashing | none |
| System.Memory.Data | none |
| System.Security.Cryptography.Pkcs | none |
| System.Security.Cryptography.ProtectedData | none |

## Explicit signature verification and audit findings

Normal macOS SDK verification, not sandbox certificate emulation, ran `dotnet nuget verify` with **all37 exact selected `.nupkg` paths** and `--all --verbosity normal`. Python subprocess captured the verifier return code directly: **exit1**. No display wrapper masked it. The full selected path list and exact command are retained in `signature-execution.json` with its digest below. Both new providers and the other34 valid artifacts report success; the one failure is:

| Selected failure | Exact normal-verifier evidence |
|---|---|
| System.Security.Cryptography.ProtectedData4.5.0 | NU3037: author primary signature validity period expired; NU3028: author timestamp chain `ExplicitDistrust`; NU3037: repository countersignature validity period expired; NU3028: repository timestamp chain `ExplicitDistrust`; `Package signature validation failed` |

This is an unresolved **macOS verifier trust/expiry failure**, not proof of package tampering, and not an authorization to accept it. It was already selected in the source's existing host lock. No automatic upgrade, signature bypass, trust-store edit or retry on another platform was performed. Docker/Podman were not installed or used; no Linux verification is claimed. Operations/security must decide how the actual approved build platform and exact graph meet integrity requirements before adapter execution/installation. No unrelated product package was modified to evade this failure.

Restore enabled NuGetAudit=true/mode=all/level=low and reported no advisory warnings. Fresh isolated HTTP-cache evidence shows the official [vulnerability index](https://api.nuget.org/v3/vulnerabilities/index.json) selected the [base](https://api.nuget.org/v3-vulnerabilities/2026.09.26.05.43.06/vulnerability.base.json) updated `2026-09-26T05:43:06.7863496Z` and [update](https://api.nuget.org/v3-vulnerabilities/2026.09.26.05.43.06/2026.10.02.23.43.48/vulnerability.update.json) updated `2026-10-02T23:43:48.5527772Z`. Cache/index page hashes are retained. This is dated successful audited restore evidence, not a claim that vulnerabilities cannot exist; audit must be repeated for any approved graph/build.

## Artifact/log receipts

Receipts name temporary evidence, not separately committed configuration/locks. The temporary files can be inspected while retained; the source/version/commands and inventory preserve the result if those files disappear. Original repository lockfiles remain unchanged.

| Evidence | SHA256 |
|---|---|
| resolved-lock.json | `2d726d936d5cb3b5d647ef77a3f62d56e16fb0a70b49738f483ab3c7b5eebe9f` |
| resolved-artifacts.json | `9741b9f08a6886b42a593799efbd47dfafaff0845c5fd7be039ba6a11c543121` |
| restore-execution.json | `5d47daeed3c1c2a79a5123c82ba404e47d754a4fed6138da4b359edf2704c674` |
| restore.log | `08ba441879693e385d88e66249062746a25e996174cd13e8bfd18cbf2030447a` |
| signature-execution.json | `4b1a2678bf09c9f8f083e701142ee553e9349e06a894863b629801e4416d150d` |
| selected-signatures.log | `e81d3777bd081eddb1878536781622c61af57946dd609eb70d3f0599b7560c57` |
| nuget-public.config | `bd495b47e9c08cfaa8a0d611a5648cce7f1de660bcb64365c74a9bce9cb0a51e` |
| vuln_index.dat | `ab489a2e0f44e13aeb239510e49be10224e9349b8c30781cb4a17254167b50f3` |
| vuln_data_base.dat | `c65988e60203108ee85b44b5c3a4d2f5efc78e0f56cf508bdc6b02c991a7c098` |
| vuln_data_update.dat | `44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a` |
| BFF project.assets.json | `432f6c7796b7302a655eb65d0e3baa1bb1cb7c656c9fc05ec8ff16b7a8874a1f` |
| IdentitySessions lock | `ef0b1094a4ffbf71f97742bf7e8ffc6a232e1a11aefd31161fb469345ed67d86` |
| IdentityAuthority lock | `337324e07366c0b4a522d98c189b405cef7510e1dc522a02f7d53aefc15a89ef` |
| IdentityPolicy lock | `a29c6aa8cfb81874ff8bb78dc369d7416f28c9b8cc47e99592bfc019b20c41eb` |

## Completion limits and next dependency

AP-T04 now has actual resolver/lock/artifact/signature failure evidence. It does **not** complete KEY-001/002/005 or any Azure/provider activation gate. Because selected integrity is unresolved, supported Blob legacy-overload and resolver guard dispatch remain source evidence only; no two-replica/restart/ETag/outage/deletion/unwrap or diagnostics harness ran.

The accepted preferred provider strategy remains unchanged. Blocking inputs still include approved-platform integrity for the actual graph, reviewed repository pins/locks and applicable build tests, compiled guard interception, trusted monotonic inventory/witness authority, key lifecycle/recovery/deadline/readiness choices, exact identity/scopes/private routing, production host/resource/CA/ingress and audit-outage preservation. No paid session, key/tenant access, grant or production release is inferred. Coordinator owns canonical status/plan/private-board updates and independent non-author review.
