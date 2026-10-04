using System.Security.Cryptography;
using System.Text;
using EvidenceGovernance;

using var signer = RSA.Create(2048);
using var otherSigner = RSA.Create(2048);
using var trustedKey = RSA.Create();
trustedKey.ImportParameters(signer.ExportParameters(false));

var artifact = Encoding.UTF8.GetBytes("synthetic artifact bytes");
var digest = Convert.ToHexString(SHA256.HashData(artifact));
var signature = signer.SignData(artifact, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

Check("valid digest and trusted signature", ArtifactIntegrityResult.Valid,
    ArtifactIntegrityVerifier.Verify(artifact, digest, signature, trustedKey));
Check("unsigned artifact", ArtifactIntegrityResult.InvalidInput,
    ArtifactIntegrityVerifier.Verify(artifact, digest, [], trustedKey));
Check("missing trusted key", ArtifactIntegrityResult.InvalidInput,
    ArtifactIntegrityVerifier.Verify(artifact, digest, signature, null));
Check("malformed digest", ArtifactIntegrityResult.InvalidInput,
    ArtifactIntegrityVerifier.Verify(artifact, "not-a-digest", signature, trustedKey));
Check("changed bytes", ArtifactIntegrityResult.DigestMismatch,
    ArtifactIntegrityVerifier.Verify(Encoding.UTF8.GetBytes("changed artifact bytes"), digest, signature, trustedKey));
Check("forged matching digest", ArtifactIntegrityResult.SignatureMismatch,
    ArtifactIntegrityVerifier.Verify(artifact, digest, otherSigner.SignData(artifact, HashAlgorithmName.SHA256, RSASignaturePadding.Pss), trustedKey));
Check("wrong digest", ArtifactIntegrityResult.DigestMismatch,
    ArtifactIntegrityVerifier.Verify(artifact, new string('0', 64), signature, trustedKey));

Console.WriteLine("7 artifact integrity checks passed.");
Console.WriteLine($"{GateCheckBundleChecks.Run()} signed gate-bundle checks passed.");
Console.WriteLine($"{GateCheckBundleWriterChecks.Run()} gate-bundle writer checks passed.");
Console.WriteLine($"{GateEvidenceRetentionChecks.Run()} gate-evidence retention clock checks passed.");
Console.WriteLine($"{GateSigningTrustChecks.Run()} signing trust registry checks passed.");
Console.WriteLine($"{PromotionMachineEvidenceChecks.Run()} promotion machine-evidence checks passed.");
Console.WriteLine($"{ReviewerDecisionChecks.Run()} reviewer decision bundle checks passed.");
Console.WriteLine($"{ReviewerTrustChecks.Run()} reviewer trust and role-coverage checks passed.");
Console.WriteLine($"{NormalPromotionEvidenceChecks.Run()} combined normal-promotion evidence checks passed.");
Console.WriteLine($"{PilotOwnerOverrideChecks.Run()} pilot-owner override evidence checks passed.");
Console.WriteLine($"{OwnerOverrideTrustChecks.Run()} pilot-owner key trust checks passed.");

static void Check(string caseName, ArtifactIntegrityResult expected, ArtifactIntegrityResult actual)
{
    if (actual != expected)
    {
        throw new Exception($"{caseName}: expected {expected}, received {actual}");
    }
}
