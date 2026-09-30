using System.Xml;
using System.Xml.Linq;

namespace BoundaryChecks;

internal static class ProjectBoundaryVerifier
{
    internal static IReadOnlyList<string> Scan(string repositoryRoot)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var sourceRoot = Path.Combine(root, "src");
        if (!Directory.Exists(sourceRoot))
        {
            return ["The source directory is missing."];
        }

        var errors = new List<string>();
        foreach (var project in Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
                     .Where(path => !IsGeneratedPath(path))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var source = Relative(root, project);
            try
            {
                using var reader = XmlReader.Create(project, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                var document = XDocument.Load(reader);
                foreach (var reference in document.Descendants()
                             .Where(element => element.Name.LocalName == "ProjectReference"))
                {
                    var include = (string?)reference.Attribute("Include");
                    if (string.IsNullOrWhiteSpace(include))
                    {
                        errors.Add($"{source}: ProjectReference has no Include path.");
                        continue;
                    }

                    var targetPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include));
                    var target = Relative(root, targetPath);
                    if (Path.IsPathRooted(target) || target.StartsWith("../", StringComparison.Ordinal) || target == ".." ||
                        !File.Exists(targetPath))
                    {
                        errors.Add($"{source}: ProjectReference target is missing or outside the repository.");
                    }
                    else if (!IsAllowed(source, target))
                    {
                        errors.Add($"{source}: forbidden ProjectReference to {target}.");
                    }
                }
            }
            catch (XmlException)
            {
                errors.Add($"{source}: invalid or unsafe project XML.");
            }
        }

        return errors;
    }

    internal static bool IsAllowed(string source, string target)
    {
        var sourcePath = Normalize(source);
        var targetPath = Normalize(target);
        if (Starts(sourcePath, "src/collector/"))
        {
            return !Starts(targetPath, "src/server/");
        }

        if (Starts(sourcePath, "src/server/hosts/renderer/"))
        {
            return !Starts(targetPath, "src/server/") && !Starts(targetPath, "src/collector/");
        }

        if (Starts(sourcePath, "src/server/modules/"))
        {
            return !Starts(targetPath, "src/server/hosts/") && !Starts(targetPath, "src/collector/");
        }

        if (Starts(sourcePath, "src/server/hosts/"))
        {
            return !Starts(targetPath, "src/collector/");
        }

        return true;
    }

    private static bool IsGeneratedPath(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "obj" or "bin");

    private static string Relative(string root, string path) =>
        Normalize(Path.GetRelativePath(root, path));

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static bool Starts(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
