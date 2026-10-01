#!/usr/bin/env python3
"""G1 compiled-template policy checks and unsafe configuration mutations.

Usage: python3 compute-policy.py environment.json observability.json private-link.json
Uses only the standard library. These checks cannot prove provider deployment,
subnet NAT/delegation, runtime redaction, table retention, or private DNS reachability.
"""

import copy
import json
import sys
from collections import Counter
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def resource(template, kind):
    matches = [r for r in template["resources"] if r["type"] == kind]
    require(len(matches) == 1, f"Expected one {kind}")
    return matches[0]


def closed_resources(template, expected):
    require(Counter(r["type"] for r in template["resources"]) == Counter(expected),
            "Unexpected resource: scaffold cannot activate workloads, exports or grants")
    require(all("preview" not in r["apiVersion"] for r in template["resources"]),
            "Stable APIs required")
    # Outputs must contain resource IDs only, never references or listKeys values.
    for value in template.get("outputs", {}).values():
        require(value["type"] == "string" and value["value"].startswith("[resourceId("),
                "Only non-secret resource ID outputs are permitted")
    serialized = json.dumps(template).lower()
    for forbidden in ("listkeys(", "sharedkey", "instrumentationkey", "connectionstring"):
        require(forbidden not in serialized, f"Forbidden secret access/configuration: {forbidden}")


def environment_policy(template):
    closed_resources(template, ["Microsoft.App/managedEnvironments"])
    env = resource(template, "Microsoft.App/managedEnvironments")
    require(env["location"] == "eastus2", "Approved single region required")
    props = env["properties"]
    require(props["publicNetworkAccess"] == "Disabled", "No public ingress")
    require(props["vnetConfiguration"]["internal"] is True, "Internal load balancer required")
    require(props["vnetConfiguration"]["infrastructureSubnetId"] ==
            "[resourceId('Microsoft.Network/virtualNetworks/subnets', parameters('virtualNetworkName'), parameters('containerAppsSubnetName'))]",
            "Existing dedicated subnet required")
    require(props["workloadProfiles"] == [{"name": "Consumption", "workloadProfileType": "Consumption"}],
            "Consumption profile only; no dedicated capacity")
    require(props["appLogsConfiguration"] == {"destination": "azure-monitor"},
            "Azure Monitor routing scaffold only; no shared-key logs")
    require(props["infrastructureResourceGroup"] == "[parameters('infrastructureResourceGroupName')]",
            "Managed group must be explicit for budget coverage")
    require("defaultValue" not in template["parameters"]["infrastructureResourceGroupName"],
            "Do not silently select managed resource group")
    require(set(props) == {"infrastructureResourceGroup", "publicNetworkAccess", "vnetConfiguration",
                           "workloadProfiles", "appLogsConfiguration"},
            "Unreviewed environment configuration")


def observability_policy(template):
    closed_resources(template, ["Microsoft.OperationalInsights/workspaces", "Microsoft.Insights/components"])
    workspace = resource(template, "Microsoft.OperationalInsights/workspaces")
    ai = resource(template, "Microsoft.Insights/components")
    require(workspace["location"] == ai["location"] == "eastus2", "Telemetry residency drift")
    wp, ap = workspace["properties"], ai["properties"]
    for props in (wp, ap):
        for control in ("publicNetworkAccessForIngestion", "publicNetworkAccessForQuery"):
            require(props[control] == "Disabled", "Private monitoring connectivity required")
    require(wp["features"]["disableLocalAuth"] is True and ap["DisableLocalAuth"] is True,
            "Entra-only telemetry authentication required")
    require(wp["features"]["enableDataExport"] is False, "No telemetry export activation")
    require(wp["features"]["enableLogAccessUsingOnlyResourcePermissions"] is False,
            "Explicit workspace reader permissions required")
    require(ap["DisableIpMasking"] is False, "IP masking must remain enabled")
    require(ap["WorkspaceResourceId"] ==
            "[resourceId('Microsoft.OperationalInsights/workspaces', parameters('workspaceName'))]",
            "Application Insights must use the scoped workspace")
    require(ap["IngestionMode"] == "LogAnalytics", "Workspace-based Application Insights required")
    require(set(ap) == {"Application_Type", "WorkspaceResourceId", "IngestionMode", "DisableLocalAuth",
                       "DisableIpMasking", "publicNetworkAccessForIngestion", "publicNetworkAccessForQuery"},
            "No unreviewed Application Insights collection/profiler configuration")
    require(wp["sku"] == {"name": "PerGB2018"}, "No prepaid telemetry capacity")
    require(wp["retentionInDays"] == "[parameters('telemetryRetentionDays')]",
            "Retention must use an explicit owner-approved input")
    retention = template["parameters"]["telemetryRetentionDays"]
    require("defaultValue" not in retention and retention["type"] == "int" and
            retention["allowedValues"] == [30, 60, 90, 120, 180, 270, 365, 550, 730],
            "Bounded explicit retention selection required")
    cap = template["parameters"]["dailyIngestionCapGb"]
    require("defaultValue" not in cap and cap["minValue"] == 1 and
            wp["workspaceCapping"] == {"dailyQuotaGb": "[parameters('dailyIngestionCapGb')]"},
            "Explicit positive daily cap required")
    require(set(wp) == {"sku", "retentionInDays", "workspaceCapping", "features",
                       "publicNetworkAccessForIngestion", "publicNetworkAccessForQuery"},
            "No unreviewed workspace failover, replication, or collection rule")


