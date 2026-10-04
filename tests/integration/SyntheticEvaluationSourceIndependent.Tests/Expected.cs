using System.Text.Json;

internal static class Expected
{
    internal const string ScheduleGroup = "e3003bf710a58e5af23e1a1af61f3af16dd9496bbc7ef146b4c88f734428100d";
    internal const string RetryGroup = "809bb94c8136fbfbddfad6073018a60814b9747d73cd1820cfb45790e8a122d9";
    internal static readonly string[] Keys = ["synthetic-ai-retry", "synthetic-ai-schedule"];
    internal static void Original(JsonElement value)
    {
        var schedule = value.GetProperty("key").GetProperty("inventoryId").GetString() == "synthetic-ai-schedule";
        Program.Check(value.GetProperty("proposalId").GetString() == (schedule ? "proposal-01" : "proposal-02"), "literal proposal identity");
        Program.Check(value.GetProperty("severity").GetString() == (schedule ? "High" : "Medium"), "literal source severity");
        Program.Check(value.GetProperty("category").GetString() == "OPERATIONS" && value.GetProperty("moduleId").GetString() == "SyntheticOperations", "native category/module preserved");
        Program.Check(value.GetProperty("state").GetString() == "Proposed" && value.GetProperty("detectionMethod").GetString() == "AI", "original AI Proposed retained");
        Program.Check(value.GetProperty("confidencePercent").GetDecimal() == 80 && value.GetProperty("weight").GetDecimal() == 1, "literal original confidence80/weight1");
        var expectedEvidence = "ev-" + new string(schedule ? '1' : '2', 64);
        Program.Check(value.GetProperty("evidenceIds").EnumerateArray().Single().GetString() == expectedEvidence, "native exact evidence reference");
    }
}
