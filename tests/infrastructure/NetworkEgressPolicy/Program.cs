using System.Text.Json.Nodes;

if (args.Length is not (1 or 4) || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Provide the compiled network-egress template, optionally followed by VNet, app-subnet and private-endpoint CIDRs.");
    Environment.ExitCode = 1;
    return;
}

var template = JsonNode.Parse(File.ReadAllText(args[0]));
if (!IsClosedNetworkEgress(template))
{
    Console.Error.WriteLine("Pilot network-egress baseline policy failed.");
    Environment.ExitCode = 1;
    return;
}

var changes = new (string Name, Action<JsonNode> Mutate)[]
{
    ("wrong region", node => node["variables"]!["location"] = "westus2"),
    ("dynamic public IP", node => Resource(node, "Microsoft.Network/publicIPAddresses")["properties"]!["publicIPAllocationMethod"] = "Dynamic"),
    ("IPv6 public IP", node => Resource(node, "Microsoft.Network/publicIPAddresses")["properties"]!["publicIPAddressVersion"] = "IPv6"),
    ("unsupported NAT tier", node => Resource(node, "Microsoft.Network/natGateways")["sku"]!["name"] = "StandardV2"),
    ("NAT without public IP", node => Resource(node, "Microsoft.Network/natGateways")["properties"]!["publicIpAddresses"] = new JsonArray()),
    ("app subnet without NAT", node => Subnet(node, "containerAppsSubnetName")["properties"]!["natGateway"] = null),
    ("wrong app delegation", node => Subnet(node, "containerAppsSubnetName")["properties"]!["delegations"]![0]!["properties"]!["serviceName"] = "Microsoft.Web/serverFarms"),
    ("wrong private subnet policies", node => Subnet(node, "privateEndpointSubnetName")["properties"]!["privateEndpointNetworkPolicies"] = "Enabled"),
    ("extra resource", node => ((JsonArray)node["resources"]!).Add(new JsonObject { ["type"] = "Microsoft.Network/publicIPAddresses" }))
};

foreach (var change in changes)
{
    var altered = template!.DeepClone();
    change.Mutate(altered);
    if (IsClosedNetworkEgress(altered))
    {
        throw new Exception($"Network policy accepted {change.Name} drift.");
    }
}

Console.WriteLine($"Pilot network-egress baseline policy passed; {changes.Length} unsafe drifts were denied.");

var addressPlans = new (string Name, string VNet, string Apps, string Endpoints, bool Expected)[]
{
    ("valid private plan", "10.40.0.0/16", "10.40.0.0/27", "10.40.1.0/27", true),
    ("valid larger app subnet", "172.20.0.0/16", "172.20.0.0/24", "172.20.1.0/28", true),
    ("small app subnet", "10.40.0.0/16", "10.40.0.0/28", "10.40.1.0/27", false),
    ("overlapping subnets", "10.40.0.0/16", "10.40.0.0/26", "10.40.0.32/27", false),
    ("app outside VNet", "10.40.0.0/24", "10.41.0.0/27", "10.40.0.64/27", false),
    ("endpoint outside VNet", "10.40.0.0/24", "10.40.0.0/27", "10.41.0.0/27", false),
    ("public VNet", "8.8.0.0/16", "8.8.0.0/27", "8.8.1.0/27", false),
    ("mixed private and public", "172.0.0.0/8", "172.20.0.0/27", "172.21.0.0/27", false),
    ("Container Apps reserved 172.30 range", "172.30.0.0/16", "172.30.0.0/27", "172.30.1.0/27", false),
    ("Container Apps reserved 172.31 range", "172.31.0.0/16", "172.31.0.0/27", "172.31.1.0/27", false),
    ("noncanonical app CIDR", "10.40.0.0/16", "10.40.0.1/27", "10.40.1.0/27", false),
    ("malformed IPv4", "10.40.0.0/16", "10.40.999.0/27", "10.40.1.0/27", false),
    ("IPv6 app CIDR", "10.40.0.0/16", "2001:db8::/27", "10.40.1.0/27", false),
    ("noncanonical prefix", "10.40.0.0/16", "10.40.0.0/027", "10.40.1.0/27", false)
};

foreach (var item in addressPlans)
{
    if (AddressPlanValidator.IsValid(item.VNet, item.Apps, item.Endpoints) != item.Expected)
    {
        throw new Exception($"Address-plan check failed: {item.Name}.");
    }
}

