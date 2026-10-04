namespace SyntheticAiValidation;

public enum PacketIssue
{
    InvalidInput,
    UnknownVersion,
    WrongScope,
    InvalidSource,
    InvalidEvidence,
    InvalidRule,
    DuplicateId
}

public sealed record PacketBuildResult(PacketIssue? Issue, SyntheticAiPacket? Packet)
{
    public bool Succeeded => Issue is null && Packet is not null;
}
