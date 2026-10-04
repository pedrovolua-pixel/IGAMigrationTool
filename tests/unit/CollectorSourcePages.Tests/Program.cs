using CollectorSourcePages.Tests;

try
{
    Console.WriteLine($"{FirstPageSqlChecks.Run()} first/source-pair SQL checks passed.");
    Console.WriteLine($"{await SourcePageChecks.RunAsync()} scripted source-page checks passed.");
    return 0;
}
catch (Exception)
{
    // Assertion checks print a fixed check name before throwing; no source payload/errors printed.
    Console.Error.WriteLine("Scripted source-page assertion host failed.");
    return 1;
}
