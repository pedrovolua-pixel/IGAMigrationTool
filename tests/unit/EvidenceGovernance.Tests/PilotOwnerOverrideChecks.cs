using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using EvidenceGovernance;

internal static class PilotOwnerOverrideChecks
{
    public static int Run()
    {
        using var owner = RSA.Create(2048);
        using var other = RSA.Create(2048);
        using var trusted = RSA.Create();
        trusted.ImportParameters(owner.ExportParameters(false));

        var now = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
        var rationale = Encoding.UTF8.GetBytes("Synthetic reason, known risk and compensating controls.");
        var artifactDigest = Convert.ToHexString(SHA256.HashData("synthetic"u8));
        var expected = new OwnerOverrideMetadata(artifactDigest, "SCOPE-A", "OWNER-A",
            "CUSTOMER-A", "ENV-A", "CAPABILITY-A", ["G1", "SME-REVIEW"],
            "PAUSE-NEW-WORK", now.AddMinutes(-10), now.AddHours(1));
        var bundle = PilotOwnerOverrideBundle.Create(expected, rationale);
        var pilotEnd = now.AddDays(1);
        var count = 0;

        void Check(string name, OwnerOverrideResult expectedResult, byte[] bytes,
            OwnerOverrideMetadata? expectedMetadata = null, byte[]? reason = null,
            RSA? signingKey = null, DateTimeOffset? end = null, DateTimeOffset? at = null)
        {
            signingKey ??= owner;
            var signature = signingKey.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            var result = PilotOwnerOverrideBundle.Verify(bytes,
                Convert.ToHexString(SHA256.HashData(bytes)), signature, trusted,
                reason ?? rationale, expectedMetadata ?? expected, end ?? pilotEnd, at ?? now);
            if (result != expectedResult)
            {
                throw new Exception($"{name}: expected {expectedResult}, received {result}.");
            }

            count++;
        }

        byte[] Changed(Action<JsonNode> mutate)
        {
            var node = JsonNode.Parse(bundle)!;
            mutate(node);
            return Encoding.UTF8.GetBytes(node.ToJsonString());
        }

        Check("valid exact override", OwnerOverrideResult.ValidForOperatorReview, bundle);
        Check("wrong signer", OwnerOverrideResult.Untrusted, bundle, signingKey: other);
        Check("wrong rationale", OwnerOverrideResult.WrongScope, bundle,
            reason: Encoding.UTF8.GetBytes("different rationale"));
        Check("wrong artifact", OwnerOverrideResult.WrongScope, bundle,
            expectedMetadata: expected with { ArtifactSha256 = new string('A', 64) });
        Check("wrong customer", OwnerOverrideResult.WrongScope, bundle,
            expectedMetadata: expected with { CustomerId = "CUSTOMER-B" });
        Check("wrong environment", OwnerOverrideResult.WrongScope, bundle,
            expectedMetadata: expected with { EnvironmentId = "ENV-B" });
        Check("wrong capability", OwnerOverrideResult.WrongScope, bundle,
            expectedMetadata: expected with { CapabilityId = "CAPABILITY-B" });
        Check("wrong scope", OwnerOverrideResult.WrongScope, bundle,
            expectedMetadata: expected with { ScopeId = "SCOPE-B" });
        Check("wrong owner", OwnerOverrideResult.WrongScope, bundle,
            expectedMetadata: expected with { OwnerId = "OWNER-B" });
        Check("wrong waiver", OwnerOverrideResult.WrongScope, bundle,
            expectedMetadata: expected with { WaivedChecks = ["G1"] });
        Check("wrong suspension target", OwnerOverrideResult.WrongScope, bundle,
            expectedMetadata: expected with { SuspensionTargetId = "OTHER-TARGET" });
        Check("expired decision", OwnerOverrideResult.Expired, bundle, at: now.AddHours(1));
        Check("after pilot end", OwnerOverrideResult.Expired, bundle, end: now.AddMinutes(30));
        Check("future decision", OwnerOverrideResult.Expired, bundle, at: now.AddMinutes(-11));
        Check("extra field", OwnerOverrideResult.InvalidSchema,
            Changed(node => node["riskText"] = "unexpected"));
        Check("duplicate field", OwnerOverrideResult.InvalidSchema,
            Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bundle).Replace(
                "\"ownerId\":\"OWNER-A\"", "\"ownerId\":\"OWNER-A\",\"ownerId\":\"OWNER-A\"", StringComparison.Ordinal)));
        Check("unordered waiver", OwnerOverrideResult.InvalidSchema,
            Changed(node => node["waivedChecks"] = new JsonArray("SME-REVIEW", "G1")));
        Check("duplicate waiver", OwnerOverrideResult.InvalidSchema,
            Changed(node => node["waivedChecks"] = new JsonArray("G1", "G1")));
        Check("wrong schema version", OwnerOverrideResult.InvalidSchema,
            Changed(node => node["schemaVersion"] = 2));
        Check("non UTC expiry", OwnerOverrideResult.InvalidSchema,
            Changed(node => node["expiresAt"] = "2026-09-29T13:00:00-04:00"));
        Check("arbitrary IP field", OwnerOverrideResult.InvalidSchema,
            Changed(node => node["customerId"] = "customer.example"));

        if (PilotOwnerOverrideBundle.Verify(bundle, Convert.ToHexString(SHA256.HashData(bundle)),
                [], trusted, rationale, expected, pilotEnd, now) != OwnerOverrideResult.Untrusted)
        {
            throw new Exception("Unsigned owner override was accepted.");
        }

        count++;
        var tampered = (byte[])bundle.Clone();
        tampered[^2] ^= 1;
        if (PilotOwnerOverrideBundle.Verify(tampered, Convert.ToHexString(SHA256.HashData(bundle)),
                owner.SignData(bundle, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
                trusted, rationale, expected, pilotEnd, now) != OwnerOverrideResult.Untrusted)
        {
            throw new Exception("Tampered owner override was accepted.");
        }

        count++;
        if (PilotOwnerOverrideBundle.Verify(bundle, Convert.ToHexString(SHA256.HashData(bundle)),
                owner.SignData(bundle, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
                trusted, [], expected, pilotEnd, now) != OwnerOverrideResult.InvalidInput)
        {
            throw new Exception("Missing rationale was accepted.");
        }

        count++;
        return count;
    }
}
