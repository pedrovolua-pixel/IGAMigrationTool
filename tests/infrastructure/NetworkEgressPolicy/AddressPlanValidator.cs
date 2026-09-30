using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

internal static class AddressPlanValidator
{
    public static bool IsValid(string? virtualNetwork, string? containerAppsSubnet,
        string? privateEndpointSubnet)
    {
        if (!TryParse(virtualNetwork, out var vnet) ||
            !TryParse(containerAppsSubnet, out var apps) ||
            !TryParse(privateEndpointSubnet, out var endpoints) ||
            apps.Prefix > 27 || !IsPrivate(vnet) ||
            Overlaps(vnet, new Cidr(0xAC1E0000, 0xAC1EFFFF, 16)) ||
            Overlaps(vnet, new Cidr(0xAC1F0000, 0xAC1FFFFF, 16)) ||
            !Contains(vnet, apps) || !Contains(vnet, endpoints) ||
            Overlaps(apps, endpoints))
        {
            return false;
        }

        return true;
    }

    private static bool TryParse(string? text, out Cidr cidr)
    {
        cidr = default;
        if (text is null)
        {
            return false;
        }

        var parts = text.Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address) ||
            address.AddressFamily != AddressFamily.InterNetwork ||
            !int.TryParse(parts[1], out var prefix) || prefix is < 1 or > 32 ||
            parts[1] != prefix.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
            parts[0] != address.ToString())
        {
            return false;
        }

        var value = BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes());
        var mask = uint.MaxValue << (32 - prefix);
        if ((value & ~mask) != 0)
        {
            return false;
        }

        cidr = new Cidr(value, value | ~mask, prefix);
        return true;
    }

    private static bool IsPrivate(Cidr cidr) =>
        InRange(cidr, 0x0A000000, 0x0AFFFFFF) ||
        InRange(cidr, 0xAC100000, 0xAC1FFFFF) ||
        InRange(cidr, 0xC0A80000, 0xC0A8FFFF);

    private static bool InRange(Cidr cidr, uint first, uint last) =>
        cidr.Start >= first && cidr.End <= last;

    private static bool Contains(Cidr outer, Cidr inner) =>
        inner.Start >= outer.Start && inner.End <= outer.End;

    private static bool Overlaps(Cidr left, Cidr right) =>
        left.Start <= right.End && right.Start <= left.End;

    private readonly record struct Cidr(uint Start, uint End, int Prefix);
}
