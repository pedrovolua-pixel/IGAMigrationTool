using System.Security.Claims;
using System.Text;
using IdentitySessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

if (args is not ["--reset-synthetic-schema"])
    throw new InvalidOperationException("Explicit synthetic-schema reset argument required.");
var connectionString = Environment.GetEnvironmentVariable("IGA_SESSION_TEST_CONNECTION")
    ?? throw new InvalidOperationException("Synthetic PostgreSQL connection must be supplied.");
var settings = new NpgsqlConnectionStringBuilder(connectionString);
if (settings.Host is not ("127.0.0.1" or "localhost") ||
    settings.Database is null || !settings.Database.StartsWith("iga_synthetic_", StringComparison.Ordinal))
    throw new InvalidOperationException("Only an explicitly named loopback synthetic database is allowed.");
await using var database = NpgsqlDataSource.Create(connectionString);
await using var database2 = NpgsqlDataSource.Create(connectionString);
await using (var reset = database.CreateCommand("DROP SCHEMA IF EXISTS identity_sessions CASCADE"))
    await reset.ExecuteNonQueryAsync();
using (var migration = typeof(PostgreSqlTicketStore).Assembly.GetManifestResourceStream("IdentitySessions.001-initial.sql")!)
using (var reader = new StreamReader(migration))
await using (var command = database.CreateCommand(await reader.ReadToEndAsync()))
    await command.ExecuteNonQueryAsync();
