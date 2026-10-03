using System.Text.Json.Nodes;

internal static class OracleCases
{
    internal static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);
    internal static void Run()
    {
        Check.Equal(Expected.Hash(Expected.Canonical(new JsonObject { ["templateVersion"] = Expected.TemplateVersion, ["templates"] = Expected.TemplateNodes() })), Expected.TemplateDigest, "literal-fixed-three-template-registry");
        foreach (var name in new[] { "normal", "hostile", "empty", "source-b", "source-return", "run-later" })
        {
            var package = JsonNode.Parse(File.ReadAllText(Fixture(name + "-fix-golden.json")))!.AsObject();
            Check.Equal(Expected.Canonical(Expected.FixPayload(package["guidance"]!.AsObject())), File.ReadAllText(Fixture(name + "-fix-golden.json")), "independent-complete-package-recipe-cross-language");
            Check.Equal(Expected.Canonical(Expected.Binding(package)), File.ReadAllText(Fixture(name + "-artifact-binding-golden.json")), "independent-full-artifact-source-recipe-cross-language");
            Check.Equal(Expected.Canonical(Expected.TaskBinding(package)), File.ReadAllText(Fixture(name + "-binding-golden.json")), "independent-full-task-source-recipe-cross-language");
            Check.Equal(Expected.Canonical(Expected.TaskOptions(package)), File.ReadAllText(Fixture(name + "-options-golden.json")), "independent-all-task-identity-option-vector-recipe-cross-language");
        }
        var identities = JsonNode.Parse(File.ReadAllText(Fixture("identity-golden.json")))!.AsArray();
        foreach (var value in identities)
        {
            var binding = new JsonObject { ["scope"] = value!["scope"]!.DeepClone(), ["runId"] = value["runId"]!.DeepClone() };
            Check.Equal(Expected.TaskId(binding, value["findingId"]!.GetValue<string>(), value["scopedOptionId"]!.GetValue<string>()), value["taskId"]!.GetValue<string>(), "independent-each-scoped-identity-coordinate");
        }
        Check.Equal(identities.Select(x => x!["taskId"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count(), 7, "seven-isolated-task-identity-coordinates-distinct");
        var commands = JsonNode.Parse(File.ReadAllText(Fixture("command-golden.json")))!.AsArray();
        foreach (var value in commands)
        {
            var payload = value!["payload"]!;
            var binding = new JsonObject { ["scope"] = payload["scope"]!.DeepClone(), ["runId"] = payload["runId"]!.DeepClone() };
            Check.Equal(Expected.Canonical(payload), value["canonical"]!.GetValue<string>(), "all-eight-independent-semantic-command-complete-bytes");
            Check.Equal(Expected.CommandDigest(binding, payload["taskId"]!.GetValue<string>(), payload["actorId"]!.GetValue<string>(), payload["command"]!), value["digest"]!.GetValue<string>(), "all-eight-independent-semantic-command-digests");
        }
        Check.Equal(commands.Count, 8, "exact-eight-command-kinds");
        Check.Group("TC14-T03/T04 independent cross-language oracle primitives only; not store acceptance");
    }
}
