using System.Text.Json.Nodes;

if (args.Length != 3 || !File.Exists(args[0]) || !File.Exists(args[1]) || !File.Exists(args[2]))
{
    Console.Error.WriteLine("Provide compiled Service Bus namespace, work-role and broker-network spike ARM template paths.");
    Environment.ExitCode = 1;
    return;
}

var template = JsonNode.Parse(File.ReadAllText(args[0]));
if (!IsClosedServiceBus(template))
{
    Console.Error.WriteLine("Service Bus namespace baseline policy failed.");
    Environment.ExitCode = 1;
    return;
}

var changes = new (string Name, Action<JsonNode> Mutate)[]
{
    ("wrong region", node => node["variables"]!["location"] = "westus2"),
    ("Premium tier", node => Resource(node, "Microsoft.ServiceBus/namespaces")["sku"]!["tier"] = "Premium"),
    ("local authentication", node => Resource(node, "Microsoft.ServiceBus/namespaces")["properties"]!["disableLocalAuth"] = false),
    ("weaker TLS", node => Resource(node, "Microsoft.ServiceBus/namespaces")["properties"]!["minimumTlsVersion"] = "1.1"),
    ("unrestricted network", node => Resource(node, "Microsoft.ServiceBus/namespaces/networkRuleSets")["properties"]!["defaultAction"] = "Allow"),
    ("missing network rule", node => ((JsonArray)node["resources"]!).RemoveAt(1)),
    ("empty egress identity", node => node["parameters"]!["staticEgressPublicIpName"]!["minLength"] = 0),
    ("broad egress range", node => Resource(node, "Microsoft.ServiceBus/namespaces/networkRuleSets")["properties"]!["ipRules"]![0]!["ipMask"] = "0.0.0.0/0"),
    ("extra allowed network", node => Resource(node, "Microsoft.ServiceBus/namespaces/networkRuleSets")["properties"]!["virtualNetworkRules"] = new JsonArray("other-network")),
    ("wrong IP action", node => Resource(node, "Microsoft.ServiceBus/namespaces/networkRuleSets")["properties"]!["ipRules"]![0]!["action"] = "Deny"),
    ("extra resource", node => ((JsonArray)node["resources"]!).Add(new JsonObject { ["type"] = "Microsoft.ServiceBus/namespaces/queues" }))
};

foreach (var change in changes)
{
    var altered = template!.DeepClone();
    change.Mutate(altered);
    if (IsClosedServiceBus(altered))
    {
        throw new Exception($"Infrastructure policy accepted {change.Name} drift.");
    }
}

Console.WriteLine($"Service Bus namespace baseline policy passed; {changes.Length} unsafe drifts were denied.");

var rolesTemplate = JsonNode.Parse(File.ReadAllText(args[1]));
if (!IsClosedWorkRoles(rolesTemplate))
{
    Console.Error.WriteLine("Service Bus work-role baseline policy failed.");
    Environment.ExitCode = 1;
    return;
}

var roleChanges = new (string Name, Action<JsonNode> Mutate)[]
{
    ("sender elevated to owner", node => node["variables"]!["senderRoleId"] = "090c5cfd-751d-490a-894a-3ce6f1109419"),
    ("wrong receiver role", node => node["variables"]!["receiverRoleId"] = "69a216fc-b8fb-44d8-bc22-1f3c2cd27a39"),
    ("namespace-wide scope", node => Assignment(node, "senderPrincipalId")["scope"] =
        "[resourceId('Microsoft.ServiceBus/namespaces', parameters('namespaceName'))]"),
    ("human principal", node => Assignment(node, "receiverPrincipalId")["properties"]!["principalType"] = "User"),
    ("swapped sender identity", node => Assignment(node, "senderPrincipalId")["properties"]!["principalId"] =
        "[parameters('receiverPrincipalId')]"),
    ("wrong role binding", node => Assignment(node, "receiverPrincipalId")["properties"]!["roleDefinitionId"] =
        "[subscriptionResourceId('Microsoft.Authorization/roleDefinitions', variables('senderRoleId'))]"),
    ("extra assignment", node => ((JsonArray)node["resources"]!).Add(new JsonObject
    {
        ["type"] = "Microsoft.Authorization/roleAssignments"
    }))
};

foreach (var change in roleChanges)
{
    var altered = rolesTemplate!.DeepClone();
    change.Mutate(altered);
    if (IsClosedWorkRoles(altered))
    {
        throw new Exception($"Infrastructure policy accepted {change.Name} role drift.");
    }
}

Console.WriteLine($"Service Bus work-role baseline policy passed; {roleChanges.Length} unsafe drifts were denied.");

var spikeTemplate = JsonNode.Parse(File.ReadAllText(args[2]));
if (!IsClosedSpike(spikeTemplate))
{
    Console.Error.WriteLine("Broker-network spike wiring policy failed.");
    Environment.ExitCode = 1;
    return;
}

