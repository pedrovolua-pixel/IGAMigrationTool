using System.Collections.Immutable;
using System.Security.Claims;
using IdentityAuthority;
using IdentityPolicy;
using IdentitySessions;
using Microsoft.AspNetCore.Authentication;
using Npgsql;
using NpgsqlTypes;

sealed class Clock : TimeProvider
{
    // PostgreSQL timestamptz stores microseconds; frozen authority observations
    // must not round forward beyond the clock used by fail-closed admission.
    private long ticks = PostgreSqlTicks(DateTimeOffset.UtcNow.AddSeconds(-1));
    private static long PostgreSqlTicks(DateTimeOffset value) => value.UtcTicks / 10 * 10;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    public void Advance(TimeSpan span) => Interlocked.Add(ref ticks, span.Ticks);
    public void SampleWallClock() => Interlocked.Exchange(ref ticks, PostgreSqlTicks(DateTimeOffset.UtcNow.AddMilliseconds(-100)));
}

sealed class Fixture : IAsyncDisposable
{
    public NpgsqlDataSource Operator { get; }
    public NpgsqlDataSource Administrator { get; }
    public NpgsqlDataSource Publisher { get; }
    public NpgsqlDataSource Runtime { get; }
    public Clock Clock { get; } = new();
    public SessionSubject Actor { get; } = new(Guid.NewGuid(), Guid.NewGuid());
    public HumanScope Scope { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    public ProviderRoleBindingV1 Roles { get; }
    public PostgreSqlSecurityAudit Audit { get; }
    public PostgreSqlIdentityAuthority AdminAuthority { get; }
    public PostgreSqlIdentityAuthority ProviderAuthority { get; }
    public PostgreSqlIdentityAuthority RuntimeAuthority { get; }

    public Fixture(string supplied)
    {
        var settings = new NpgsqlConnectionStringBuilder(supplied);
        if (settings.Host != "127.0.0.1" || settings.Database != "iga_synthetic_bff_composition")
            throw new InvalidOperationException("Dedicated 127.0.0.1 iga_synthetic_bff_composition database required.");
        settings.CommandTimeout = 10;
        Operator = NpgsqlDataSource.Create(settings.ConnectionString);
        NpgsqlDataSource As(string username)
        {
            var restricted = new NpgsqlConnectionStringBuilder(settings.ConnectionString) { Username = username, Password = "" };
            return NpgsqlDataSource.Create(restricted.ConnectionString);
        }
        Administrator = As("icom_admin"); Publisher = As("icom_provider"); Runtime = As("icom_runtime");
        Roles = new("provider-role-binding-v1", Actor.TenantId, Guid.NewGuid(), Guid.NewGuid(),
            Enum.GetValues<CoarseAppRole>().Select(r => new ProviderAppRoleV1(Guid.NewGuid(), r, true)).ToImmutableArray(),
            1, new string('a', 64), Guid.NewGuid());
        var binding = new SecurityAuditBindingV1(Guid.NewGuid(), "composition-local", Guid.NewGuid(), Actor, Roles.ClientId);
        Audit = new(binding, Clock);
        AdminAuthority = new(Administrator, Audit, Roles, Clock);
        ProviderAuthority = new(Publisher, Audit, Roles, Clock);
        RuntimeAuthority = new(Runtime, Audit, Roles, Clock);
    }

    public static async Task Sql(NpgsqlDataSource source, string sql, params object[] parameters)
    {
        await using var command = source.CreateCommand(sql);
        foreach (var value in parameters) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync();
    }
    public async Task<long> Number(string sql)
    { await using var command = Operator.CreateCommand(sql); return Convert.ToInt64(await command.ExecuteScalarAsync()); }

    public async Task Install()
    {
        await Sql(Operator, "DROP SCHEMA IF EXISTS identity_authority CASCADE; DROP SCHEMA IF EXISTS security_audit CASCADE; DROP SCHEMA IF EXISTS identity_sessions CASCADE");
        foreach (var name in new[] { "001-initial.sql", "002-authentication-context.sql", "003-atomic-audit.sql" })
        {
            using var resource = typeof(PostgreSqlTicketStore).Assembly.GetManifestResourceStream("IdentitySessions." + name)
                ?? throw new InvalidOperationException("Required audit migration resource absent.");
            using var reader = new StreamReader(resource); await Sql(Operator, await reader.ReadToEndAsync());
        }
        using (var resource = typeof(PostgreSqlIdentityAuthority).Assembly.GetManifestResourceStream("IdentityAuthority.001-authority.sql")!)
        using (var reader = new StreamReader(resource)) await Sql(Operator, await reader.ReadToEndAsync());
        await Sql(Operator, """
            DO $$ BEGIN
              IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='icom_owner') THEN CREATE ROLE icom_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
              IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='icom_admin') THEN CREATE ROLE icom_admin LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
              IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='icom_provider') THEN CREATE ROLE icom_provider LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
              IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='icom_runtime') THEN CREATE ROLE icom_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
            END $$;
            GRANT USAGE ON SCHEMA identity_authority,identity_sessions,security_audit TO icom_owner,icom_admin,icom_provider,icom_runtime;
            GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA identity_authority,identity_sessions,security_audit TO icom_owner;
            GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA identity_authority,identity_sessions,security_audit TO icom_owner;
            DO $$ DECLARE f record; BEGIN
              FOR f IN SELECT p.oid::regprocedure signature FROM pg_proc p JOIN pg_namespace n ON p.pronamespace=n.oid WHERE n.nspname IN ('identity_authority','security_audit') LOOP
                EXECUTE 'ALTER FUNCTION '||f.signature||' OWNER TO icom_owner';
              END LOOP;
            END $$;
            DO $$ DECLARE f record; BEGIN
              FOR f IN SELECT p.oid::regprocedure signature FROM pg_proc p JOIN pg_namespace n ON p.pronamespace=n.oid
                WHERE n.nspname='security_audit' AND p.proname IN ('lock_head','append_event','read_receipt','append_receipt') LOOP
                EXECUTE 'GRANT EXECUTE ON FUNCTION '||f.signature||' TO icom_admin,icom_provider,icom_runtime';
              END LOOP;
              FOR f IN SELECT p.oid::regprocedure signature FROM pg_proc p JOIN pg_namespace n ON p.pronamespace=n.oid
                WHERE n.nspname='security_audit' AND p.proname IN ('lock_subject','lookup_ticket_subject','lock_ticket','touch_ticket','issue_ticket','revoke_ticket') LOOP
                EXECUTE 'GRANT EXECUTE ON FUNCTION '||f.signature||' TO icom_runtime';
              END LOOP;
            END $$;
            GRANT EXECUTE ON FUNCTION identity_authority.lock_subject(uuid,uuid) TO icom_admin,icom_provider,icom_runtime;
            GRANT EXECUTE ON FUNCTION identity_authority.apply_command(jsonb) TO icom_admin;
            GRANT EXECUTE ON FUNCTION identity_authority.publish_provider(jsonb,jsonb,uuid,text) TO icom_provider;
            """);
        await Sql(Operator, "INSERT INTO identity_authority.writer_bindings VALUES('icom_admin','Administrator',$1,$2),('icom_provider','Provider',$1,$2),('icom_runtime','Reader',$1,$2)", Actor.TenantId, Actor.ObjectId);
        await Sql(Operator, "INSERT INTO identity_authority.customers VALUES($1)", Scope.CustomerId);
        await Sql(Operator, "INSERT INTO identity_authority.projects VALUES($1,$2)", Scope.CustomerId, Scope.ProjectId);
        await Sql(Operator, "INSERT INTO identity_authority.environments VALUES($1,$2,$3)", Scope.CustomerId, Scope.ProjectId, Scope.EnvironmentId);
        await Sql(Operator, "INSERT INTO identity_authority.assessments VALUES($1,$2,$3,$4)", Scope.CustomerId, Scope.ProjectId, Scope.EnvironmentId, Scope.AssessmentId);
        await using (var binding = Operator.CreateCommand("INSERT INTO identity_authority.role_bindings VALUES($1,$2)"))
        { binding.Parameters.AddWithValue(Actor.TenantId); binding.Parameters.AddWithValue(NpgsqlDbType.Jsonb, AuthorityCodec.Serialize(Roles)); await binding.ExecuteNonQueryAsync(); }
        var b = Audit.Binding;
        await Sql(Operator, "INSERT INTO security_audit.streams(stream_id,environment_id,writer_binding_reference,writer_tenant_id,writer_object_id,application_client_id) VALUES($1,$2,$3,$4,$5,$6)", b.StreamId, b.EnvironmentId, b.WriterBindingReference, Actor.TenantId, Actor.ObjectId, b.ApplicationClientId);
        await Sql(Operator, "INSERT INTO security_audit.writer_roles VALUES($1,'icom_admin',$2,ARRAY['AuthorityChanged','SubjectRevoked']),($1,'icom_provider',$2,ARRAY['AuthorityChanged']),($1,'icom_runtime',$2,ARRAY['SessionIssued','SessionRevoked','SessionRotated','AuthenticationDenied','AuthenticationFailed'])", b.StreamId, b.WriterBindingReference);
    }

    public AuthorityCommandV1 Command(SessionSubject subject, long revision, AuthorityOperation operation,
        SubjectEnrollmentV1? enrollment = null, HumanAssignmentV1? assignment = null, GuestLifecycleV1? guest = null)
    {
        var attribution = enrollment?.Attribution ?? assignment?.Attribution ?? guest?.Attribution ?? Attribution();
        var value = new AuthorityCommandV1("authority-command-v1", new("authority-decision-v1", Guid.NewGuid(), revision,
            subject, assignment?.Scope, operation, attribution.ApprovedDecisionId, AuthorityReason.ApprovedOnboarding, Actor, new string('0', 64)), enrollment, assignment, guest);
        return value with { Decision = value.Decision with { PayloadSha256 = AuthorityCodec.PayloadDigest(value) } };
    }
    public DecisionAttributionV1 Attribution() => new(Guid.NewGuid(), Actor, Clock.GetUtcNow());
    public async Task<SessionSubject> Enroll(bool external = false, SessionSubject? sponsor = null, bool publish = true)
    {
        var subject = new SessionSubject(Actor.TenantId, Guid.NewGuid());
        var enrollment = new SubjectEnrollmentV1("subject-enrollment-v1", subject, 1, EnrollmentLifecycle.Pending,
            external ? OrganizationalOrigin.ExternalOrganizational : OrganizationalOrigin.InternalOrganizational,
            external ? Guid.NewGuid() : Actor.TenantId, Clock.GetUtcNow(), Attribution());
        await AdminAuthority.ExecuteAsync(Command(subject, 0, AuthorityOperation.EnrollPending, enrollment));
        await AdminAuthority.ExecuteAsync(Command(subject, 1, AuthorityOperation.ActivateEnrollment, enrollment with { Revision = 2, Lifecycle = EnrollmentLifecycle.Active }));
        var assignment = new HumanAssignmentV1("human-assignment-v1", Guid.NewGuid(), subject, Scope, HumanRole.Consultant,
            true, Clock.GetUtcNow().AddHours(-1), Clock.GetUtcNow().AddHours(2), ["normalized"], [], 1, Attribution());
        await AdminAuthority.ExecuteAsync(Command(subject, 2, AuthorityOperation.SetAssignment, assignment: assignment));
        if (external)
        {
            var guest = new GuestLifecycleV1("guest-lifecycle-v1", subject, sponsor ?? throw new InvalidOperationException("Sponsor required."),
                Clock.GetUtcNow(), Clock.GetUtcNow().AddDays(90), Clock.GetUtcNow(), Guid.NewGuid(), 1, false, Attribution());
            await AdminAuthority.ExecuteAsync(Command(subject, 3, AuthorityOperation.ApproveExternalLifecycle, guest: guest));
        }
        if (publish) await Publish(subject);
        return subject;
    }
    public async Task<ProviderReadResultV1> Observation(SessionSubject subject, DateTimeOffset? resourceCutoff = null, DateTimeOffset? homeCutoff = null)
    {
        var snapshot = await RuntimeAuthority.ReadAsync(subject) ?? throw new InvalidOperationException("Snapshot absent.");
        var p = new ProviderObservationV1("provider-observation-v1", subject, Roles.ClientId, Roles.ResourceServicePrincipalId,
            (snapshot.Provider?.Sequence ?? 0) + 1, Clock.GetUtcNow(), Clock.GetUtcNow(), snapshot.Enrollment.Revision,
            snapshot.SecurityVersion, subject.ObjectId, true, ProviderUserType.Member,
            snapshot.Enrollment.Origin == OrganizationalOrigin.ExternalOrganizational ? InvitationState.Accepted : null,
            resourceCutoff ?? DateTimeOffset.UnixEpoch, [Roles.AppRoles.Single(r => r.Role == CoarseAppRole.PilotConsultant).AppRoleId], true, Guid.NewGuid());
        var home = snapshot.Enrollment.Origin == OrganizationalOrigin.ExternalOrganizational
            ? new HomeStatusEvidenceV1(subject, snapshot.Enrollment.HomeTenantId, snapshot.Enrollment.Revision, snapshot.SecurityVersion,
                Clock.GetUtcNow(), homeCutoff ?? DateTimeOffset.UnixEpoch, true, true) : null;
        return new(p, home);
    }
    public async Task Publish(SessionSubject subject, DateTimeOffset? resourceCutoff = null, DateTimeOffset? homeCutoff = null)
    { _ = await ProviderAuthority.PublishAsync(await Observation(subject, resourceCutoff, homeCutoff), Guid.NewGuid()); }
    public AuthenticationTicket Ticket(SessionSubject subject, long version, DateTimeOffset? auth = null) => new(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("tid", subject.TenantId.ToString("D")), new Claim("oid", subject.ObjectId.ToString("D")), new Claim("roles", "PilotConsultant")], "Cookie", "oid", "roles")),
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [SessionTicket.AuthenticatedUtc] = (auth ?? Clock.GetUtcNow()).ToString("O"),
            [SessionTicket.ProviderCheckedUtc] = Clock.GetUtcNow().ToString("O"),
            [SessionTicket.SecurityVersion] = version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [SessionTicket.MfaCaVerified] = "false"
        }), "Cookie");
    public async Task<long> Version(SessionSubject subject) => (await RuntimeAuthority.ReadAsync(subject))!.SecurityVersion;
    public async ValueTask DisposeAsync()
    { await Runtime.DisposeAsync(); await Publisher.DisposeAsync(); await Administrator.DisposeAsync(); await Operator.DisposeAsync(); }
}
