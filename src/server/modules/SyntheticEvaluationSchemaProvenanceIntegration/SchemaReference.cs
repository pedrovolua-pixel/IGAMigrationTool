using System.Buffers.Binary;
using System.Text;
using SyntheticAiExecution;

namespace SyntheticEvaluationSchemaProvenanceIntegration;

internal static class SchemaReference
{
    internal static string Encode(AiScope scope, string valueJson) => Native(scope, "version-binding", "AiSchema", valueJson);
    internal static string Native(AiScope scope, string kind, params string[] parts)
    {
        var expected = kind switch { "scope" => 0, "environment" => 1, "version-binding" => 2, _ => throw new ArgumentException("Invalid reference kind.") };
        if (parts.Length != expected) throw new ArgumentException("Invalid reference cardinality.");
        var utf8 = new UTF8Encoding(false, true);
        using var stream = new MemoryStream(); stream.Write(utf8.GetBytes("iga.synthetic-evaluation.native-reference.v1")); stream.WriteByte(0);
        void Size(uint value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(bytes, value); stream.Write(bytes); }
        void Field(string value) { var bytes = utf8.GetBytes(value); Size(checked((uint)bytes.Length)); stream.Write(bytes); }
        Field(kind); Field(scope.CustomerId); Field(scope.ProjectId); Field(scope.EnvironmentId); Size(checked((uint)parts.Length));
        foreach (var part in parts) Field(part);
        return "synthetic-ref-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream.ToArray()));
    }
}