Console.WriteLine($"{addressPlans.Length} synthetic network address-plan checks passed.");
if (args.Length == 4)
{
    if (!AddressPlanValidator.IsValid(args[1], args[2], args[3]))
    {
        Console.Error.WriteLine("Environment network address plan is invalid.");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine("Environment network address plan passed.");
}

static bool IsClosedNetworkEgress(JsonNode? template)
{
    try
    {
        if (template?["parameters"] is not JsonObject parameters || parameters.Count != 8 ||
            template["resources"] is not JsonArray resources || resources.Count != 5 ||
            template["outputs"] is not JsonObject outputs || outputs.Count != 3 ||
            Value<string>(template["variables"]?["location"]) != "eastus2")
        {
            return false;
        }

        foreach (var name in new[]
                 { "virtualNetworkName", "virtualNetworkAddressPrefix", "containerAppsSubnetName",
                     "containerAppsSubnetAddressPrefix", "privateEndpointSubnetName",
                     "privateEndpointSubnetAddressPrefix", "staticEgressPublicIpName", "natGatewayName" })
        {
            if (Value<string>(parameters[name]?["type"]) != "string" ||
                Value<int>(parameters[name]?["minLength"]) != 1)
            {
                return false;
            }
        }

        var publicIp = Resource(template, "Microsoft.Network/publicIPAddresses");
        var nat = Resource(template, "Microsoft.Network/natGateways");
        var vnet = Resource(template, "Microsoft.Network/virtualNetworks");
        var appSubnet = Subnet(template, "containerAppsSubnetName");
        var privateSubnet = Subnet(template, "privateEndpointSubnetName");
        var ipRefs = nat["properties"]?["publicIpAddresses"] as JsonArray;
        var delegations = appSubnet["properties"]?["delegations"] as JsonArray;
        return resources.All(resource => Value<string>(resource?["apiVersion"]) == "2025-05-01") &&
               Value<string>(publicIp["name"]) == "[parameters('staticEgressPublicIpName')]" &&
               Value<string>(publicIp["location"]) == "[variables('location')]" &&
               Value<string>(publicIp["sku"]?["name"]) == "Standard" &&
               Value<string>(publicIp["sku"]?["tier"]) == "Regional" &&
               Value<string>(publicIp["properties"]?["publicIPAddressVersion"]) == "IPv4" &&
               Value<string>(publicIp["properties"]?["publicIPAllocationMethod"]) == "Static" &&
               Value<string>(nat["name"]) == "[parameters('natGatewayName')]" &&
               Value<string>(nat["location"]) == "[variables('location')]" &&
               Value<string>(nat["sku"]?["name"]) == "Standard" &&
               ipRefs is { Count: 1 } &&
               Value<string>(ipRefs[0]?["id"]) ==
                   "[resourceId('Microsoft.Network/publicIPAddresses', parameters('staticEgressPublicIpName'))]" &&
               Value<string>(vnet["name"]) == "[parameters('virtualNetworkName')]" &&
               Value<string>(vnet["location"]) == "[variables('location')]" &&
               vnet["properties"]?["addressSpace"]?["addressPrefixes"] is JsonArray { Count: 1 } prefixes &&
               Value<string>(prefixes[0]) == "[parameters('virtualNetworkAddressPrefix')]" &&
               Value<string>(appSubnet["properties"]?["addressPrefix"]) ==
                   "[parameters('containerAppsSubnetAddressPrefix')]" &&
               delegations is { Count: 1 } &&
               Value<string>(delegations[0]?["properties"]?["serviceName"]) ==
                   "Microsoft.App/environments" &&
               Value<string>(appSubnet["properties"]?["natGateway"]?["id"]) ==
                   "[resourceId('Microsoft.Network/natGateways', parameters('natGatewayName'))]" &&
               Value<string>(privateSubnet["properties"]?["addressPrefix"]) ==
                   "[parameters('privateEndpointSubnetAddressPrefix')]" &&
               Value<string>(privateSubnet["properties"]?["privateEndpointNetworkPolicies"]) ==
                   "Disabled" &&
               Value<string>(outputs["containerAppsSubnetId"]?["value"]) ==
                   "[resourceId('Microsoft.Network/virtualNetworks/subnets', parameters('virtualNetworkName'), parameters('containerAppsSubnetName'))]" &&
               Value<string>(outputs["privateEndpointSubnetId"]?["value"]) ==
                   "[resourceId('Microsoft.Network/virtualNetworks/subnets', parameters('virtualNetworkName'), parameters('privateEndpointSubnetName'))]" &&
               Value<string>(outputs["staticEgressPublicIpId"]?["value"]) ==
                   "[resourceId('Microsoft.Network/publicIPAddresses', parameters('staticEgressPublicIpName'))]";
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

static JsonNode Subnet(JsonNode template, string nameParameter)
{
    var matches = ((JsonArray)template["resources"]!).Where(node =>
        Value<string>(node?["type"]) == "Microsoft.Network/virtualNetworks/subnets" &&
        Value<string>(node?["name"]) ==
        $"[format('{{0}}/{{1}}', parameters('virtualNetworkName'), parameters('{nameParameter}'))]").ToArray();
    return matches.Length == 1 ? matches[0]! : throw new InvalidOperationException("Missing or duplicate subnet.");
}

static T Value<T>(JsonNode? node) => node is JsonValue value && value.TryGetValue<T>(out var result)
    ? result
    : throw new InvalidOperationException("Missing or invalid template value.");
