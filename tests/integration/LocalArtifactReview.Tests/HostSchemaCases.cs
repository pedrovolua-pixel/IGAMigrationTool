using System.Net;
using System.Text.Json.Nodes;

internal static class HostSchemaCases
{
    internal static async Task Restore()
    {
        DatabaseCases.Configure();
        await DatabaseCases.Sql("ALTER TABLE synthetic_fix_review.events ENABLE TRIGGER event_immutable");
        Check.Equal(await DatabaseCases.Scalar<string>("SELECT tgenabled::text FROM pg_trigger WHERE tgname='event_immutable' AND tgrelid='synthetic_fix_review.events'::regclass"), "O", "outer-owned-host-trigger-recovery-verified");
    }
    internal static async Task Run(Guid run)
    {
        DatabaseCases.Configure();
        Check.Equal(await DatabaseCases.Scalar<string>("SELECT tgenabled::text FROM pg_trigger WHERE tgname='event_immutable' AND tgrelid='synthetic_fix_review.events'::regclass"), "O", "owned-host-probe-original-trigger-enabled");
        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5183"), Timeout = TimeSpan.FromSeconds(30) };
        var route = $"/local-demo/v1/runs/{run:D}/analysis";
        var initial = await client.GetAsync(route);
        Check.Equal(initial.StatusCode, HttpStatusCode.OK, "owned-host-initial-current-source-readable");
        var initialBody = JsonNode.Parse(await initial.Content.ReadAsStringAsync())!;
        Check.Equal(initialBody["artifactReview"]!["status"]!.GetValue<string>(), "Ready", "owned-host-initial-overlay-ready");
        var disabled = false;
        try
        {
            await DatabaseCases.Sql("ALTER TABLE synthetic_fix_review.events DISABLE TRIGGER event_immutable");
            disabled = true;
            var denied = await client.GetAsync(route);
            Check.Equal(denied.StatusCode, HttpStatusCode.ServiceUnavailable, "pre-capture-schema-denial-is-503");
            var actual = JsonNode.Parse(await denied.Content.ReadAsStringAsync())!;
            var literal = JsonNode.Parse("{\"schemaVersion\":1,\"code\":\"Unavailable\",\"message\":\"The current artifact source could not be verified. Refresh the run.\",\"correlationId\":null,\"currentRevision\":null}");
            Check.Equal(Expected.Canonical(actual), Expected.Canonical(literal), "pre-capture-denial-closed-payload-free-no-stale-source");
        }
        finally
        {
            if (disabled) await DatabaseCases.Sql("ALTER TABLE synthetic_fix_review.events ENABLE TRIGGER event_immutable");
        }
        Check.Equal(await DatabaseCases.Scalar<string>("SELECT tgenabled::text FROM pg_trigger WHERE tgname='event_immutable' AND tgrelid='synthetic_fix_review.events'::regclass"), "O", "owned-host-original-trigger-restored");
        Check.Equal((await client.GetAsync(route)).StatusCode, HttpStatusCode.OK, "owned-host-restored-current-source-readable");
        Check.Group("AR13-T12 actual host pre-capture integrity denial");
    }
}
