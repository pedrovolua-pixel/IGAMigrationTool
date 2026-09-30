using BoundaryChecks;

var cases = new (string Name, string Source, string Target, bool Allowed)[]
{
    ("collector cannot reference server internals", "src/collector/Pilot/Collector.csproj", "src/server/modules/Evidence/Evidence.csproj", false),
    ("collector can reference versioned contracts", "src/collector/Pilot/Collector.csproj", "contracts/Evidence/Evidence.csproj", true),
    ("renderer cannot reference canonical modules", "src/server/hosts/Renderer/Renderer.csproj", "src/server/modules/Reporting/Reporting.csproj", false),
    ("renderer can reference render contracts", "src/server/hosts/Renderer/Renderer.csproj", "contracts/Render/Render.csproj", true),
    ("module cannot reference host", "src/server/modules/Assessment/Assessment.csproj", "src/server/hosts/Bff/Bff.csproj", false),
    ("worker can reference module", "src/server/hosts/Worker/Worker.csproj", "src/server/modules/Assessment/Assessment.csproj", true),
    ("BFF cannot reference collector", "src/server/hosts/Bff/Bff.csproj", "src/collector/Pilot/Collector.csproj", false)
};

foreach (var item in cases)
{
    if (ProjectBoundaryVerifier.IsAllowed(item.Source, item.Target) != item.Allowed)
    {
        throw new Exception($"Architecture policy case failed: {item.Name}.");
    }
}

var temporaryRoot = Path.Combine(Path.GetTempPath(), $"iga-boundary-{Guid.NewGuid():N}");
try
{
    var collector = Path.Combine(temporaryRoot, "src", "collector", "Pilot", "Collector.csproj");
    var serverModule = Path.Combine(temporaryRoot, "src", "server", "modules", "Evidence", "Evidence.csproj");
    var contract = Path.Combine(temporaryRoot, "contracts", "Evidence", "Evidence.csproj");
    Directory.CreateDirectory(Path.GetDirectoryName(collector)!);
    Directory.CreateDirectory(Path.GetDirectoryName(serverModule)!);
    Directory.CreateDirectory(Path.GetDirectoryName(contract)!);
    File.WriteAllText(serverModule, "<Project />");
    File.WriteAllText(contract, "<Project />");

    File.WriteAllText(collector,
        "<Project><ItemGroup><ProjectReference Include=\"../../server/modules/Evidence/Evidence.csproj\" /></ItemGroup></Project>");
    AssertScan("forbidden project reference", "forbidden ProjectReference");

    File.WriteAllText(collector,
        "<Project><ItemGroup><ProjectReference Include=\"../../../contracts/Evidence/Evidence.csproj\" /></ItemGroup></Project>");
    if (ProjectBoundaryVerifier.Scan(temporaryRoot).Count != 0)
    {
        throw new Exception("Allowed versioned contract reference was rejected.");
    }

    File.WriteAllText(collector,
        "<Project><ItemGroup><ProjectReference Include=\"../../../missing.csproj\" /></ItemGroup></Project>");
    AssertScan("missing project reference", "missing or outside");

    File.WriteAllText(collector, "<!DOCTYPE Project [<!ENTITY unsafe SYSTEM \"file:///etc/passwd\">]><Project />");
    AssertScan("unsafe project XML", "invalid or unsafe");

    void AssertScan(string caseName, string expectedText)
    {
        var scanErrors = ProjectBoundaryVerifier.Scan(temporaryRoot);
        if (scanErrors.Count != 1 || !scanErrors[0].Contains(expectedText, StringComparison.Ordinal))
        {
            throw new Exception($"Architecture scan case failed: {caseName}.");
        }
    }
}
finally
{
    Directory.Delete(temporaryRoot, recursive: true);
}

var errors = ProjectBoundaryVerifier.Scan(Directory.GetCurrentDirectory());
if (errors.Count > 0)
{
    foreach (var error in errors)
    {
        Console.Error.WriteLine(error);
    }

    Environment.ExitCode = 1;
}
else
{
    Console.WriteLine($"{cases.Length} architecture policy and 4 project-scan cases passed; repository ProjectReferences satisfy the checked boundaries.");
}
