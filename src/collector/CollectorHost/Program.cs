using CollectorHost;

if (!CollectorArguments.TryParse(args, out var command))
{
    Console.Error.WriteLine("ARGUMENTS_INVALID");
    return 2;
}

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("PLATFORM_UNSUPPORTED");
    return 4;
}

CollectorConfig config;
try
{
    config = WindowsProtectedConfig.Read(command!.ConfigPath);
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or
       FormatException or System.Security.SecurityException)
{
    Console.Error.WriteLine("CONFIG_INVALID");
    return 3;
}

if (command!.Command == CollectorCommand.Status)
{
    Console.WriteLine(config.Enabled ? "SOURCE_CONTRACT_PENDING" : "COLLECTOR_DISABLED");
    return 0;
}

if (command.Command == CollectorCommand.Service && OperatingSystem.IsWindows())
{
    await CollectorServiceRunner.RunAsync(command.ConfigPath);
    return 0;
}

Console.Error.WriteLine(config.Enabled ? "SOURCE_CONTRACT_PENDING" : "COLLECTOR_DISABLED");
return 5;
