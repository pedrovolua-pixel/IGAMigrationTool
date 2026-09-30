using System.Security.Cryptography;
using System.Text;
using EvidenceGovernance;

internal static class OwnerOverrideTrustChecks
{
    public static int Run()
    {
        using var signer = RSA.Create(2048);
        using var other = RSA.Create(2048);
        var publicKey = signer.ExportSubjectPublicKeyInfo();
        var now = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
        var metadata = new OwnerOverrideMetadata(new string('A', 64), "SCOPE-A",
            "OWNER-A", "CUSTOMER-A", "ENV-A", "CAPABILITY-A", ["G1"],
            "PAUSE-NEW-WORK", now.AddMinutes(-5), now.AddHours(1));
        var rationale = Encoding.UTF8.GetBytes("Synthetic reason and known risk.");
        var bundle = PilotOwnerOverrideBundle.Create(metadata, rationale);
        var digest = Convert.ToHexString(SHA256.HashData(bundle));
        var signature = signer.SignData(bundle, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var entry = new TrustedOwnerOverrideKey("OWNER-KEY-A", publicKey, "OWNER-A",
            ["SCOPE-A"], now.AddDays(-1), now.AddDays(1), null);
        var registry = new OwnerOverrideTrustRegistry([entry]);
        var count = 0;

        void Expect(string name, OwnerOverrideResult expected, OwnerOverrideResult actual)
        {
            if (actual != expected)
            {
                throw new Exception($"{name}: expected {expected}, received {actual}.");
            }

            count++;
        }

        OwnerOverrideResult Verify(OwnerOverrideTrustRegistry trust, string keyId,
            OwnerOverrideMetadata? expected = null, byte[]? signed = null) =>
            trust.VerifyOverride(keyId, bundle, digest, signed ?? signature, rationale,
                expected ?? metadata, now.AddDays(1), now);

        void Reject(string name, Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                count++;
                return;
            }

            throw new Exception($"{name}: invalid trust entry was accepted.");
        }

        Expect("trusted owner", OwnerOverrideResult.ValidForOperatorReview,
            Verify(registry, "OWNER-KEY-A"));
        Expect("unknown key", OwnerOverrideResult.Untrusted,
            Verify(registry, "UNKNOWN-KEY"));
        Expect("wrong owner identity", OwnerOverrideResult.Untrusted,
            Verify(registry, "OWNER-KEY-A", metadata with { OwnerId = "OWNER-B" }));
        Expect("wrong scope", OwnerOverrideResult.Untrusted,
            Verify(registry, "OWNER-KEY-A", metadata with { ScopeId = "SCOPE-B" }));
        Expect("wrong signature", OwnerOverrideResult.Untrusted,
            Verify(registry, "OWNER-KEY-A", signed: other.SignData(bundle,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
        Expect("revoked key", OwnerOverrideResult.Untrusted,
            Verify(new OwnerOverrideTrustRegistry([entry with { RevokedAt = now.AddMinutes(-1) }]),
                "OWNER-KEY-A"));
        Expect("future revocation", OwnerOverrideResult.ValidForOperatorReview,
            Verify(new OwnerOverrideTrustRegistry([entry with { RevokedAt = now.AddMinutes(1) }]),
                "OWNER-KEY-A"));
        Expect("decision outside signing window", OwnerOverrideResult.Untrusted,
            Verify(new OwnerOverrideTrustRegistry([entry with { ValidFrom = now }]),
                "OWNER-KEY-A"));

        Reject("duplicate key ID", () => new OwnerOverrideTrustRegistry([entry, entry]));
        Reject("duplicate public key", () => new OwnerOverrideTrustRegistry([
            entry, entry with { KeyId = "OWNER-KEY-B" }
        ]));
        Reject("invalid public key", () => new OwnerOverrideTrustRegistry([
            entry with { SubjectPublicKeyInfo = [1, 2, 3] }
        ]));
        Reject("duplicate scope", () => new OwnerOverrideTrustRegistry([
            entry with { AllowedScopes = ["SCOPE-A", "SCOPE-A"] }
        ]));

        Array.Fill(publicKey, (byte)0);
        Expect("public key snapshot", OwnerOverrideResult.ValidForOperatorReview,
            Verify(registry, "OWNER-KEY-A"));
        return count;
    }
}
