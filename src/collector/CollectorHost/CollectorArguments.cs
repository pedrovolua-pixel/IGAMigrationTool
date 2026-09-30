namespace CollectorHost;

public enum CollectorCommand
{
    Status,
    CollectOnce,
    Service
}

public sealed record CollectorArguments(CollectorCommand Command, string ConfigPath, string? OfflineOutputPath)
{
    public static bool TryParse(string[] args, out CollectorArguments? parsed)
    {
        parsed = null;
        if (args.Length != 3 && args.Length != 5)
        {
            return false;
        }

        var command = args[0] switch
        {
            "status" => CollectorCommand.Status,
            "collect-once" => CollectorCommand.CollectOnce,
            "service" => CollectorCommand.Service,
            _ => (CollectorCommand?)null
        };
        if (command is null || args[1] != "--config" || !LocalPath.IsValid(args[2]))
        {
            return false;
        }

        if (command == CollectorCommand.CollectOnce)
        {
            if (args.Length != 5 || args[3] != "--offline-output" || !LocalPath.IsValid(args[4]) ||
                string.Equals(args[2], args[4], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            parsed = new CollectorArguments(command.Value, args[2], args[4]);
            return true;
        }

        if (args.Length != 3)
        {
            return false;
        }

        parsed = new CollectorArguments(command.Value, args[2], null);
        return true;
    }
}

public static class LocalPath
{
    public static bool IsValid(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length < 4 ||
            !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' ||
            path.Contains('/') || path.Contains('"') || path.Contains('*') || path.Contains('?') ||
            path.Contains('<') || path.Contains('>') || path.Contains('|') || path.Contains('\0'))
        {
            return false;
        }

        var segments = path[3..].Split('\\');
        return segments.Length > 0 && segments.All(segment =>
            segment.Length > 0 && segment != "." && segment != ".." &&
            !segment.EndsWith(' ') && !segment.EndsWith('.') && !segment.Contains(':') &&
            !segment.Split('.').Any(part => part.EndsWith(' ')) &&
            !IsReservedDeviceName(segment));
    }

    private static bool IsReservedDeviceName(string segment)
    {
        var stem = segment.Split('.')[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (stem.Length == 4 &&
             (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
              stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
             stem[3] is >= '1' and <= '9');
    }
}