var spikeChanges = new (string Name, Action<JsonNode> Mutate)[]
{
    ("broker uses a different IP", node => Deployment(node, "pilot-service-bus-namespace")["properties"]!["parameters"]!["staticEgressPublicIpName"]!["value"] = "other-ip"),
    ("broker deploys before network", node => Deployment(node, "pilot-service-bus-namespace")["dependsOn"] = new JsonArray()),
    ("network uses a different IP", node => Deployment(node, "pilot-network-egress")["properties"]!["parameters"]!["staticEgressPublicIpName"]!["value"] = "other-ip"),
    ("wrong namespace parameter", node => Deployment(node, "pilot-service-bus-namespace")["properties"]!["parameters"]!["namespaceName"]!["value"] = "other-namespace"),
    ("extra deployed resource", node => ((JsonArray)node["resources"]!).Add(new JsonObject { ["type"] = "Microsoft.Resources/deployments" }))
};

foreach (var change in spikeChanges)
{
    var altered = spikeTemplate!.DeepClone();
    change.Mutate(altered);
    if (IsClosedSpike(altered))
    {
        throw new Exception($"Spike wiring policy accepted {change.Name} drift.");
    }
}

Console.WriteLine($"Broker-network spike wiring policy passed; {spikeChanges.Length} unsafe drifts were denied.");