var keysDirectory = Path.Combine(Path.GetTempPath(), "iga-session-test-keys-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(keysDirectory);
try
{
    var protection = DataProtectionProvider.Create(new DirectoryInfo(keysDirectory), c => c.SetApplicationName("SyntheticSessions"));
    var protection2 = DataProtectionProvider.Create(new DirectoryInfo(keysDirectory), c => c.SetApplicationName("SyntheticSessions"));
    var clock = new MutableClock();
    var eligibility = new Admission();
    var first = new PostgreSqlTicketStore(database, protection, clock, eligibility);
    var second = new PostgreSqlTicketStore(database2, protection2, clock, eligibility);
    var authority = new PostgreSqlSessionAuthority(database, clock);
    var tenant = Guid.NewGuid();
    var checks = 0;
    void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    async Task<SessionSubject> Subject()
    {
        var value = new SessionSubject(tenant, Guid.NewGuid());
        await authority.ProvisionAsync(value, true, clock.GetUtcNow());
        return value;
    }
    AuthenticationTicket Ticket(SessionSubject subject, long version = 1) => new(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim("tid", subject.TenantId.ToString()), new Claim("oid", subject.ObjectId.ToString()),
        new Claim("roles", "PilotConsultant")], "Cookie", "oid", "roles")), new AuthenticationProperties(new Dictionary<string, string?>
        {
            [SessionTicket.AuthenticatedUtc] = clock.GetUtcNow().ToString("O"),
            [SessionTicket.ProviderCheckedUtc] = clock.GetUtcNow().ToString("O"),
            [SessionTicket.SecurityVersion] = version.ToString(),
            [SessionTicket.MfaCaVerified] = "false"
        }), "Cookie");
    async Task Refused(Func<Task> action, string label)
    {
        try { await action(); throw new Exception(label); }
        catch (InvalidOperationException) { checks++; }
    }
    var subject = await Subject();
    var initial = Ticket(subject);
    var key = await first.StoreAsync(initial);
    var loaded = await second.RetrieveAsync(key);
    Check(loaded is not null, "Second store must retrieve persisted session with shared protected keys");
    var sessions = await authority.ListSessionsAsync(subject);
    Check(sessions.Count == 1 && loaded!.Properties.Items[SessionTicket.SessionReference] == sessions[0].Reference.ToString("D"), "Server reference must match persisted session");
    await using (var inspect = database.CreateCommand("SELECT key_hash,protected_ticket FROM identity_sessions.tickets WHERE tenant_id=$1 AND object_id=$2"))
    {
        inspect.Parameters.AddWithValue(subject.TenantId); inspect.Parameters.AddWithValue(subject.ObjectId);
        await using var row = await inspect.ExecuteReaderAsync(); await row.ReadAsync();
        Check(row.GetString(0).Trim() == SessionTicket.HashKey(key) && row.GetString(0).Trim() != key, "Raw cookie key must not be stored");
        Check(!Encoding.UTF8.GetString(row.GetFieldValue<byte[]>(1)).Contains("PilotConsultant", StringComparison.Ordinal), "Ticket payload must be protected");
    }
    var wrongKeyStore = new PostgreSqlTicketStore(database2, new EphemeralDataProtectionProvider(), clock, eligibility);
    Check(await wrongKeyStore.RetrieveAsync(key) is null, "Wrong protector must fail closed");
    Check(await second.RetrieveAsync(key + "A") is null, "Malformed identifier denied");
    var otherSubject = await Subject();
    Check(!await authority.RevokeSessionAsync(otherSubject, sessions[0].Reference), "Wrong subject cannot revoke session reference");
    eligibility.Allowed = false;
    Check(await second.RetrieveAsync(key) is null, "Current product eligibility must deny retrieval");
    await Refused(() => first.StoreAsync(Ticket(subject)), "Ineligible product context cannot create session");
    eligibility.Allowed = true;
    var changedMfa = TicketSerializer.Default.Deserialize(TicketSerializer.Default.Serialize(loaded!))!;
    changedMfa.Properties.Items[SessionTicket.MfaCaVerified] = "true";
    await Refused(() => second.RenewAsync(key, changedMfa), "Ordinary renewal cannot manufacture privileged context");
    var changedReference = TicketSerializer.Default.Deserialize(TicketSerializer.Default.Serialize(loaded!))!;
    changedReference.Properties.Items[SessionTicket.SessionReference] = Guid.NewGuid().ToString();
    await Refused(() => second.RenewAsync(key, changedReference), "Ordinary renewal cannot substitute session reference");
    await second.RenewAsync(key, loaded!);
    await first.RemoveAsync(key);
    await second.RenewAsync(key, loaded!);
    Check(await second.RetrieveAsync(key) is null, "Renewal must not revive removed key");
    key = await first.StoreAsync(Ticket(subject));
    var replacement = await second.RotateAsync(key, Ticket(subject));
    Check(replacement is not null && replacement != key && await first.RetrieveAsync(key) is null &&
        await first.RetrieveAsync(replacement) is not null, "Rotation invalidates old identifier across replicas");
    Check(await first.RotateAsync(key, Ticket(subject)) is null, "Old key cannot rotate twice");
    var current = (await authority.ListSessionsAsync(subject)).Single(s => !s.Revoked);
    await authority.RevokeSessionAsync(subject, current.Reference);
    Check(await second.RetrieveAsync(replacement!) is null, "Individual reference revocation immediate");

    // Provider status is authoritative and cannot be refreshed by ticket values.
    var staleSubject = await Subject();
    var staleKey = await first.StoreAsync(Ticket(staleSubject));
    clock.Advance(TimeSpan.FromMinutes(15));
    Check(await first.RetrieveAsync(staleKey) is null, "Provider status expires at exact 15-minute bound");
    await Refused(() => first.StoreAsync(Ticket(staleSubject)), "New ticket cannot manufacture DB provider freshness");
    await authority.ConfirmProviderAsync(staleSubject, clock.GetUtcNow());
    Check(await second.RetrieveAsync(staleKey) is not null, "Verified provider check may resume unexpired session");

    var idleSubject = await Subject(); var idleKey = await first.StoreAsync(Ticket(idleSubject));
    var beforeIdleAuthentication = clock.GetUtcNow();
    clock.Advance(TimeSpan.FromMinutes(30));
    await authority.ConfirmProviderAsync(idleSubject, clock.GetUtcNow());
    Check(await second.RetrieveAsync(idleKey) is null, "Exact idle boundary denied despite fresh provider");
    var staleRotation = Ticket(idleSubject);
    staleRotation.Properties.Items[SessionTicket.AuthenticatedUtc] = beforeIdleAuthentication.ToString("O");
    Check(await first.RotateAsync(idleKey, staleRotation) is null, "Idle-expired key cannot rotate using original authentication");
    Check(await second.RotateAsync(idleKey, Ticket(idleSubject)) is not null, "Trusted authentication after idle expiry can rotate to a new key");
    var absoluteSubject = await Subject(); var absoluteKey = await first.StoreAsync(Ticket(absoluteSubject));
    for (var i = 0; i < 31; i++)
    {
        clock.Advance(TimeSpan.FromMinutes(15));
        await authority.ConfirmProviderAsync(absoluteSubject, clock.GetUtcNow());
        Check(await (i % 2 == 0 ? first : second).RetrieveAsync(absoluteKey) is not null, "Activity should extend idle within absolute bound");
    }
    clock.Advance(TimeSpan.FromMinutes(15)); await authority.ConfirmProviderAsync(absoluteSubject, clock.GetUtcNow());
    Check(await first.RetrieveAsync(absoluteKey) is null, "Activity cannot extend exact eight-hour deadline");

    var versionSubject = await Subject(); var versionKey = await first.StoreAsync(Ticket(versionSubject));
    var nextVersion = await authority.RevokeSubjectAsync(versionSubject);
    Check(nextVersion == 2 && await second.RetrieveAsync(versionKey) is null, "Monotonic subject revoke across replicas");
    await Refused(() => first.StoreAsync(Ticket(versionSubject, 1)), "Old version cannot create session after revocation");
    versionKey = await second.StoreAsync(Ticket(versionSubject, 2));
    await authority.RevokeSubjectAsync(versionSubject, true);
    Check(await first.RetrieveAsync(versionKey) is null, "Disabled subject denied");
    await Refused(() => first.StoreAsync(Ticket(versionSubject, 3)), "Disabled subject cannot create fresh session");

    // A concurrent renew/remove race cannot resurrect the key.
    for (var i = 0; i < 12; i++)
    {
        var raceSubject = await Subject(); var raceKey = await first.StoreAsync(Ticket(raceSubject));
        var raceTicket = (await second.RetrieveAsync(raceKey))!;
        await Task.WhenAll(first.RemoveAsync(raceKey), second.RenewAsync(raceKey, raceTicket));
        Check(await first.RetrieveAsync(raceKey) is null, "Concurrent renewal/removal resurrected key");
    }
    for (var i = 0; i < 12; i++)
    {
        var raceSubject = await Subject(); var raceTicket = Ticket(raceSubject);
        var create = first.StoreAsync(raceTicket);
        var revoke = authority.RevokeSubjectAsync(raceSubject);
        string? raceKey = null;
        try { raceKey = await create; } catch (InvalidOperationException) { }
        await revoke;
        Check(raceKey is null || await second.RetrieveAsync(raceKey) is null, "Store race survived subject revocation");
    }
    for (var i = 0; i < 12; i++)
    {
        var raceSubject = await Subject(); var raceKey = await first.StoreAsync(Ticket(raceSubject));
        var rotate = second.RotateAsync(raceKey, Ticket(raceSubject));
        var revoke = authority.RevokeSubjectAsync(raceSubject);
        var rotatedKey = await rotate; await revoke;
        Check(await first.RetrieveAsync(raceKey) is null &&
            (rotatedKey is null || await second.RetrieveAsync(rotatedKey) is null), "Rotation race survived subject revocation");
    }
    var slowSubject = await Subject(); var slowKey = await first.StoreAsync(Ticket(slowSubject));
    eligibility.Delay = () => clock.Advance(TimeSpan.FromMinutes(15));
    Check(await second.RetrieveAsync(slowKey) is null, "Provider age must be rechecked after a slow eligibility lookup");
    eligibility.Delay = null;
    var slowCreateSubject = await Subject(); var slowCreateTicket = Ticket(slowCreateSubject);
    eligibility.Delay = () => clock.Advance(TimeSpan.FromMinutes(15));
    await Refused(() => first.StoreAsync(slowCreateTicket), "Slow issuance must not use stale authentication/provider timestamps");
    eligibility.Delay = null;
    Check((await authority.ListSessionsAsync(slowCreateSubject)).Count == 0, "Failed admission must not insert a session");
    var olderSubject = await Subject(); var olderTicket = Ticket(olderSubject);
    olderTicket.Properties.Items[SessionTicket.AuthenticatedUtc] = clock.GetUtcNow().AddHours(-1).ToString("O");
    var olderKey = await first.StoreAsync(olderTicket);
    var olderSession = (await authority.ListSessionsAsync(olderSubject)).Single();
    Check(olderSession.AbsoluteExpiresUtc == clock.GetUtcNow().AddHours(7), "Older ordinary authentication must keep its original deadline");
    var olderRotated = await second.RotateAsync(olderKey, olderTicket);
    Check(olderRotated is not null && (await authority.ListSessionsAsync(olderSubject)).Single(s => !s.Revoked).AbsoluteExpiresUtc == olderSession.AbsoluteExpiresUtc,
        "Rotation with unchanged authentication must not extend the absolute deadline");
    olderTicket.Properties.Items[SessionTicket.AuthenticatedUtc] = clock.GetUtcNow().AddHours(-8).ToString("O");
    await Refused(() => first.StoreAsync(olderTicket), "Absolute-expired authentication cannot issue a new session");
    var corruptSubject = await Subject(); var corruptKey = await first.StoreAsync(Ticket(corruptSubject));
    await using (var corrupt = database.CreateCommand("UPDATE identity_sessions.tickets SET protected_ticket=decode('00000000','hex') WHERE key_hash=$1"))
    { corrupt.Parameters.AddWithValue(SessionTicket.HashKey(corruptKey)!); await corrupt.ExecuteNonQueryAsync(); }
    Check(await second.RetrieveAsync(corruptKey) is null, "Corrupt ciphertext must fail closed");
    Console.WriteLine($"PASS: {checks} real PostgreSQL shared-ticket/context/lifetime/rotation/revocation/race checks.");
    Console.WriteLine("NOT VERIFIED: deployed Entra/provider verification, production data-protection key ring, product guest/assignment adapter, audit and complete BFF/G1 gates.");
}
finally
{
    Directory.Delete(keysDirectory, true);
}

sealed class MutableClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.Parse("2026-10-01T12:00:00+00:00");
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now += duration;
}
sealed class Admission : ISessionAdmissionPolicy
{
    public bool Allowed { get; set; } = true;
    public Action? Delay { get; set; }
    public ValueTask<bool> IsEligibleAsync(SessionSubject subject, CancellationToken cancellationToken)
    {
        Delay?.Invoke();
        return ValueTask.FromResult(Allowed);
    }
}