def private_link_policy(template):
    closed_resources(template, ["Microsoft.Insights/privateLinkScopes",
                               "Microsoft.Insights/privateLinkScopes/scopedResources",
                               "Microsoft.Insights/privateLinkScopes/scopedResources",
                               "Microsoft.Network/privateEndpoints",
                               "Microsoft.Network/privateEndpoints/privateDnsZoneGroups"])
    scope = resource(template, "Microsoft.Insights/privateLinkScopes")
    require(scope["location"] == "global", "AMPLS metadata resource must be global")
    require(scope["properties"]["accessModeSettings"] == {
        "ingestionAccessMode": "PrivateOnly", "queryAccessMode": "PrivateOnly", "exclusions": []},
        "No open access or private endpoint exclusions")
    links = [r for r in template["resources"] if r["type"].endswith("/scopedResources")]
    require({r["properties"]["linkedResourceId"] for r in links} == {
        "[resourceId('Microsoft.OperationalInsights/workspaces', parameters('workspaceName'))]",
        "[resourceId('Microsoft.Insights/components', parameters('applicationInsightsName'))]"},
        "Scope must link exactly the pilot workspace and Application Insights")
    endpoint = resource(template, "Microsoft.Network/privateEndpoints")
    require(endpoint["location"] == "eastus2", "Private endpoint residency drift")
    ep = endpoint["properties"]
    require(ep["subnet"]["id"] ==
            "[resourceId('Microsoft.Network/virtualNetworks/subnets', parameters('virtualNetworkName'), parameters('privateEndpointSubnetName'))]",
            "Existing dedicated private endpoint subnet required")
    require(ep["privateLinkServiceConnections"] == [{"name": "azure-monitor", "properties": {
        "privateLinkServiceId": "[resourceId('Microsoft.Insights/privateLinkScopes', parameters('privateLinkScopeName'))]",
        "groupIds": ["azuremonitor"]}}], "Exactly one Azure Monitor private service connection required")
    require("manualPrivateLinkServiceConnections" not in ep, "No unreviewed manual connection")
    require(len(endpoint["dependsOn"]) == 3 and
            sum("/scopedResources'" in d for d in endpoint["dependsOn"]) == 2,
            "Both monitor resources must be scoped before endpoint creation")
    zones = template["variables"]["monitorZoneNames"]
    require(zones == ["privatelink.monitor.azure.com", "privatelink.oms.opinsights.azure.com",
                      "privatelink.ods.opinsights.azure.com", "privatelink.agentsvc.azure-automation.net",
                      "[format('privatelink.blob.{0}', environment().suffixes.storage)]"],
            "All five documented monitor DNS zones required")
    group = resource(template, "Microsoft.Network/privateEndpoints/privateDnsZoneGroups")
    require(group["properties"]["copy"] == [{"name": "privateDnsZoneConfigs",
        "count": "[length(variables('monitorZoneNames'))]", "input": {
            "name": "[replace(variables('monitorZoneNames')[copyIndex('privateDnsZoneConfigs')], '.', '-')]",
            "properties": {"privateDnsZoneId":
                "[resourceId('Microsoft.Network/privateDnsZones', variables('monitorZoneNames')[copyIndex('privateDnsZoneConfigs')])]"}}}],
        "Private endpoint must bind every required DNS zone")


def set_path(template, path, value):
    target = template
    for part in path[:-1]:
        target = target[part]
    target[path[-1]] = value


def mutations(template, policy, cases):
    policy(template)
    for label, path, value in cases:
        mutated = copy.deepcopy(template)
        set_path(mutated, path, value)
        try:
            policy(mutated)
        except (ValueError, KeyError):
            continue
        raise AssertionError(f"Unsafe mutation escaped: {label}")
    print(f"PASS: baseline and {len(cases)} unsafe drift cases")


