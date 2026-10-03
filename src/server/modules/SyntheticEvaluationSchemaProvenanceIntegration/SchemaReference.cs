using System.Buffers.Binary;
using System.Text;
using SyntheticAiExecution;

namespace SyntheticEvaluationSchemaProvenanceIntegration;

internal static class SchemaReference
{
    internal static string Encode(AiScope scope, string valueJson)
    {
        var utf8 = new UTF8Encoding(false, true);
        using var stream = new MemoryStream(); stream.Write(utf8.GetBytes("iga.synthetic-evaluation.native-reference.v1")); stream.WriteByte(0);
        void Size(uint value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(bytes, value); stream.Write(bytes); }
        void Field(string value) { var bytes = utf8.GetBytes(value); Size(checked((uint)bytes.Length)); stream.Write(bytes); }
        Field("version-binding"); Field(scope.CustomerId); Field(scope.ProjectId); Field(scope.EnvironmentId); Size(2); Field("AiSchema"); Field(valueJson);
        return "synthetic-ref-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream.ToArray()));
    }
}
