using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using SyntheticAiExecution;

namespace SyntheticEvaluationPopulationIntegration;

internal static class PopulationReference
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static byte[] Frame(AiScope scope, string kind, params string[] parts)
    {
        if (scope is null || parts is null || kind is not ("scope" or "environment" or "member" or "module" or "category" or "rule-version" or "model-prompt" or "confidence-band" or "version-binding"))
            throw new ArgumentException("Invalid native reference input.");
        var expected = kind switch { "scope" => 0, "rule-version" or "model-prompt" or "version-binding" => 2, _ => 1 };
        if (parts.Length != expected) throw new ArgumentException("Invalid native reference cardinality.");
        using var stream = new MemoryStream();
        stream.Write(Utf8.GetBytes("iga.synthetic-evaluation.native-reference.v1")); stream.WriteByte(0);
        void Size(uint length) { Span<byte> size = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(size, length); stream.Write(size); }
        void Field(string value) { ArgumentNullException.ThrowIfNull(value); var bytes = Utf8.GetBytes(value); Size(checked((uint)bytes.Length)); stream.Write(bytes); }
        Field(kind); Field(scope.CustomerId); Field(scope.ProjectId); Field(scope.EnvironmentId);
        Size(checked((uint)parts.Length)); foreach (var part in parts) Field(part);
        return stream.ToArray();
    }
    internal static string Encode(AiScope scope, string kind, params string[] parts) =>
        "synthetic-ref-" + Convert.ToHexStringLower(SHA256.HashData(Frame(scope, kind, parts)));
}

internal sealed class PopulationReferences(AiScope scope)
{
    private readonly Dictionary<string, string> nativeByReference = new(StringComparer.Ordinal);
    internal string Add(string kind, params string[] parts)
    {
        var bytes = PopulationReference.Frame(scope, kind, parts);
        var reference = "synthetic-ref-" + Convert.ToHexStringLower(SHA256.HashData(bytes));
        var native = Convert.ToHexString(bytes);
        if (nativeByReference.TryGetValue(reference, out var prior) && prior != native) throw new ArgumentException("Conflicting native reference.");
        nativeByReference[reference] = native;
        return reference;
    }
}
