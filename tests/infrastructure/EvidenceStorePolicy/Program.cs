using System.Text.Json.Nodes;

if (args.Length != 2 || !File.Exists(args[0]) || !File.Exists(args[1]))
{
    Console.Error.WriteLine("Provide compiled evidence-store and evidence-roles ARM template paths.");
    Environment.ExitCode = 1;
    return;
}

var template = JsonNode.Parse(File.ReadAllText(args[0]));
if (!IsClosedEvidenceStore(template))
{
    Console.Error.WriteLine("Engineering evidence-store baseline policy failed.");
    Environment.ExitCode = 1;
    return;
}

var changes = new (string Name, Action<JsonNode> Mutate)[]
{
    ("public network", node => Resource(node, "Microsoft.Storage/storageAccounts")["properties"]!["publicNetworkAccess"] = "Enabled"),
    ("shared key", node => Resource(node, "Microsoft.Storage/storageAccounts")["properties"]!["allowSharedKeyAccess"] = true),
    ("public blob", node => Resource(node, "Microsoft.Storage/storageAccounts/blobServices/containers")["properties"]!["publicAccess"] = "Blob"),
    ("disabled versioning", node => Resource(node, "Microsoft.Storage/storageAccounts/blobServices")["properties"]!["isVersioningEnabled"] = false),
    ("wrong region", node => node["variables"]!["location"] = "westus2"),
    ("long soft-delete window", node => Resource(node, "Microsoft.Storage/storageAccounts/blobServices")["properties"]!["deleteRetentionPolicy"]!["days"] = 35),
    ("wrong private-link service", node => Resource(node, "Microsoft.Network/privateEndpoints")["properties"]!["privateLinkServiceConnections"]![0]!["properties"]!["groupIds"]![0] = "dfs"),
    ("wrong private DNS binding", node => Resource(node, "Microsoft.Network/privateEndpoints/privateDnsZoneGroups")["properties"]!["privateDnsZoneConfigs"]![0]!["properties"]!["privateDnsZoneId"] = "other-zone")
};

foreach (var change in changes)
{
    var altered = template!.DeepClone();
    change.Mutate(altered);
    if (IsClosedEvidenceStore(altered))
    {
        throw new Exception($"Infrastructure policy accepted {change.Name} drift.");
    }
}

Console.WriteLine($"Engineering evidence-store baseline policy passed; {changes.Length} unsafe drifts were denied.");

var roleTemplate = JsonNode.Parse(File.ReadAllText(args[1]));
if (!IsClosedEvidenceRoles(roleTemplate))
{
    Console.Error.WriteLine("Engineering evidence-role policy failed.");
    Environment.ExitCode = 1;
    return;
}

var roleChanges = new (string Name, Action<JsonNode> Mutate)[]
{
    ("intake delete", node => Role(node, "intake")["properties"]!["permissions"]![0]!["dataActions"]![0] =
        "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/delete"),
    ("verification write", node => Role(node, "verification")["properties"]!["permissions"]![0]!["dataActions"]![0] =
        "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/write"),
    ("purge write", node => Role(node, "purge")["properties"]!["permissions"]![0]!["dataActions"]![0] =
        "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/write"),
    ("wider role scope", node => Role(node, "intake")["properties"]!["assignableScopes"]![0] =
        "[subscription().id]"),
    ("wider assignment scope", node => Assignment(node, "intakePrincipalId")["scope"] =
        "[resourceGroup().id]"),
    ("human principal", node => Assignment(node, "verificationPrincipalId")["properties"]!["principalType"] =
        "User"),
    ("wrong role binding", node => Assignment(node, "purgePrincipalId")["properties"]!["roleDefinitionId"] =
        "wrong-role")
};

foreach (var change in roleChanges)
{
    var altered = roleTemplate!.DeepClone();
    change.Mutate(altered);
    if (IsClosedEvidenceRoles(altered))
    {
        throw new Exception($"Infrastructure policy accepted {change.Name} role drift.");
    }
}

Console.WriteLine($"Engineering evidence-role policy passed; {roleChanges.Length} unsafe drifts were denied.");

