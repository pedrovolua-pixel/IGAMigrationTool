#!/usr/bin/env python3
"""Check the compiled PostgreSQL scaffold and reject synthetic unsafe drifts.

Usage: python3 postgresql-policy.py COMPILED_TEMPLATE [--parameters ARM_PARAMETERS]
Only structural/configuration evidence; no Azure connection, token or SQL test.
"""

import argparse
import copy
import json
import re
import uuid
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def parameter(name):
    return f"[parameters('{name}')]"


SERVER = "Microsoft.DBforPostgreSQL/flexibleServers"
SERVER_ID = "[resourceId('Microsoft.DBforPostgreSQL/flexibleServers', parameters('serverName'))]"
PE = "Microsoft.Network/privateEndpoints"
PE_ID = "[resourceId('Microsoft.Network/privateEndpoints', format('{0}-pe', parameters('serverName')))]"
PARAMETERS = {
    "serverName", "skuName", "skuTier", "storageSizeGB", "storageAutoGrow",
    "backupRetentionDays", "administratorPrincipalId", "administratorPrincipalName",
    "administratorPrincipalType", "entraTenantId", "privateEndpointSubnetId", "privateDnsZoneId",
}


def policy(template):
    inputs = template["parameters"]
    require(set(inputs) == PARAMETERS, "Unexpected or missing deployment input")
    require(all("defaultValue" not in p for p in inputs.values()), "Deployment inputs must be explicit")
    require(inputs["backupRetentionDays"]["type"] == "int"
            and inputs["backupRetentionDays"].get("minValue") == 7
            and inputs["backupRetentionDays"].get("maxValue") == 35, "Backup bounds must be 7–35")
    require(inputs["administratorPrincipalType"].get("allowedValues")
            == ["User", "Group", "ServicePrincipal"], "Unknown administrator type prohibited")
    for name in ("entraTenantId", "administratorPrincipalId"):
        require(inputs[name].get("minLength") == 36 and inputs[name].get("maxLength") == 36,
                "Entra identifiers require explicit bounded inputs")
    require(template.get("variables", {}).get("location") == "eastus2", "Region must be East US 2")
    resources = template["resources"]
    types = [r["type"] for r in resources]
    require(sorted(types) == sorted([SERVER, SERVER + "/administrators",
                                    SERVER + "/configurations", SERVER + "/configurations",
                                    PE, PE + "/privateDnsZoneGroups"]),
            "Only server, administrator, TLS settings, private endpoint and DNS group permitted")
    for resource in resources:
        require(resource["apiVersion"] == ("2025-08-01" if resource["type"].startswith(SERVER)
                                          else "2025-05-01"), "Stable API lock changed")
    server = next(r for r in resources if r["type"] == SERVER)
    require(server["location"] == "[variables('location')]", "Server region must use locked region")
    require(server["name"] == parameter("serverName"), "Server name must use explicit input")
    require(server["sku"] == {"name": parameter("skuName"), "tier": parameter("skuTier")},
            "Compute size must use reviewed inputs")
    properties = server["properties"]
    require(properties["version"] == "18" and properties["createMode"] == "Default",
            "Only new approved major-18 primary permitted")
    require(properties["authConfig"] == {"activeDirectoryAuth": "Enabled", "passwordAuth": "Disabled",
                                          "tenantId": parameter("entraTenantId")}, "Entra-only auth required")
    require(properties["highAvailability"] == {"mode": "Disabled"}, "Pilot HA must remain disabled")
    require(properties["backup"] == {"backupRetentionDays": parameter("backupRetentionDays"),
                                     "geoRedundantBackup": "Disabled"}, "Explicit local backup policy required")
    require(properties["network"] == {"publicNetworkAccess": "Disabled"}, "Private Link only, public disabled")
    require(properties["storage"] == {"storageSizeGB": parameter("storageSizeGB"),
                                      "autoGrow": parameter("storageAutoGrow"), "type": "Premium_LRS"},
            "Storage size/growth must use explicit cost-reviewed inputs")
    require(set(properties) == {"createMode", "version", "authConfig", "highAvailability", "backup",
                               "network", "storage"}, "Undocumented server settings or password prohibited")
    admin = next(r for r in resources if r["type"] == SERVER + "/administrators")
    require(admin["name"] == "[format('{0}/{1}', parameters('serverName'), parameters('administratorPrincipalId'))]",
            "Administrator must be explicit principal, not deployer")
    require(admin["properties"] == {"principalName": parameter("administratorPrincipalName"),
                                     "principalType": parameter("administratorPrincipalType"),
                                     "tenantId": parameter("entraTenantId")}, "Administrator tenant/binding mismatch")
    dns_id = "[resourceId('Microsoft.Network/privateEndpoints/privateDnsZoneGroups', format('{0}-pe', parameters('serverName')), 'default')]"
    tls_id = "[resourceId('Microsoft.DBforPostgreSQL/flexibleServers/configurations', parameters('serverName'), 'ssl_min_protocol_version')]"
    require(set(admin["dependsOn"]) == {SERVER_ID, dns_id, tls_id},
            "Administrator must wait for TLS configuration and private DNS")
    for name, value in (("require_secure_transport", "ON"), ("ssl_min_protocol_version", "TLSv1.2")):
        config = next(r for r in resources if r["type"] == SERVER + "/configurations"
                      and r["name"] == f"[format('{{0}}/{{1}}', parameters('serverName'), '{name}')]")
        require(config["properties"] == {"value": value, "source": "user-override"}, "TLS weakening prohibited")
        expected = {SERVER_ID}
        if name == "ssl_min_protocol_version":
            expected.add("[resourceId('Microsoft.DBforPostgreSQL/flexibleServers/configurations', parameters('serverName'), 'require_secure_transport')]")
        require(set(config["dependsOn"]) == expected, "TLS changes must run in sequence after server")
    endpoint = next(r for r in resources if r["type"] == PE)
    require(endpoint["location"] == "[variables('location')]", "Endpoint region must use locked region")
    require(endpoint["properties"]["subnet"] == {"id": parameter("privateEndpointSubnetId")}, "Wrong PE subnet binding")
    connection = endpoint["properties"]["privateLinkServiceConnections"]
    require(len(connection) == 1 and connection[0]["properties"] == {
        "privateLinkServiceId": SERVER_ID, "groupIds": ["postgresqlServer"]}, "Wrong private-link target/subresource")
    require(set(endpoint["properties"]) == {"subnet", "privateLinkServiceConnections"}, "Unexpected endpoint configuration")
    require(endpoint["dependsOn"] == [SERVER_ID], "Endpoint must follow server")
    dns = next(r for r in resources if r["type"] == PE + "/privateDnsZoneGroups")
    require(dns["properties"] == {"privateDnsZoneConfigs": [{"name": "postgresql", "properties": {
        "privateDnsZoneId": parameter("privateDnsZoneId")}}]}, "Wrong private DNS binding")
    require(dns["dependsOn"] == [PE_ID], "DNS group must follow endpoint")
    require(set(template["outputs"]) == {"serverResourceId", "serverFqdn", "privateEndpointResourceId"},
            "Only non-secret references may be outputs")
    require(template["outputs"]["serverResourceId"]["value"] == SERVER_ID
            and template["outputs"]["privateEndpointResourceId"]["value"] == PE_ID
            and template["outputs"]["serverFqdn"]["value"] ==
            "[reference(resourceId('Microsoft.DBforPostgreSQL/flexibleServers', parameters('serverName')), '2025-08-01').fullyQualifiedDomainName]",
            "Secret/credential output prohibited")