static bool IsClosedServiceBus(JsonNode? template)
{
    try
    {
        if (template?["parameters"] is not JsonObject parameters || parameters.Count != 2 ||
            template["resources"] is not JsonArray resources || resources.Count != 2 ||
            template["outputs"] is not JsonObject outputs || outputs.Count != 1 ||
            Value<string>(template["variables"]?["location"]) != "eastus2" ||
            Value<string>(parameters["namespaceName"]?["type"]) != "string" ||
            Value<int>(parameters["namespaceName"]?["minLength"]) != 6 ||
            Value<int>(parameters["namespaceName"]?["maxLength"]) != 50 ||
            Value<string>(parameters["staticEgressPublicIpName"]?["type"]) != "string" ||
            Value<int>(parameters["staticEgressPublicIpName"]?["minLength"]) != 1 ||
            Value<string>(outputs["namespaceResourceId"]?["type"]) != "string" ||
            Value<string>(outputs["namespaceResourceId"]?["value"]) !=
                "[resourceId('Microsoft.ServiceBus/namespaces', parameters('namespaceName'))]")
        {
            return false;
        }

        var serviceBus = Resource(template, "Microsoft.ServiceBus/namespaces");
        var networkRules = Resource(template, "Microsoft.ServiceBus/namespaces/networkRuleSets");
        var networkProperties = networkRules["properties"];
        var ipRules = networkProperties?["ipRules"] as JsonArray;
        return Value<string>(serviceBus["type"]) == "Microsoft.ServiceBus/namespaces" &&
               Value<string>(serviceBus["apiVersion"]) == "2026-01-01" &&
               Value<string>(serviceBus["name"]) == "[parameters('namespaceName')]" &&
               Value<string>(serviceBus["location"]) == "[variables('location')]" &&
               Value<string>(serviceBus["sku"]?["name"]) == "Standard" &&
               Value<string>(serviceBus["sku"]?["tier"]) == "Standard" &&
               Value<bool>(serviceBus["properties"]?["disableLocalAuth"]) &&
               Value<string>(serviceBus["properties"]?["minimumTlsVersion"]) == "1.2" &&
               Value<string>(serviceBus["properties"]?["publicNetworkAccess"]) == "Enabled" &&
               Value<string>(networkRules["type"]) == "Microsoft.ServiceBus/namespaces/networkRuleSets" &&
               Value<string>(networkRules["apiVersion"]) == "2026-01-01" &&
               Value<string>(networkRules["name"]) ==
                   "[format('{0}/{1}', parameters('namespaceName'), 'default')]" &&
               networkRules["dependsOn"] is JsonArray { Count: 1 } dependsOn &&
               Value<string>(dependsOn[0]) ==
                   "[resourceId('Microsoft.ServiceBus/namespaces', parameters('namespaceName'))]" &&
               Value<string>(networkProperties?["defaultAction"]) == "Deny" &&
               Value<string>(networkProperties?["publicNetworkAccess"]) == "Enabled" &&
               networkProperties?["virtualNetworkRules"] is JsonArray { Count: 0 } &&
               ipRules is { Count: 1 } &&
               Value<string>(ipRules[0]?["ipMask"]) ==
                   "[reference(resourceId('Microsoft.Network/publicIPAddresses', parameters('staticEgressPublicIpName')), '2025-05-01').ipAddress]" &&
               Value<string>(ipRules[0]?["action"]) == "Allow";
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

static bool IsClosedWorkRoles(JsonNode? template)
{
    try
    {
        if (template?["resources"] is not JsonArray resources || resources.Count != 2 ||
            template["parameters"] is not JsonObject parameters || parameters.Count != 4 ||
            Value<string>(template["variables"]?["senderRoleId"]) !=
                "69a216fc-b8fb-44d8-bc22-1f3c2cd27a39" ||
            Value<string>(template["variables"]?["receiverRoleId"]) !=
                "4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0" ||
            Value<int>(parameters["namespaceName"]?["minLength"]) != 6 ||
            Value<int>(parameters["namespaceName"]?["maxLength"]) != 50)
        {
            return false;
        }

        foreach (var name in new[] { "namespaceName", "workQueueName", "senderPrincipalId", "receiverPrincipalId" })
        {
            if (Value<string>(parameters[name]?["type"]) != "string" ||
                Value<int>(parameters[name]?["minLength"]) < 1)
            {
                return false;
            }
        }

        foreach (var item in new[]
                 { (Principal: "senderPrincipalId", Role: "senderRoleId"),
                     (Principal: "receiverPrincipalId", Role: "receiverRoleId") })
        {
            var assignment = Assignment(template, item.Principal);
            if (Value<string>(assignment["type"]) != "Microsoft.Authorization/roleAssignments" ||
                Value<string>(assignment["apiVersion"]) != "2022-04-01" ||
                Value<string>(assignment["scope"]) !=
                    "[resourceId('Microsoft.ServiceBus/namespaces/queues', parameters('namespaceName'), parameters('workQueueName'))]" ||
                Value<string>(assignment["name"]) !=
                    $"[guid(resourceId('Microsoft.ServiceBus/namespaces/queues', parameters('namespaceName'), parameters('workQueueName')), parameters('{item.Principal}'), variables('{item.Role}'))]" ||
                Value<string>(assignment["properties"]?["principalId"]) !=
                    $"[parameters('{item.Principal}')]" ||
                Value<string>(assignment["properties"]?["principalType"]) != "ServicePrincipal" ||
                Value<string>(assignment["properties"]?["roleDefinitionId"]) !=
                    $"[subscriptionResourceId('Microsoft.Authorization/roleDefinitions', variables('{item.Role}'))]")
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

static JsonNode Assignment(JsonNode template, string principalParameter) =>
    ((JsonArray)template["resources"]!).Single(node =>
        Value<string>(node?["properties"]?["principalId"]) == $"[parameters('{principalParameter}')]")!;

static bool IsClosedSpike(JsonNode? template)
{
    try
    {
        if (template?["resources"] is not JsonArray resources || resources.Count != 2 ||
            template["parameters"] is not JsonObject parameters || parameters.Count != 9 ||
            template["outputs"] is not JsonObject outputs || outputs.Count != 3)
        {
            return false;
        }

        foreach (var name in new[]
                 { "virtualNetworkName", "virtualNetworkAddressPrefix", "containerAppsSubnetName",
                     "containerAppsSubnetAddressPrefix", "privateEndpointSubnetName",
                     "privateEndpointSubnetAddressPrefix", "staticEgressPublicIpName", "natGatewayName",
                     "serviceBusNamespaceName" })
        {
            if (Value<string>(parameters[name]?["type"]) != "string")
            {
                return false;
            }
        }

        var network = Deployment(template, "pilot-network-egress");
        var broker = Deployment(template, "pilot-service-bus-namespace");
        if (resources.Any(resource => Value<string>(resource?["type"]) != "Microsoft.Resources/deployments" ||
            Value<string>(resource?["apiVersion"]) != "2025-04-01") ||
            network["properties"]?["parameters"] is not JsonObject networkParameters ||
            networkParameters.Count != 8 ||
            broker["properties"]?["parameters"] is not JsonObject brokerParameters ||
            brokerParameters.Count != 2 ||
            broker["dependsOn"] is not JsonArray { Count: 1 } dependencies ||
            Value<string>(dependencies[0]) !=
                "[resourceId('Microsoft.Resources/deployments', 'pilot-network-egress')]" ||
            Value<string>(brokerParameters["namespaceName"]?["value"]) !=
                "[parameters('serviceBusNamespaceName')]" ||
            Value<string>(brokerParameters["staticEgressPublicIpName"]?["value"]) !=
                "[parameters('staticEgressPublicIpName')]" ||
            Value<string>(networkParameters["staticEgressPublicIpName"]?["value"]) !=
                "[parameters('staticEgressPublicIpName')]")
        {
            return false;
        }

        foreach (var name in new[]
                 { "virtualNetworkName", "virtualNetworkAddressPrefix", "containerAppsSubnetName",
                     "containerAppsSubnetAddressPrefix", "privateEndpointSubnetName",
                     "privateEndpointSubnetAddressPrefix", "natGatewayName" })
        {
            if (Value<string>(networkParameters[name]?["value"]) != $"[parameters('{name}')]")
            {
                return false;
            }
        }

        return outputs.ContainsKey("containerAppsSubnetId") &&
               outputs.ContainsKey("privateEndpointSubnetId") &&
               outputs.ContainsKey("namespaceResourceId");
    }
    catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or ArgumentException)
    {
        return false;
    }
}

static JsonNode Deployment(JsonNode template, string name) =>
    ((JsonArray)template["resources"]!).Single(node => Value<string>(node?["name"]) == name)!;

static T Value<T>(JsonNode? node) => node is JsonValue value && value.TryGetValue<T>(out var result)
    ? result
    : throw new InvalidOperationException("Missing or invalid template value.");
