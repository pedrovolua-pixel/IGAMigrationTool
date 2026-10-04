using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdentitySessions;

internal static class AuthenticationFailureJournalChecks
{
    public static async Task<int> RunAsync()
    {
        var checks = 0;
        void Check(bool value, string label)
        {
            if (!value) throw new Exception("Failure journal test: " + label);
            checks++;
        }
        void Denied(Action action, string label)
        {
            try { action(); throw new Exception("Expected journal denial: " + label); }
            catch (InvalidOperationException e)
            {
                Check(e.Message == "Authentication failure journal denied." && e.InnerException is null, "payload-free refusal " + label);
            }
        }
        var now = new DateTimeOffset(2026, 10, 3, 14, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        var id = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var writer = new SessionSubject(id, Guid.NewGuid());
        var subject = new SessionSubject(Guid.NewGuid(), Guid.NewGuid());
        var binding = new SecurityAuditBindingV1(Guid.NewGuid(), "synthetic-journal", Guid.NewGuid(), writer, Guid.NewGuid());
        var anonymous = new AuthenticationFailureJournalV1(binding, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now,
            SecurityAuditActorKind.Anonymous, null, SecurityAuditAction.AuthenticationDenied, SecurityAuditOutcome.Denied,
            SecurityAuditReason.InvalidProtocol, null, null);
        var verified = anonymous with
        {
            ActorKind = SecurityAuditActorKind.Human,
            Subject = subject,
            Action = SecurityAuditAction.AuthenticationFailed,
            Outcome = SecurityAuditOutcome.Failed,
            SessionReference = Guid.NewGuid(),
            SecurityVersion = long.MaxValue
        };
        byte[] Encode(AuthenticationFailureJournalV1 value) => AuthenticationFailureJournalCodec.Serialize(value, binding, now);
        AuthenticationFailureJournalV1 Parse(byte[] bytes) => AuthenticationFailureJournalCodec.Parse(bytes, binding, now);
        byte[] Change(string key, JsonNode? value, byte[]? bytes = null)
        {
            var node = JsonNode.Parse(bytes ?? Encode(verified))!;
            node[key] = value;
            return Encoding.UTF8.GetBytes(node.ToJsonString());
        }
        foreach (var value in new[] { anonymous, verified, verified with { ActorKind = SecurityAuditActorKind.Workload },
            verified with { SessionReference = null, SecurityVersion = null }, verified with { SessionReference = null, SecurityVersion = 1 } })
        {
            var bytes = Encode(value);
            Check(Parse(bytes) == value, "closed anonymous/coherent verified roundtrip");
            Check(AuthenticationFailureJournalCodec.Digest(value, binding, now)
                == Convert.ToHexStringLower(SHA256.HashData(bytes)), "digest of exact canonical UTF-8");
            Check(Parse(bytes) == value && Encode(value).SequenceEqual(bytes), "stable serialization");
        }
        foreach (var reason in Enum.GetValues<SecurityAuditReason>())
            Check(Parse(Encode(anonymous with { Reason = reason })).Reason == reason, "preserve existing closed reason meaning");
        var canonical = Encode(verified);
        using (var doc = JsonDocument.Parse(canonical))
        {
            var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
            Check(names.Length == 17 && names.SequenceEqual(names.Order(StringComparer.Ordinal)), "exact ordinal root fields");
            Check(doc.RootElement.GetProperty("securityVersion").ValueKind == JsonValueKind.String
                && doc.RootElement.GetProperty("securityVersion").GetString() == long.MaxValue.ToString(), "signed64 canonical decimal string");
            Check(doc.RootElement.GetProperty("occurredAtUtc").GetString() == "2026-10-03T14:00:00.1234567Z", "exact original UTC ticks");
            foreach (var field in names)
            {
                var node = JsonNode.Parse(canonical)!;
                node.AsObject().Remove(field);
                Denied(() => Parse(Encoding.UTF8.GetBytes(node.ToJsonString())), "missing required " + field);
                var text = Encoding.UTF8.GetString(canonical);
                var property = doc.RootElement.GetProperty(field).GetRawText();
                var duplicate = text[..^1] + "," + JsonSerializer.Serialize(field) + ":" + property + "}";
                Denied(() => Parse(Encoding.UTF8.GetBytes(duplicate)), "duplicate required " + field);
            }
        }

        // AP-T02: banned fields, scalar coercion, aliases and canonical byte spellings.
        foreach (var field in new[] { "target", "previousSessionReference", "customerId", "projectId", "sequence", "head", "previousEventSha256",
            "authenticationEvidence", "token", "assertion", "code", "nonce", "state", "cookie", "ticket", "claims", "headers", "ip", "userAgent",
            "requestUrl", "query", "body", "email", "displayName", "exception", "evidence", "connectionString", "key" })
            Denied(() => Parse(Change(field, "synthetic-prohibited")), "prohibited field " + field);
        foreach (var field in new[] { "actorKind", "action", "outcome", "reason" })
        {
            Denied(() => Parse(Change(field, 0)), "numeric enum " + field);
            Denied(() => Parse(Change(field, "0")), "numeric string enum " + field);
            using var doc = JsonDocument.Parse(canonical);
            Denied(() => Parse(Change(field, doc.RootElement.GetProperty(field).GetString()!.ToLowerInvariant())), "enum case alias " + field);
            Denied(() => Parse(Change(field, "synthetic-prohibited")), "unknown enum " + field);
        }
        foreach (var value in new[] { "0", "-1", "+1", "01", "1.0", "1e1", "9223372036854775808", " 1" })
            Denied(() => Parse(Change("securityVersion", value)), "closed version string");
        Denied(() => Parse(Change("securityVersion", 1)), "JSON number version");
        foreach (var field in new[] { "applicationClientId", "correlationId", "eventId", "operationId", "sessionReference", "streamId", "writerBindingReference" })
        {
            Denied(() => Parse(Change(field, Guid.Empty.ToString("D"))), "empty ID " + field);
            Denied(() => Parse(Change(field, id.ToString("D").ToUpperInvariant())), "uppercase ID " + field);
            Denied(() => Parse(Change(field, id.ToString("N"))), "noncanonical ID " + field);
            Denied(() => Parse(Change(field, "synthetic-prohibited")), "prohibited scalar in ID " + field);
        }
        foreach (var field in new[] { "writer", "subject" })
        {
            var node = JsonNode.Parse(canonical)!;
            node[field]!["objectId"] = Guid.Empty.ToString("D");
            Denied(() => Parse(Encoding.UTF8.GetBytes(node.ToJsonString())), "empty nested object ID");
            node = JsonNode.Parse(canonical)!;
            node[field]!["tenantId"] = id.ToString("D").ToUpperInvariant();
            Denied(() => Parse(Encoding.UTF8.GetBytes(node.ToJsonString())), "uppercase nested tenant");
            node = JsonNode.Parse(canonical)!;
            node[field]!["extra"] = "synthetic-prohibited";
            Denied(() => Parse(Encoding.UTF8.GetBytes(node.ToJsonString())), "nested unknown field");
            var text = Encoding.UTF8.GetString(canonical);
            var nested = JsonNode.Parse(canonical)![field]!.ToJsonString();
            var dup = nested[..^1] + ",\"objectId\":\"" + id.ToString("D") + "\"}";
            Denied(() => Parse(Encoding.UTF8.GetBytes(text.Replace(nested, dup, StringComparison.Ordinal))), "nested duplicate");
        }
        foreach (var time in new[] { "2026-10-03T14:00:00Z", "2026-10-03T14:00:00.12345678Z", "2026-10-03T14:00:00.1234567+00:00",
            "2026-10-03t14:00:00.1234567Z", "2026-10-03T14:00:00.1234567z", "2026-10-03T14:00:00.1234568Z", "synthetic-prohibited" })
            Denied(() => Parse(Change("occurredAtUtc", time)), "invalid/noncanonical/future occurrence");
        var wire = Encoding.UTF8.GetString(canonical);
        foreach (var text in new[] { " " + wire, wire + "\n", wire.Replace("\"action\":", "\"action\" :", StringComparison.Ordinal),
            wire.Replace("Failed", "Fa\\u0069led", StringComparison.Ordinal), wire.Replace("\"action\"", "\"\\u0061ction\"", StringComparison.Ordinal),
            wire.Replace("authentication-failure-journal-v1", "other-schema", StringComparison.Ordinal), "[]", "null", "{}", wire[..^1],
            "{\"extra\":" + new string('[', 4) + "0" + new string(']', 4) + "}", wire.Replace("synthetic-journal", "synthetic-\\ud800") })
            Denied(() => Parse(Encoding.UTF8.GetBytes(text)), "noncanonical/invalid/deep byte corpus");
        var reversed = new JsonObject();
        foreach (var field in JsonNode.Parse(canonical)!.AsObject().Reverse()) reversed.Add(field.Key, field.Value?.DeepClone());
        Denied(() => Parse(Encoding.UTF8.GetBytes(reversed.ToJsonString())), "nonordinal root ordering");
        var invalidUtf8 = canonical.ToArray();
        var environmentIndex = wire.IndexOf("synthetic-journal", StringComparison.Ordinal);
        invalidUtf8[environmentIndex] = 0xc0; invalidUtf8[environmentIndex + 1] = 0xaf;
        Denied(() => Parse(invalidUtf8), "invalid UTF-8");
        Denied(() => Parse([]), "empty input");
        Denied(() => Parse(new byte[AuthenticationFailureJournalCodec.MaximumEncodedLength + 1]), "derived size exceeded");

        foreach (var swapped in new[] { binding with { StreamId = Guid.NewGuid() }, binding with { EnvironmentId = "other-environment" },
            binding with { WriterBindingReference = Guid.NewGuid() }, binding with { Writer = new(Guid.NewGuid(), writer.ObjectId) },
            binding with { Writer = new(writer.TenantId, Guid.NewGuid()) }, binding with { ApplicationClientId = Guid.NewGuid() } })
        {
            var swappedBytes = AuthenticationFailureJournalCodec.Serialize(verified with { Binding = swapped }, swapped, now);
            Denied(() => Parse(swappedBytes), "full expected trusted binding mismatch");
        }
        foreach (var invalid in new[] { anonymous with { Subject = subject }, anonymous with { SessionReference = Guid.NewGuid() },
            anonymous with { SecurityVersion = 1 }, verified with { Subject = null }, verified with { Subject = default(SessionSubject) },
            verified with { SecurityVersion = 0 }, verified with { SecurityVersion = null }, verified with { SessionReference = Guid.Empty },
            verified with { ActorKind = (SecurityAuditActorKind)99 }, verified with { Reason = (SecurityAuditReason)99 },
            verified with { Action = SecurityAuditAction.SessionIssued }, verified with { Outcome = SecurityAuditOutcome.Succeeded },
            verified with { Action = SecurityAuditAction.AuthenticationDenied }, verified with { OccurredAtUtc = now.AddTicks(1) },
            verified with { OccurredAtUtc = now.ToOffset(TimeSpan.FromHours(1)) } })
            Denied(() => Encode(invalid), "invalid coherent identity/terminal shape");
        Denied(() => AuthenticationFailureJournalCodec.Parse(canonical, binding, now.ToOffset(TimeSpan.FromHours(1))), "non-UTC trusted now");
        foreach (var invalid in new[] { binding with { StreamId = Guid.Empty }, binding with { ApplicationClientId = Guid.Empty },
            binding with { WriterBindingReference = Guid.Empty }, binding with { Writer = default }, binding with { EnvironmentId = "synthetic\n" },
            binding with { EnvironmentId = new string('a', 49) }, binding with { EnvironmentId = "Synthetic" } })
            Denied(() => AuthenticationFailureJournalCodec.Serialize(verified with { Binding = invalid }, invalid, now), "invalid writer binding");

        // AP-T01/T02: prove the derived maximum on a longest valid fully populated descriptor.
        var maxBinding = binding with { EnvironmentId = new string('a', 48) };
        var max = verified with { Binding = maxBinding, ActorKind = SecurityAuditActorKind.Workload, Reason = SecurityAuditReason.ProviderUnavailable };
        var maxBytes = AuthenticationFailureJournalCodec.Serialize(max, maxBinding, now);
        Check(maxBytes.Length == AuthenticationFailureJournalCodec.MaximumEncodedLength, "longest closed representation reaches derived maximum");
        Check(AuthenticationFailureJournalCodec.Parse(maxBytes, maxBinding, now) == max, "exact derived bound roundtrip");
        Denied(() => AuthenticationFailureJournalCodec.Parse(maxBytes.Append((byte)' ').ToArray(), maxBinding, now), "one byte past exact bound");

        // AP-T03: freeze original terminal metadata, never claim storage acknowledgment or upgrade identity.
        var clock = new JournalClock(now);
        var attempt = new AuthenticationFailureAttempt(binding, clock);
        clock.Now = now.AddMinutes(1);
        var first = attempt.Observe(SecurityAuditActorKind.Anonymous, null, SecurityAuditAction.AuthenticationFailed,
            SecurityAuditOutcome.Failed, SecurityAuditReason.InvalidProtocol);
        Check(first.OccurredAtUtc == clock.Now, "occurrence sampled at first terminal, not construction");
        var firstBytes = AuthenticationFailureJournalCodec.Serialize(first, binding, clock.Now);
        var firstDigest = AuthenticationFailureJournalCodec.Digest(first, binding, clock.Now);
        clock.Now = now.AddHours(1);
        var late = attempt.Observe(SecurityAuditActorKind.Human, subject, SecurityAuditAction.AuthenticationDenied,
            SecurityAuditOutcome.Denied, SecurityAuditReason.AuthorityDenied, Guid.NewGuid(), 2);
        Check(ReferenceEquals(first, late) && late.Subject is null && late.OccurredAtUtc == first.OccurredAtUtc,
            "late valid identity/time/outcome never upgrades original anonymous outcome");
        Check(AuthenticationFailureJournalCodec.Serialize(late, binding, clock.Now).SequenceEqual(firstBytes)
            && AuthenticationFailureJournalCodec.Digest(late, binding, clock.Now) == firstDigest, "immutable original bytes/digest");
        Denied(() => attempt.Observe(SecurityAuditActorKind.Anonymous, subject, SecurityAuditAction.AuthenticationFailed,
            SecurityAuditOutcome.Failed, SecurityAuditReason.None), "late invalid observer still validates");
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 128).Select(i => Task.Run(() => attempt.Observe(
            i % 2 == 0 ? SecurityAuditActorKind.Anonymous : SecurityAuditActorKind.Human, i % 2 == 0 ? null : subject,
            SecurityAuditAction.AuthenticationDenied, SecurityAuditOutcome.Denied, SecurityAuditReason.AuthorityDenied))));
        Check(concurrent.All(value => ReferenceEquals(value, first)), "concurrent overlap on frozen attempt returns one object");
        var racingAttempt = new AuthenticationFailureAttempt(binding, clock);
        var racing = await Task.WhenAll(Enumerable.Range(0, 128).Select(i => Task.Run(() => racingAttempt.Observe(
            i % 2 == 0 ? SecurityAuditActorKind.Anonymous : SecurityAuditActorKind.Human, i % 2 == 0 ? null : subject,
            SecurityAuditAction.AuthenticationFailed, SecurityAuditOutcome.Failed, SecurityAuditReason.ProviderUnavailable))));
        Check(racing.All(value => ReferenceEquals(value, racing[0])) && racing.Select(value =>
            AuthenticationFailureJournalCodec.Digest(value, binding, clock.Now)).Distinct().Count() == 1, "first-terminal concurrent race one descriptor/hash");
        var unfrozen = new AuthenticationFailureAttempt(binding, clock);
        Denied(() => unfrozen.Observe(SecurityAuditActorKind.Human, null, SecurityAuditAction.AuthenticationDenied,
            SecurityAuditOutcome.Denied, SecurityAuditReason.None), "invalid input cannot freeze attempt");
        clock.Now = now.AddHours(2);
        Check(unfrozen.Observe(SecurityAuditActorKind.Anonymous, null, SecurityAuditAction.AuthenticationDenied,
            SecurityAuditOutcome.Denied, SecurityAuditReason.None).OccurredAtUtc == clock.Now, "first valid terminal follows invalid observation");
        var distinct = Enumerable.Range(0, 32).Select(_ => new AuthenticationFailureAttempt(binding, clock).Observe(
            SecurityAuditActorKind.Anonymous, null, SecurityAuditAction.AuthenticationDenied, SecurityAuditOutcome.Denied, SecurityAuditReason.None)).ToArray();
        var ids = distinct.SelectMany(value => new[] { value.EventId, value.OperationId, value.CorrelationId }).ToArray();
        Check(ids.All(value => value != Guid.Empty) && ids.Distinct().Count() == ids.Length, "distinct server-owned attempt IDs");
        clock.Now = now.ToOffset(TimeSpan.FromHours(1));
        Denied(() => attempt.Observe(SecurityAuditActorKind.Anonymous, null, SecurityAuditAction.AuthenticationDenied,
            SecurityAuditOutcome.Denied, SecurityAuditReason.None), "late invalid trusted time denied before return");
        return checks;
    }

    private sealed class JournalClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