static bool IsClosedEvidenceStore(JsonNode? template)
{
    try
    {
        if (template?["resources"] is not JsonArray resources || resources.Count != 5 ||
            template["parameters"] is not JsonObject parameters || parameters.Count != 3 ||
            Value<string>(parameters["storageAccountName"]?["type"]) != "string" ||
            Value<string>(parameters["privateEndpointSubnetId"]?["type"]) != "string" ||
            Value<string>(parameters["blobPrivateDnsZoneId"]?["type"]) != "string" ||
            Value<string>(template["variables"]?["location"]) != "eastus2" ||
            Value<string>(template["variables"]?["containerName"]) != "gate-evidence" ||
            template["outputs"] is not JsonObject outputs || outputs.Count != 3 ||
            !outputs.ContainsKey("storageAccountId") || !outputs.ContainsKey("containerResourceId") ||
            !outputs.ContainsKey("privateEndpointId"))
        {
            return false;
        }

        foreach (var resource in resources)
        {
            var type = Value<string>(resource?["type"]);
            var expectedApi = type.StartsWith("Microsoft.Storage/", StringComparison.Ordinal)
                ? "2025-06-01"
                : "2025-05-01";
            if (Value<string>(resource?["apiVersion"]) != expectedApi)
            {
                return false;
            }
        }

        var account = Resource(template, "Microsoft.Storage/storageAccounts");
        var accountProperties = account["properties"];
        var network = accountProperties?["networkAcls"];
        var blobs = Resource(template, "Microsoft.Storage/storageAccounts/blobServices")["properties"];
        var container = Resource(template, "Microsoft.Storage/storageAccounts/blobServices/containers")["properties"];
        var privateEndpoint = Resource(template, "Microsoft.Network/privateEndpoints");
        var privateConnection = privateEndpoint["properties"]?["privateLinkServiceConnections"] as JsonArray;
        var dnsGroup = Resource(template, "Microsoft.Network/privateEndpoints/privateDnsZoneGroups")["properties"]?["privateDnsZoneConfigs"] as JsonArray;
        return Value<string>(account["location"]) == "[variables('location')]" &&
               Value<string>(account["kind"]) == "StorageV2" &&
               Value<string>(account["sku"]?["name"]) == "Standard_LRS" &&
               Value<bool>(accountProperties?["allowBlobPublicAccess"]) == false &&
               Value<bool>(accountProperties?["allowCrossTenantReplication"]) == false &&
               Value<bool>(accountProperties?["allowSharedKeyAccess"]) == false &&
               Value<bool>(accountProperties?["defaultToOAuthAuthentication"]) == true &&
               Value<bool>(accountProperties?["isLocalUserEnabled"]) == false &&
               Value<bool>(accountProperties?["supportsHttpsTrafficOnly"]) == true &&
               Value<string>(accountProperties?["minimumTlsVersion"]) == "TLS1_2" &&
               Value<string>(accountProperties?["publicNetworkAccess"]) == "Disabled" &&
               Value<string>(accountProperties?["encryption"]?["keySource"]) == "Microsoft.Storage" &&
               Value<string>(network?["bypass"]) == "None" &&
               Value<string>(network?["defaultAction"]) == "Deny" &&
               network?["ipRules"] is JsonArray { Count: 0 } &&
               network?["virtualNetworkRules"] is JsonArray { Count: 0 } &&
               Value<bool>(blobs?["isVersioningEnabled"]) == true &&
               blobs?["changeFeed"] is null &&
               Value<bool>(blobs?["deleteRetentionPolicy"]?["enabled"]) == true &&
               Value<int>(blobs?["deleteRetentionPolicy"]?["days"]) == 7 &&
               Value<bool>(blobs?["containerDeleteRetentionPolicy"]?["enabled"]) == true &&
               Value<int>(blobs?["containerDeleteRetentionPolicy"]?["days"]) == 7 &&
               Value<string>(container?["publicAccess"]) == "None" &&
               Value<string>(privateEndpoint["location"]) == "[variables('location')]" &&
               Value<string>(privateEndpoint["properties"]?["subnet"]?["id"]) == "[parameters('privateEndpointSubnetId')]" &&
               privateConnection is { Count: 1 } &&
               Value<string>(privateConnection[0]?["name"]) == "blob" &&
               Value<string>(privateConnection[0]?["properties"]?["privateLinkServiceId"]) ==
                   "[resourceId('Microsoft.Storage/storageAccounts', parameters('storageAccountName'))]" &&
               privateConnection[0]?["properties"]?["groupIds"] is JsonArray { Count: 1 } groups &&
               Value<string>(groups[0]) == "blob" &&
               dnsGroup is { Count: 1 } &&
               Value<string>(dnsGroup[0]?["name"]) == "blob" &&
               Value<string>(dnsGroup[0]?["properties"]?["privateDnsZoneId"]) ==
                   "[parameters('blobPrivateDnsZoneId')]";
    }
    catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or ArgumentException)
    {
        return false;
    }
}

