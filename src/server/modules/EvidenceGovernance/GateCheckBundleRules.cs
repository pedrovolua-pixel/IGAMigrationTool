namespace EvidenceGovernance;

internal static class GateCheckBundleRules
{
    internal const int MaximumBundleBytes = 64 * 1024;

    internal static bool IsGate(string? value) => value is "G1" or "G2" or "G3" or "G4" or
        "G5" or "G6" or "G7" or "G8" or "G9";

    internal static bool IsHex(string? value, int length) => value is not null && value.Length == length &&
        value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');

    internal static bool IsCheckId(string? value) => value is not null && value.Length is >= 2 and <= 64 &&
        value[0] is >= 'A' and <= 'Z' &&
        value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-');
}