def main():
    require(len(sys.argv) == 4, "Supply compiled environment, observability, private-link JSON paths")
    env, obs, link = [json.loads(Path(path).read_text()) for path in sys.argv[1:]]
    ep = ["resources", 0, "properties"]
    mutations(env, environment_policy, [
        ("external environment", ep + ["vnetConfiguration", "internal"], False),
        ("public network", ep + ["publicNetworkAccess"], "Enabled"),
        ("wrong subnet", ep + ["vnetConfiguration", "infrastructureSubnetId"], "/synthetic/wrong"),
        ("dedicated profile", ep + ["workloadProfiles"], [{"name": "D4", "workloadProfileType": "D4"}]),
        ("shared key logging", ep + ["appLogsConfiguration"], {"destination": "log-analytics"}),
        ("uncovered managed group", ep + ["infrastructureResourceGroup"], "hidden-group"),
        ("extra workload", ["resources"], env["resources"] + [{"type": "Microsoft.App/containerApps"}]),
        ("secret output", ["outputs", "environmentResourceId", "value"], "[listKeys('synthetic', '2025-01-01')]"),
        ("wrong region", ["resources", 0, "location"], "westus"),
    ])
    wp, ap = ["resources", 0, "properties"], ["resources", 1, "properties"]
    mutations(obs, observability_policy, [
        ("workspace public ingest", wp + ["publicNetworkAccessForIngestion"], "Enabled"),
        ("workspace public query", wp + ["publicNetworkAccessForQuery"], "Enabled"),
        ("AI public ingest", ap + ["publicNetworkAccessForIngestion"], "Enabled"),
        ("AI public query", ap + ["publicNetworkAccessForQuery"], "Enabled"),
        ("workspace key auth", wp + ["features", "disableLocalAuth"], False),
        ("AI key auth", ap + ["DisableLocalAuth"], False),
        ("export activation", wp + ["features", "enableDataExport"], True),
        ("resource-only read", wp + ["features", "enableLogAccessUsingOnlyResourcePermissions"], True),
        ("unmasked IP", ap + ["DisableIpMasking"], True),
        ("wrong workspace", ap + ["WorkspaceResourceId"], "/synthetic/other"),
        ("implicit retention", ["parameters", "telemetryRetentionDays", "defaultValue"], 365),
        ("unbounded retention", ["parameters", "telemetryRetentionDays", "allowedValues"], [30, 9999]),
        ("hardcoded retention", wp + ["retentionInDays"], 365),
        ("unlimited cap", wp + ["workspaceCapping", "dailyQuotaGb"], -1),
        ("cap default", ["parameters", "dailyIngestionCapGb", "defaultValue"], 1),
        ("console export", ["resources"], obs["resources"] + [{"type": "Microsoft.Insights/diagnosticSettings"}]),
        ("telemetry credential output", ["outputs", "workspaceResourceId", "value"], "[listKeys('synthetic', '2025-01-01')]"),
        ("cross-region replication", wp + ["replication"], {"enabled": True, "location": "westus"}),
        ("unreviewed profiler", ap + ["ForceCustomerStorageForProfiler"], False),
    ])
    sp = ["resources", 0, "properties", "accessModeSettings"]
    pp = ["resources", 3, "properties"]
    mutations(link, private_link_policy, [
        ("open ingestion", sp + ["ingestionAccessMode"], "Open"),
        ("open queries", sp + ["queryAccessMode"], "Open"),
        ("endpoint exception", sp + ["exclusions"], [{"queryAccessMode": "Open"}]),
        ("wrong workspace link", ["resources", 1, "properties", "linkedResourceId"], "/synthetic/other"),
        ("wrong AI link", ["resources", 2, "properties", "linkedResourceId"], "/synthetic/other"),
        ("wrong PE subnet", pp + ["subnet", "id"], "/synthetic/other-subnet"),
        ("wrong PE target", pp + ["privateLinkServiceConnections", 0, "properties", "privateLinkServiceId"], "/synthetic/other-scope"),
        ("wrong PE group", pp + ["privateLinkServiceConnections", 0, "properties", "groupIds"], ["blob"]),
        ("missing DNS zone", ["variables", "monitorZoneNames"], link["variables"]["monitorZoneNames"][:-1]),
        ("incomplete DNS binding", ["resources", 4, "properties", "copy", 0, "count"], 1),
        ("link race", ["resources", 3, "dependsOn"], []),
        ("extra grant", ["resources"], link["resources"] + [{"type": "Microsoft.Authorization/roleAssignments"}]),
    ])
    print("Local policy evidence only; deployment, redaction, retention and private-path tests remain NOT VERIFIED.")


if __name__ == "__main__":
    main()
