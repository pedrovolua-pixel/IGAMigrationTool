using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Azure;
using Azure.Core;
using Azure.Core.Cryptography;
using Azure.Core.Pipeline;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal static class ResolverPipelineCases
{
    internal const string KeyPath = FixtureResolver.Path;
    internal const string Concrete = KeyPath + "/v1";
    private const string FixtureToken = "synthetic-fixture-token-not-a-credential";
    private static int checks;

    private static void Case(string label, Action action)
    {
        Console.WriteLine(label + " START");
        action();
        checks++;
        Console.WriteLine(label + " PASS");
    }

    internal static void Run()
    {
        foreach (var asynchronous in new[] { false, true })
        {
            Case("RP01-metadata200-" + (asynchronous ? "async" : "sync"), () =>
            {
                using var fixture = new ScriptFixture();
                var resolver = fixture.Resolver();
                var key = asynchronous ? resolver.ResolveAsync(new Uri(KeyPath)).GetAwaiter().GetResult() : resolver.Resolve(new Uri(KeyPath));
                Dispatch.Require(key.KeyId == Concrete);
                fixture.AssertCounts(1, 0);
                fixture.AssertMode(asynchronous);
            });
            Case("RP03-terminal403-" + (asynchronous ? "async" : "sync"), () =>
            {
                using var fixture = new ScriptFixture { MetadataStatus = 403 };
                var resolver = fixture.Resolver(guard403: true);
                Dispatch.Denies<GuardDenied>(() =>
                {
                    if (asynchronous)
                    {
                        resolver.ResolveAsync(new Uri(KeyPath)).GetAwaiter().GetResult();
                    }
                    else
                    {
                        resolver.Resolve(new Uri(KeyPath));
                    }
                });
                fixture.AssertCounts(1, 0);
                fixture.AssertMode(asynchronous);
                Dispatch.Require(fixture.Policy!.Denied403 == 1);
            });
        }
        Case("RP03-provider-refuses-wrap", () =>
        {
            using var fixture = new ScriptFixture { MetadataStatus = 403 };
            var resolver = new FixedResolver(fixture.Resolver(guard403: true));
            var services = new ServiceCollection();
            services.AddDataProtection().ProtectKeysWithAzureKeyVault(new Uri(KeyPath), resolver);
            using var provider = services.BuildServiceProvider();
            var encryptor = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor!;
            Dispatch.Denies<GuardDenied>(() => encryptor.Encrypt(new XElement("fixture", "synthetic")));
            fixture.AssertCounts(1, 0);
            Dispatch.Require(fixture.Policy!.Denied403 == 1);
        });
        foreach (var id in new[] { "https://foreign.invalid/keys/wrapping/v1", KeyPath + "/v2", KeyPath + "?alternate=true" })
        {
            Case("RP04-input-denied-" + checks, () =>
            {
                using var fixture = new ScriptFixture();
                var resolver = new FixedResolver(fixture.Resolver(guard403: true));
                Dispatch.Denies<GuardDenied>(() => resolver.Resolve(id));
                Dispatch.Denies<GuardDenied>(() => resolver.ResolveAsync(id).GetAwaiter().GetResult());
                fixture.AssertCounts(0, 0);
            });
        }
        foreach (var returned in new[] { KeyPath, "https://foreign.invalid/keys/wrapping/v1" })
        {
            Case("RP04-resolved-denied-" + checks, () =>
            {
                using var fixture = new ScriptFixture { ReturnedId = returned };
                var resolver = new FixedResolver(fixture.Resolver(guard403: true));
                Dispatch.Denies<GuardDenied>(() => resolver.Resolve(KeyPath));
                fixture.AssertCounts(1, 0);
            });
        }
        Case("RP04-versionless-historical-before-resolver", () =>
        {
            using var fixture = new ScriptFixture();
            var resolver = new FixedResolver(fixture.Resolver(guard403: true));
            var services = new ServiceCollection();
            services.AddDataProtection().ProtectKeysWithAzureKeyVault(new Uri(KeyPath), resolver);
            using var provider = services.BuildServiceProvider();
            var encrypted = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor!.Encrypt(new XElement("fixture"));
            encrypted.EncryptedElement.Element("kid")!.Value = KeyPath;
            var decryptor = (IXmlDecryptor)ActivatorUtilities.CreateInstance(provider, encrypted.DecryptorType);
            Dispatch.Denies<GuardDenied>(() => HistoricalDecrypt(decryptor, encrypted.EncryptedElement));
            fixture.AssertCounts(1, 0);
        });
        Case("RP07-unexpected-request-trap", () =>
        {
            using var fixture = new ScriptFixture();
            var resolver = fixture.Resolver();
            Dispatch.Denies<GuardDenied>(() => resolver.Resolve(new Uri("https://foreign.invalid/keys/other/v1")));
            Dispatch.Require(fixture.TransportCalls == 1 && fixture.UnexpectedRequests == 1 && fixture.Credential.Calls == 0);
            Console.WriteLine("RP07 unexpected=1 transport=1 credential=0 baseHttp=0");
        });
        // Finish all unchallenged metadata/403 negatives before supported auth primes SDK static cache.
        AuthenticatedCases();
        DiagnosticCases();
        Console.WriteLine("RESOLVER PIPELINE PASS checks=" + checks);
    }

    private static void AuthenticatedCases()
    {
        foreach (var asynchronous in new[] { false, true })
        {
            Case("RP02-unguarded403-force-remote-" + (asynchronous ? "async" : "sync"), () =>
            {
                using var fixture = new ScriptFixture { MetadataStatus = 403, AcceptSyntheticToken = true };
                var resolver = fixture.Resolver();
                var client = asynchronous ? resolver.ResolveAsync(new Uri(KeyPath)).GetAwaiter().GetResult() : resolver.Resolve(new Uri(KeyPath));
                var input = new byte[32];
                var result = asynchronous ? client.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep, input).GetAwaiter().GetResult() :
                    client.WrapKey(KeyWrapAlgorithm.RsaOaep, input);
                Dispatch.Require(fixture.Rsa.Decrypt(result.EncryptedKey, RSAEncryptionPadding.OaepSHA1).SequenceEqual(input));
                Dispatch.Require(result.KeyId == Concrete && client.KeyId == KeyPath);
                fixture.AssertCounts(1, asynchronous ? 1 : 2, asynchronous ? 0 : 1, 1);
                Dispatch.Require(fixture.SuccessfulCrypto == 1);
                fixture.AssertMode(asynchronous);
                Console.WriteLine("RP02 resolvedIsVersionless=true responseIsConcrete=true");
            });
        }
        Case("RP01-actual-provider-wrap-historical-decrypt", () =>
        {
            using var fixture = new ScriptFixture { AcceptSyntheticToken = true };
            var resolver = new FixedResolver(fixture.Resolver(guard403: true));
            var services = new ServiceCollection();
            services.AddDataProtection().ProtectKeysWithAzureKeyVault(new Uri(KeyPath), resolver);
            using var provider = services.BuildServiceProvider();
            var encryptor = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor!;
            var encrypted = encryptor.Encrypt(new XElement("fixture", "synthetic"));
            Dispatch.Require(encrypted.EncryptedElement.Element("kid")!.Value == Concrete);
            fixture.AssertCounts(1, 0, tokens: 1); // Actual SDK local public-RSA wrap after metadata GET.
            var decryptor = (IXmlDecryptor)ActivatorUtilities.CreateInstance(provider, encrypted.DecryptorType);
            Dispatch.Require(HistoricalDecrypt(decryptor, encrypted.EncryptedElement).Value == "synthetic");
            fixture.AssertCounts(2, 1, tokens: 1); // Public-only metadata requires scripted remote unwrap.
            Dispatch.Require(fixture.HistoricalMetadata == 1 && fixture.UnwrapRequests == 1);
        });
    }

    private static XElement HistoricalDecrypt(IXmlDecryptor decryptor, XElement encrypted)
    {
        if (encrypted.Element("kid")?.Value != Concrete)
        {
            throw new GuardDenied();
        }
        return decryptor.Decrypt(encrypted);
    }

    private static void DiagnosticCases()
    {
        foreach (var off in new[] { false, true })
        {
            Case(off ? "RP06-supported-diagnostics-off" : "RP05-normal-diagnostics", () =>
            {
                using var observations = new DiagnosticObservations();
                using var fixture = new ScriptFixture { MetadataStatus = 403, AcceptSyntheticToken = true };
                var client = fixture.Resolver(diagnosticsOff: off).Resolve(new Uri(KeyPath));
                client.WrapKey(KeyWrapAlgorithm.RsaOaep, new byte[32]);
                fixture.AssertCounts(1, 1, tokens: 1);
                var state = observations.State;
                if (!off)
                {
                    Dispatch.Require(state.ActivityCount > 0 && state.DiagnosticCount > 0 && state.EventCount > 0);
                    Dispatch.Require(state.BindingSeen && state.ProtectedFieldSeen);
                }
                // No assumption that one SDK switch universally suppresses every diagnostics channel.
                Dispatch.Require(state.Sink.Accepted == 0 && !state.Sink.ProtectedReached);
                state.Sink.Offer(new Dictionary<string, object?> { ["kind"] = "synthetic-safe-count", ["count"] = 1 });
                Dispatch.Require(state.Sink.Accepted == 1 && !state.Sink.ProtectedReached);
                state.Print(off);
            });
        }
        Case("RP06-allowlist-hostile-and-positive-controls", () =>
        {
            var sink = new AllowlistSink();
            sink.Offer(new Dictionary<string, object?> { ["kind"] = KeyPath, ["count"] = 1 });
            sink.Offer(new Dictionary<string, object?> { ["kind"] = "synthetic-safe-count", ["count"] = 1, ["body"] = "synthetic-protected" });
            sink.Offer(new Dictionary<string, object?> { ["kind"] = "synthetic-safe-count", ["count"] = 1 });
            Dispatch.Require(sink.Rejected == 2 && sink.Accepted == 1 && !sink.ProtectedReached);
        });
    }

    private sealed class FixedResolver(KeyResolver resolver) : IKeyEncryptionKeyResolver
    {
        private static void Input(string id)
        {
            if (id != KeyPath && id != Concrete)
            {
                throw new GuardDenied();
            }
        }
        private static IKeyEncryptionKey Result(CryptographyClient client)
        {
            if (client.KeyId != Concrete)
            {
                throw new GuardDenied();
            }
            return client;
        }
        public IKeyEncryptionKey Resolve(string keyId, CancellationToken cancellationToken = default)
        {
            Input(keyId);
            return Result(resolver.Resolve(new Uri(keyId), cancellationToken));
        }
        public async Task<IKeyEncryptionKey> ResolveAsync(string keyId, CancellationToken cancellationToken = default)
        {
            Input(keyId);
            return Result(await resolver.ResolveAsync(new Uri(keyId), cancellationToken));
        }
    }

    private sealed class TerminalMetadataPolicy : HttpPipelineSynchronousPolicy
    {
        internal int Denied403 { get; private set; }
        public override void OnSendingRequest(HttpMessage message)
        {
            var uri = message.Request.Uri.ToUri();
            if (uri.GetLeftPart(UriPartial.Path) != KeyPath && uri.GetLeftPart(UriPartial.Path) != Concrete &&
                uri.GetLeftPart(UriPartial.Path) != Concrete + "/unwrapKey")
            {
                throw new GuardDenied();
            }
        }
        public override void OnReceivedResponse(HttpMessage message)
        {
            if (message.Request.Method == RequestMethod.Get && message.Response.Status == 403)
            {
                Denied403++;
                // Do not use RequestFailedException403: deny before KeyResolver's ParseResponse fallback.
                throw new GuardDenied();
            }
        }
    }

    private sealed class UnexpectedCredential : TokenCredential
    {
        internal int Calls { get; private set; }
        internal bool Expected { get; set; }
        private AccessToken Token(TokenRequestContext context)
        {
            Calls++;
            if (!Expected || !context.Scopes.SequenceEqual(new[] { "https://invalid/.default" }) || context.TenantId != "synthetic-tenant" ||
                context.Claims is not null || !context.IsCaeEnabled)
            {
                throw new GuardDenied();
            }
            return new AccessToken(FixtureToken, DateTimeOffset.UtcNow.AddHours(1)); // Fixture expiry only.
        }
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return Token(requestContext);
        }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Token(requestContext));
        }
    }

    private sealed class ScriptFixture : HttpPipelineTransport, IDisposable
    {
        internal RSA Rsa { get; } = RSA.Create(2048); // Synthetic fixture; no production crypto selection.
        internal UnexpectedCredential Credential { get; } = new();
        internal int MetadataStatus { get; init; } = 200;
        internal bool AcceptSyntheticToken { get; init; }
        internal string ReturnedId { get; init; } = Concrete;
        internal int TransportCalls { get; private set; }
        internal int SyncCalls { get; private set; }
        internal int AsyncCalls { get; private set; }
        internal int MetadataRequests { get; private set; }
        internal int HistoricalMetadata { get; private set; }
        internal int CryptoRequests { get; private set; }
        internal int SuccessfulCrypto { get; private set; }
        internal int Challenges { get; private set; }
        internal int AuthenticatedRequests { get; private set; }
        internal int UnwrapRequests { get; private set; }
        internal int UnexpectedRequests { get; private set; }
        internal TerminalMetadataPolicy? Policy { get; private set; }

        internal KeyResolver Resolver(bool guard403 = false, bool diagnosticsOff = false)
        {
            Credential.Expected = AcceptSyntheticToken;
            var options = new CryptographyClientOptions { Transport = this, Retry = { MaxRetries = 0 } };
            Dispatch.Require(!options.DisableChallengeResourceVerification);
            if (guard403)
            {
                Policy = new TerminalMetadataPolicy();
                options.AddPolicy(Policy, HttpPipelinePosition.PerRetry);
            }
            if (diagnosticsOff)
            {
                options.Diagnostics.IsLoggingEnabled = false;
                options.Diagnostics.IsLoggingContentEnabled = false;
                options.Diagnostics.IsDistributedTracingEnabled = false;
                options.Diagnostics.IsTelemetryEnabled = false;
            }
            return new KeyResolver(Credential, options);
        }

        public override Request CreateRequest() => HttpClientTransport.Shared.CreateRequest();
        public override void Process(HttpMessage message)
        {
            SyncCalls++;
            Handle(message);
        }
        private void Handle(HttpMessage message)
        {
            TransportCalls++;
            var uri = message.Request.Uri.ToUri();
            var path = uri.GetLeftPart(UriPartial.Path);
            var hasAuthorization = message.Request.Headers.TryGetValue("Authorization", out var authorization);
            if (!uri.Query.StartsWith("?api-version=", StringComparison.Ordinal) || uri.Query.Contains('&') ||
                uri.Fragment.Length != 0 || (hasAuthorization && (!AcceptSyntheticToken || authorization != "Bearer " + FixtureToken)))
            {
                UnexpectedRequests++;
                Console.WriteLine("RP07 unexpected headersOrQuery=true");
                throw new GuardDenied();
            }
            if (hasAuthorization)
            {
                AuthenticatedRequests++;
            }
            if (message.Request.Method == RequestMethod.Get && (path == KeyPath || path == Concrete))
            {
                MetadataRequests++;
                if (path == Concrete)
                {
                    HistoricalMetadata++;
                }
                var rsa = Rsa.ExportParameters(false);
                var body = MetadataStatus == 403 ? "{\"error\":{\"code\":\"Forbidden\",\"message\":\"Synthetic denial\"}}" :
                    JsonSerializer.Serialize(new
                    {
                        key = new { kid = ReturnedId, kty = "RSA", key_ops = new[] { "wrapKey", "unwrapKey" }, n = Encode(rsa.Modulus!), e = Encode(rsa.Exponent!) },
                        attributes = new { enabled = true }
                    });
                message.Response = new ScriptResponse(MetadataStatus, body);
                return;
            }
            if (message.Request.Method == RequestMethod.Post && (path == KeyPath + "/wrapKey" || path == Concrete + "/unwrapKey"))
            {
                CryptoRequests++;
                if (!hasAuthorization && AcceptSyntheticToken)
                {
                    Dispatch.Require(message.Request.Content is null && Challenges == 0);
                    Challenges++;
                    message.Response = new ScriptResponse(401, "{\"error\":{\"code\":\"Unauthorized\"}}", challenge: true);
                    return;
                }
                Dispatch.Require(hasAuthorization && AcceptSyntheticToken);
                using var stream = new MemoryStream();
                Dispatch.Require(message.Request.Content is not null);
                message.Request.Content!.WriteTo(stream, message.CancellationToken);
                using var json = JsonDocument.Parse(stream.ToArray());
                Dispatch.Require(json.RootElement.GetProperty("alg").GetString() == "RSA-OAEP");
                var input = Decode(json.RootElement.GetProperty("value").GetString()!);
                byte[] result;
                if (path.EndsWith("/unwrapKey", StringComparison.Ordinal))
                {
                    UnwrapRequests++;
                    result = Rsa.Decrypt(input, RSAEncryptionPadding.OaepSHA1);
                }
                else
                {
                    result = Rsa.Encrypt(input, RSAEncryptionPadding.OaepSHA1);
                }
                message.Response = new ScriptResponse(200, JsonSerializer.Serialize(new { kid = Concrete, value = Encode(result) }));
                SuccessfulCrypto++;
                return;
            }
            UnexpectedRequests++;
            Console.WriteLine("RP07 unexpected metadata=" + MetadataRequests + " crypto=" + CryptoRequests +
                              " isPost=" + (message.Request.Method == RequestMethod.Post) + " doubledWrapSeparator=" + path.Contains("//wrapkey", StringComparison.Ordinal) +
                              " caseOnlyWrapDifference=" + path.Equals(KeyPath + "/wrapkey", StringComparison.OrdinalIgnoreCase) +
                              " concreteWrap=" + path.Equals(Concrete + "/wrapkey", StringComparison.Ordinal) +
                              " trailingSlash=" + path.EndsWith("/", StringComparison.Ordinal) + " segmentCount=" + uri.Segments.Length +
                              " knownHost=" + (uri.Host == new Uri(KeyPath).Host));
            throw new GuardDenied();
        }
        public override ValueTask ProcessAsync(HttpMessage message)
        {
            AsyncCalls++;
            Handle(message);
            return ValueTask.CompletedTask;
        }
        internal void AssertCounts(int metadata, int crypto, int challenges = 0, int tokens = 0)
        {
            Dispatch.Require(MetadataRequests == metadata && CryptoRequests == crypto &&
                             TransportCalls == metadata + crypto && Credential.Calls == tokens && Challenges == challenges && UnexpectedRequests == 0);
            Console.WriteLine("RP07 counters metadata=" + MetadataRequests + " crypto=" + CryptoRequests +
                              " transport=" + TransportCalls + " sync=" + SyncCalls + " async=" + AsyncCalls +
                              " credential=" + Credential.Calls + " challenge=" + Challenges + " authenticated=" + AuthenticatedRequests +
                              " acceptedCrypto=" + SuccessfulCrypto + " unexpected=0 baseHttp=0");
        }
        internal void AssertMode(bool asynchronous) => Dispatch.Require(asynchronous ? AsyncCalls == TransportCalls && SyncCalls == 0 : SyncCalls == TransportCalls && AsyncCalls == 0);
        public void Dispose() => Rsa.Dispose();
        private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static byte[] Decode(string value)
        {
            var text = value.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(text.PadRight((text.Length + 3) / 4 * 4, '='));
        }
    }

    private sealed class ScriptResponse : Response
    {
        private readonly Dictionary<string, string> headers;
        internal ScriptResponse(int status, string body, bool challenge = false)
        {
            Status = status;
            var bytes = Encoding.UTF8.GetBytes(body);
            ContentStream = new MemoryStream(bytes);
            headers = new(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json", ["Content-Length"] = bytes.Length.ToString(), ["x-ms-request-id"] = "synthetic" };
            if (challenge)
            {
                headers["WWW-Authenticate"] = "Bearer authorization=\"https://synthetic-auth.invalid/synthetic-tenant\", resource=\"https://invalid\"";
            }
        }
        public override int Status { get; }
        public override string ReasonPhrase => "Synthetic";
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = "synthetic";
        public override void Dispose() => ContentStream?.Dispose();
        protected override bool ContainsHeader(string name) => headers.ContainsKey(name);
        protected override bool TryGetHeader(string name, out string value) => headers.TryGetValue(name, out value!);
        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            var found = headers.TryGetValue(name, out var value);
            values = found ? new[] { value! } : [];
            return found;
        }
        protected override IEnumerable<HttpHeader> EnumerateHeaders() => headers.Select(h => new HttpHeader(h.Key, h.Value));
    }

    private sealed class AllowlistSink
    {
        internal int Accepted { get; private set; }
        internal int Rejected { get; private set; }
        internal bool ProtectedReached { get; private set; }
        internal void Offer(IReadOnlyDictionary<string, object?> fields)
        {
            if (fields.Count != 2 || !fields.TryGetValue("kind", out var kind) || kind is not string text || text != "synthetic-safe-count" ||
                !fields.TryGetValue("count", out var count) || count is not int number || number < 0)
            {
                Rejected++;
                return;
            }
            // Only the closed label and integer reach this test sink; no raw payload is retained.
            ProtectedReached |= text.Contains(KeyPath, StringComparison.Ordinal);
            Accepted++;
        }
    }

    private sealed class ObservationState
    {
        private readonly object gate = new();
        internal AllowlistSink Sink { get; } = new();
        internal int ActivityCount { get; private set; }
        internal int DiagnosticCount { get; private set; }
        internal int EventCount { get; private set; }
        internal bool BindingSeen { get; private set; }
        internal bool BodyFieldSeen { get; private set; }
        internal bool ProtectedFieldSeen { get; private set; }
        internal bool TokenSeen { get; private set; }
        internal bool CryptoMarkerSeen { get; private set; }
        internal void Observe(string channel, object? payload, string field = "")
        {
            lock (gate)
            {
                if (channel == "activity")
                {
                    ActivityCount++;
                }
                else if (channel == "diagnostic")
                {
                    DiagnosticCount++;
                }
                else
                {
                    EventCount++;
                }
                Inspect(payload, field, 0);
                Sink.Offer(new Dictionary<string, object?> { ["payload"] = payload });
            }
        }
        private void Inspect(object? value, string field, int depth)
        {
            // Bounded observation only, not a generic production redaction or serializer.
            if (depth > 4 || value is null)
            {
                return;
            }
            ProtectedFieldSeen |= field is "az.keyvault.key.id" or "uri" or "requestUri" or "body" or "content";
            if (value is string text)
            {
                BindingSeen |= text.Contains(KeyPath, StringComparison.Ordinal);
                BodyFieldSeen |= text.Contains("\"value\"", StringComparison.Ordinal) || text.Contains("\"key_ops\"", StringComparison.Ordinal);
                TokenSeen |= text.Contains(FixtureToken, StringComparison.Ordinal);
                CryptoMarkerSeen |= text.Contains(Convert.ToBase64String(new byte[32]).TrimEnd('='), StringComparison.Ordinal);
                return;
            }
            if (value is Activity activity)
            {
                foreach (var tag in activity.TagObjects)
                {
                    Inspect(tag.Value, tag.Key, depth + 1);
                }
                return;
            }
            if (value is EventWrittenEventArgs eventData)
            {
                if (eventData.Payload is not null)
                {
                    for (var i = 0; i < eventData.Payload.Count; i++)
                    {
                        Inspect(eventData.Payload[i], eventData.PayloadNames?[i] ?? "", depth + 1);
                    }
                }
                return;
            }
            if (value is IEnumerable collection)
            {
                foreach (var item in collection)
                {
                    Inspect(item, field, depth + 1);
                }
                return;
            }
            foreach (var property in value.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length != 0 || property.PropertyType != typeof(string))
                {
                    continue;
                }
                Inspect(property.GetValue(value), property.Name, depth + 1);
            }
        }
        internal void Print(bool off)
        {
            lock (gate)
            {
                Console.WriteLine("RP05 observations off=" + off + " activity=" + ActivityCount + " diagnostic=" + DiagnosticCount +
                                  " event=" + EventCount + " binding=" + BindingSeen + " bodyField=" + BodyFieldSeen +
                                  " protectedField=" + ProtectedFieldSeen + " token=" + TokenSeen + " cryptoMarker=" + CryptoMarkerSeen +
                                  " sinkAccepted=" + Sink.Accepted + " sinkRejected=" + Sink.Rejected +
                                  " protectedReachedSink=" + Sink.ProtectedReached);
            }
        }
    }

    private sealed class DiagnosticObservations : IDisposable
    {
        internal ObservationState State { get; } = new();
        private readonly ActivityListener activity;
        private readonly IDisposable all;
        private readonly List<IDisposable> subscriptions = [];
        private readonly EventObserver events;
        internal DiagnosticObservations()
        {
            activity = new ActivityListener
            {
                ShouldListenTo = source => source.Name.StartsWith("Azure", StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = value => State.Observe("activity", value)
            };
            ActivitySource.AddActivityListener(activity);
            all = DiagnosticListener.AllListeners.Subscribe(new Observer<DiagnosticListener>(listener =>
            {
                if (listener.Name.StartsWith("Azure", StringComparison.Ordinal))
                {
                    subscriptions.Add(listener.Subscribe(new Observer<KeyValuePair<string, object?>>(value => State.Observe("diagnostic", value.Value))));
                }
            }));
            events = new EventObserver(State);
        }
        public void Dispose()
        {
            events.Dispose();
            all.Dispose();
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
            activity.Dispose();
        }
    }

    private sealed class Observer<T>(Action<T> next) : IObserver<T>
    {
        public void OnNext(T value) => next(value);
        public void OnError(Exception error) => throw new GuardDenied();
        public void OnCompleted() { }
    }

    private sealed class EventObserver : EventListener
    {
        private readonly ObservationState? state;
        internal EventObserver(ObservationState value) => state = value;
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name.StartsWith("Azure", StringComparison.Ordinal))
            {
                EnableEvents(source, EventLevel.Verbose);
            }
        }
        protected override void OnEventWritten(EventWrittenEventArgs data)
        {
            state?.Observe("event", data);
        }
    }
}