def configuration(values):
    require(set(values) == PARAMETERS, "All explicit configuration inputs required")
    for name in ("entraTenantId", "administratorPrincipalId"):
        require(str(uuid.UUID(values[name])) == values[name].lower()
                and uuid.UUID(values[name]).int != 0, "Invalid Entra identifier")
    require(values["administratorPrincipalType"] in ("User", "Group", "ServicePrincipal"), "Invalid administrator type")
    require(isinstance(values["administratorPrincipalName"], str)
            and values["administratorPrincipalName"].strip(), "Administrator display name required")
    require(type(values["backupRetentionDays"]) is int and 7 <= values["backupRetentionDays"] <= 35,
            "Invalid rolling retention")
    require(type(values["storageSizeGB"]) is int and values["storageSizeGB"] >= 32, "Invalid storage size")
    require(values["storageAutoGrow"] in ("Enabled", "Disabled"), "Invalid storage growth choice")
    require(values["skuTier"] in ("Burstable", "GeneralPurpose", "MemoryOptimized")
            and isinstance(values["skuName"], str) and values["skuName"].strip(), "Explicit reviewed compute required")
    require(isinstance(values["serverName"], str) and 3 <= len(values["serverName"]) <= 63
            and re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", values["serverName"]), "Invalid server name")
    base = r"/subscriptions/[0-9a-fA-F-]{36}/resourceGroups/[^/]+/providers/Microsoft.Network/"
    require(re.fullmatch(base + r"virtualNetworks/[^/]+/subnets/[^/]+", values["privateEndpointSubnetId"]),
            "Expected existing private-endpoint subnet ID")
    require(re.fullmatch(base + r"privateDnsZones/privatelink\.postgres\.database\.azure\.com", values["privateDnsZoneId"]),
            "Expected PostgreSQL Private Link DNS zone ID")


def reject(check, value, label):
    try:
        check(value)
    except (ValueError, KeyError, StopIteration, TypeError):
        return
    raise AssertionError(f"Unsafe mutation accepted: {label}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("template", type=Path)
    parser.add_argument("--parameters", type=Path)
    args = parser.parse_args()
    template = json.loads(args.template.read_text())
    policy(template)
    mutations = [
        (("variables", "location"), "westus"),
        (("resources", 0, "apiVersion"), "2026-04-01-preview"),
        (("resources", 0, "properties", "version"), "17"),
        (("resources", 0, "properties", "createMode"), "GeoRestore"),
        (("resources", 0, "properties", "authConfig", "passwordAuth"), "Enabled"),
        (("resources", 0, "properties", "authConfig", "activeDirectoryAuth"), "Disabled"),
        (("resources", 0, "properties", "authConfig", "tenantId"), "foreign-tenant"),
        (("resources", 0, "properties", "administratorLoginPassword"), "synthetic-forbidden-password"),
        (("resources", 0, "properties", "highAvailability", "mode"), "ZoneRedundant"),
        (("resources", 0, "properties", "backup", "geoRedundantBackup"), "Enabled"),
        (("resources", 0, "properties", "backup", "backupRetentionDays"), 36),
        (("resources", 0, "properties", "network", "publicNetworkAccess"), "Enabled"),
        (("resources", 0, "properties", "network", "delegatedSubnetResourceId"), "synthetic-subnet"),
        (("resources", 1, "name"), "[deployer().objectId]"),
        (("resources", 1, "properties", "tenantId"), "foreign-tenant"),
        (("resources", 2, "properties", "value"), "OFF"),
        (("resources", 3, "properties", "value"), "TLSv1.1"),
        (("resources", 4, "properties", "subnet", "id"), "wrong-subnet"),
        (("resources", 4, "properties", "privateLinkServiceConnections", 0, "properties", "groupIds"), ["sqlServer"]),
        (("resources", 4, "properties", "privateLinkServiceConnections", 0, "properties", "privateLinkServiceId"), "foreign-server"),
        (("resources", 5, "properties", "privateDnsZoneConfigs", 0, "properties", "privateDnsZoneId"), "wrong-zone"),
        (("parameters", "backupRetentionDays", "maxValue"), 36),
        (("parameters", "backupRetentionDays", "defaultValue"), 7),
        (("parameters", "administratorPrincipalType", "allowedValues"), ["Unknown"]),
        (("outputs", "serverFqdn", "value"), "synthetic-forbidden-password"),
        (("resources", 4, "dependsOn"), []),
        (("resources", 1, "dependsOn"), [SERVER_ID]),
        (("resources", 3, "dependsOn"), [SERVER_ID]),
    ]
    for path, value in mutations:
        altered = copy.deepcopy(template)
        target = altered
        for part in path[:-1]:
            target = target[part]
        target[path[-1]] = value
        reject(policy, altered, "/".join(map(str, path)))
    altered = copy.deepcopy(template)
    altered["resources"].append({"type": SERVER + "/firewallRules", "properties": {
        "startIpAddress": "0.0.0.0", "endIpAddress": "255.255.255.255"}})
    reject(policy, altered, "public firewall")
    prefix = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/synthetic/providers/Microsoft.Network/"
    values = dict(serverName="synthetic-pg", skuName="Standard_B1ms", skuTier="Burstable",
                  storageSizeGB=32, storageAutoGrow="Disabled", backupRetentionDays=7,
                  administratorPrincipalId="22222222-2222-2222-2222-222222222222",
                  administratorPrincipalName="Synthetic administrator", administratorPrincipalType="Group",
                  entraTenantId="33333333-3333-3333-3333-333333333333",
                  privateEndpointSubnetId=prefix + "virtualNetworks/synthetic/subnets/private-endpoints",
                  privateDnsZoneId=prefix + "privateDnsZones/privatelink.postgres.database.azure.com")
    configuration(values)
    for days in (7, 35):
        boundary = dict(values, backupRetentionDays=days)
        configuration(boundary)
    bad_inputs = [("backupRetentionDays", 6), ("backupRetentionDays", 36), ("backupRetentionDays", True),
                  ("administratorPrincipalId", ""), ("entraTenantId", "00000000-0000-0000-0000-000000000000"),
                  ("administratorPrincipalType", "Unknown"), ("administratorPrincipalName", "  "),
                  ("privateEndpointSubnetId", "public-subnet"),
                  ("privateDnsZoneId", prefix + "privateDnsZones/postgres.database.azure.com"),
                  ("serverName", "UPPERCASE"), ("storageSizeGB", 1), ("storageAutoGrow", "Unreviewed"),
                  ("skuTier", "Unknown")]
    for name, value in bad_inputs:
        reject(configuration, dict(values, **{name: value}), name)
    missing = dict(values)
    del missing["administratorPrincipalId"]
    reject(configuration, missing, "missing approved administrator")
    if args.parameters:
        document = json.loads(args.parameters.read_text())
        configuration({name: entry["value"] for name, entry in document["parameters"].items()})
    print(f"PASS PostgreSQL compiled policy; {len(mutations) + 1} unsafe template drifts rejected; "
          f"{len(bad_inputs) + 1} invalid synthetic inputs rejected; 7/35-day boundaries accepted.")
    print("NOT VERIFIED: Azure validation/deployment, capacity, administrator approval, DNS resolution, "
          "managed-identity SQL access, cross-customer denial, outage and deletion-safe PITR.")


if __name__ == "__main__":
    main()