static JsonNode Resource(JsonNode template, string type)
{
    var matches = ((JsonArray)template["resources"]!).Where(node => Value<string>(node?["type"]) == type).ToArray();
    return matches.Length == 1 ? matches[0]! : throw new InvalidOperationException("Missing or duplicate resource type.");
}

static bool IsClosedEvidenceRoles(JsonNode? template)
{
    try
    {
        if (template?["resources"] is not JsonArray resources || resources.Count != 6 ||
            template["parameters"] is not JsonObject parameters || parameters.Count != 4 ||
            Value<string>(template["variables"]?["blobActionPrefix"]) !=
                "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/" ||
            resources.Count(node => Value<string>(node?["type"]) == "Microsoft.Authorization/roleDefinitions") != 3 ||
            resources.Count(node => Value<string>(node?["type"]) == "Microsoft.Authorization/roleAssignments") != 3 ||
            resources.Any(node => Value<string>(node?["apiVersion"]) != "2022-04-01"))
        {
            return false;
        }

        foreach (var parameterName in new[]
                 { "storageAccountName", "intakePrincipalId", "verificationPrincipalId", "purgePrincipalId" })
        {
            if (Value<string>(parameters[parameterName]?["type"]) != "string")
            {
                return false;
            }
        }

        var roles = new[]
        {
            (Name: "intake", Principal: "intakePrincipalId", Actions: new[] { "add/action" }),
            (Name: "verification", Principal: "verificationPrincipalId", Actions: new[] { "read" }),
            (Name: "purge", Principal: "purgePrincipalId", Actions: new[] { "read", "delete", "deleteBlobVersion/action" })
        };
        foreach (var item in roles)
        {
            var role = Role(template, item.Name);
            var properties = role["properties"];
            var permissions = properties?["permissions"] as JsonArray;
            if (Value<string>(properties?["type"]) != "CustomRole" ||
                properties?["assignableScopes"] is not JsonArray { Count: 1 } scopes ||
                Value<string>(scopes[0]) != "[resourceGroup().id]" ||
                permissions is not { Count: 1 } ||
                permissions[0]?["actions"] is not JsonArray { Count: 0 } ||
                permissions[0]?["notActions"] is not JsonArray { Count: 0 } ||
                permissions[0]?["notDataActions"] is not JsonArray { Count: 0 } ||
                permissions[0]?["dataActions"] is not JsonArray dataActions ||
                dataActions.Count != item.Actions.Length)
            {
                return false;
            }

            for (var index = 0; index < item.Actions.Length; index++)
            {
                if (Value<string>(dataActions[index]) !=
                    $"[format('{{0}}{item.Actions[index]}', variables('blobActionPrefix'))]")
                {
                    return false;
                }
            }

            var assignment = Assignment(template, item.Principal);
            var assignmentProperties = assignment["properties"];
            if (Value<string>(assignment["scope"]) !=
                    "[resourceId('Microsoft.Storage/storageAccounts/blobServices/containers', split(format('{0}/default/gate-evidence', parameters('storageAccountName')), '/')[0], split(format('{0}/default/gate-evidence', parameters('storageAccountName')), '/')[1], split(format('{0}/default/gate-evidence', parameters('storageAccountName')), '/')[2])]" ||
                Value<string>(assignmentProperties?["principalType"]) != "ServicePrincipal" ||
                Value<string>(assignmentProperties?["principalId"]) != $"[parameters('{item.Principal}')]" ||
                Value<string>(assignmentProperties?["roleDefinitionId"]) !=
                    $"[resourceId('Microsoft.Authorization/roleDefinitions', guid(resourceGroup().id, parameters('storageAccountName'), 'gate-evidence-{item.Name}'))]")
            {
                return false;
            }
        }

        return true;
    }
    catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or ArgumentException)
    {
        return false;
    }
}

static JsonNode Role(JsonNode template, string name) => ((JsonArray)template["resources"]!)
    .Single(node => Value<string>(node?["type"]) == "Microsoft.Authorization/roleDefinitions" &&
        Value<string>(node?["name"]).Contains($"gate-evidence-{name}'", StringComparison.Ordinal))!;

static JsonNode Assignment(JsonNode template, string principalParameter) => ((JsonArray)template["resources"]!)
    .Single(node => Value<string>(node?["type"]) == "Microsoft.Authorization/roleAssignments" &&
        Value<string>(node?["properties"]?["principalId"]) == $"[parameters('{principalParameter}')]")!;

static T Value<T>(JsonNode? node) => node is JsonValue value && value.TryGetValue<T>(out var result)
    ? result
    : throw new InvalidOperationException("Missing or invalid template value.");
