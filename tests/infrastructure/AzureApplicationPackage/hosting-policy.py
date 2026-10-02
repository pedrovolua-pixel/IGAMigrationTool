#!/usr/bin/env python3
"""Independent compiled-root/module checks for the inert development package.

Usage: python3 hosting-policy.py ROOT.json MODULE.json
Checks definition drift only; cannot execute ARM fail guards or prove image pulls.
"""

import copy
import json
import sys
from functools import reduce
from pathlib import Path


INPUTS = {"environmentName", "registryName", "webIdentityName", "workerIdentityName",
          "webAppName", "workerAppName", "imageRepository", "imageDigest"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def p(name):
    return f"parameters('{name}')"


def call(name, *args):
    return f"{name}({', '.join(args)})"


def all_conditions(conditions):
    return reduce(lambda left, right: call("and", left, right), conditions)


def reference(kind, input_name, api, full=False):
    return call("reference", call("resourceId", repr(kind), p(input_name)), repr(api),
                *(["'full'"] if full else []))


def eq(left, right):
    return call("equals", left, right)


def expected_bindings():
    env = reference("Microsoft.App/managedEnvironments", "environmentName", "2026-01-01")
    env_full = reference("Microsoft.App/managedEnvironments", "environmentName", "2026-01-01", True)
    profiles = call("filter", env + ".workloadProfiles", "lambda('profile', and(equals(lambdaVariables('profile').name, 'Consumption'), equals(lambdaVariables('profile').workloadProfileType, 'Consumption')))")
    env_conditions = [eq(call("toLower", env_full + ".location"), "'eastus2'"),
                      eq(env + ".publicNetworkAccess", "'Disabled'"),
                      eq(env + ".vnetConfiguration.internal", "true()"), eq(call("length", profiles), "1")]
    env_id = call("if", all_conditions(env_conditions),
                  call("resourceId", "'Microsoft.App/managedEnvironments'", p("environmentName")),
                  "fail('Require the existing private internal East US 2 Consumption environment.')")
    reg = reference("Microsoft.ContainerRegistry/registries", "registryName", "2025-11-01")
    reg_full = reference("Microsoft.ContainerRegistry/registries", "registryName", "2025-11-01", True)
    reg_conditions = [eq(call("toLower", reg_full + ".location"), "'eastus2'"),
                      eq(reg_full + ".sku.name", "'Premium'"), eq(reg + ".publicNetworkAccess", "'Disabled'"),
                      eq(reg + ".adminUserEnabled", "false()"), eq(reg + ".anonymousPullEnabled", "false()"),
                      eq(reg + ".networkRuleBypassOptions", "'None'")]
    reg_server = call("if", all_conditions(reg_conditions), reg + ".loginServer",
                      "fail('Require the existing private Premium registry with local and anonymous authentication disabled and no network bypass.')")
    image = call("format", "'{0}/{1}@sha256:{2}'", reg_server, p("imageRepository"), "variables('validatedDigest')")
    return "[" + env_id + "]", "[" + reg_server + "]", "[" + image + "]"


def module_policy(template):
    require(set(template["parameters"]) == INPUTS, "Unexpected inputs")
    require(all("defaultValue" not in value for value in template["parameters"].values()), "Explicit inputs required")
    require(template["parameters"]["imageRepository"]["allowedValues"] == ["iga/azure-development-bootstrap"], "Repository must be allowlisted")
    digest = template["parameters"]["imageDigest"]
    require(digest["minLength"] == 64 and digest["maxLength"] == 64, "Digest length lock missing")
    require(not template.get("outputs"), "No output or secret export permitted")
    variables = template["variables"]
    require(variables["hexAlphabet"] == "0123456789abcdef", "Lowercase SHA256 only")
    require(variables["digestValid"] == "[and(and(equals(length(parameters('imageDigest')), 64), not(equals(parameters('imageDigest'), '0000000000000000000000000000000000000000000000000000000000000000'))), equals(length(filter(range(0, length(parameters('imageDigest'))), lambda('i', not(contains(variables('hexAlphabet'), substring(parameters('imageDigest'), lambdaVariables('i'), 1)))))), 0))]", "Digest guard weakened")
    for variable, first, second in (("identityNamesDistinct", "webIdentityName", "workerIdentityName"),
                                     ("appNamesDistinct", "webAppName", "workerAppName")):
        require(variables[variable] == f"[not(equals(toLower(parameters('{first}')), toLower(parameters('{second}'))))]", "Case-insensitive identity/name separation required")
    require(variables["validatedDigest"] == "[if(and(and(variables('digestValid'), variables('identityNamesDistinct')), variables('appNamesDistinct')), parameters('imageDigest'), fail('Require a real lowercase SHA256 image digest, distinct workload identities and distinct application names.'))]", "Invalid-input guard must fail closed")
    resources = template["resources"]
    require(len(resources) == 2 and all(r["type"] == "Microsoft.App/containerApps" for r in resources), "Exactly two apps; no grants or other resources")
    env_id, reg_server, image = expected_bindings()
    for role in ("web", "worker"):
        app = next(r for r in resources if r["name"] == f"[{p(role + 'AppName')}]")
        require(app["apiVersion"] == "2026-01-01" and app["location"] == "eastus2", "Approved stable API and region required")
        identity_id = call("resourceId", "'Microsoft.ManagedIdentity/userAssignedIdentities'", p(role + "IdentityName"))
        require(app["identity"] == {"type": "UserAssigned", "userAssignedIdentities": {
            "[" + call("format", "'{0}'", identity_id) + "]": {}}}, "Workload identity binding changed")
        props = app["properties"]
        require(set(props) == {"managedEnvironmentId", "workloadProfileName", "configuration", "template"}, "Unreviewed app property")
        require(props["managedEnvironmentId"] == env_id and props["workloadProfileName"] == "Consumption", "Private environment/Consumption binding weakened")
        expected_config = {"activeRevisionsMode": "Single", "registries": [{"server": reg_server, "identity": "[" + identity_id + "]"}]}
        if role == "web":
            expected_config["ingress"] = {"external": False, "allowInsecure": False, "targetPort": 8080, "transport": "http"}
        require(props["configuration"] == expected_config, "Public/worker ingress, secret registry auth or additional config prohibited")
        template_props = props["template"]
        require(set(template_props) == {"containers", "scale"}, "No extra workload settings")
        require(template_props["scale"] == {"minReplicas": 1, "maxReplicas": 1, "rules": []}, "Bounded inert test replicas only")
        probes = [{"type": "Liveness", "httpGet": {"path": "/health/live", "port": 8080, "scheme": "HTTP"},
                   "initialDelaySeconds": 5, "periodSeconds": 10, "timeoutSeconds": 2, "failureThreshold": 3}] if role == "web" else []
        require(template_props["containers"] == [{"name": "bootstrap-" + role, "image": image,
            "command": ["dotnet", "AzureDevelopmentBootstrap.dll"],
            "args": ["--azure-development-bootstrap", "--role", role], "probes": probes,
            "resources": {"cpu": "0.25", "memory": "0.5Gi"}}], "Image/args/probes/resource bounds or env/secret wiring changed")


def root_policy(root, module):
    require(set(root["parameters"]) == INPUTS and not root.get("outputs"), "Root inputs/outputs changed")
    require(all("defaultValue" not in value for value in root["parameters"].values()), "Root inputs must be explicit")
    resources = root["resources"]
    require(len(resources) == 1 and resources[0]["type"] == "Microsoft.Resources/deployments", "Root must only compose reviewed module")
    props = resources[0]["properties"]
    require(props["mode"] == "Incremental", "Destructive deployment mode prohibited")
    require(props["parameters"] == {name: {"value": "[" + p(name) + "]"} for name in INPUTS}, "Root identity/image parameter substitution")
    require(props["template"] == module, "Root nested template must match independently verified module")
    module_policy(props["template"])


def mutate(template, path, value):
    changed = copy.deepcopy(template)
    target = changed
    for part in path[:-1]:
        target = target[part]
    target[path[-1]] = value
    return changed


def denied(check, value, label):
    try:
        check(value)
    except (ValueError, KeyError, StopIteration, TypeError):
        return
    raise AssertionError("Unsafe drift accepted: " + label)


def main():
    require(len(sys.argv) == 3, __doc__)
    root, module = [json.loads(Path(path).read_text()) for path in sys.argv[1:]]
    module_policy(module)
    root_policy(root, module)
    cases = [(('variables', 'digestValid'), '[true()]'),
             (('variables', 'identityNamesDistinct'), '[true()]'),
             (('variables', 'appNamesDistinct'), '[true()]'),
             (('variables', 'validatedDigest'), "[parameters('imageDigest')]"),
             (('parameters', 'imageDigest', 'defaultValue'), '0' * 64),
             (('parameters', 'imageRepository', 'allowedValues'), ['unreviewed/repository']),
             (('outputs',), {'credential': {'type': 'string', 'value': "[listKeys('synthetic', '2026-01-01')]"}})]
    for index in (0, 1):
        app = ('resources', index)
        config = app + ('properties', 'configuration')
        container = app + ('properties', 'template', 'containers', 0)
        cases += [(app + ('apiVersion',), '2025-07-01'), (app + ('location',), 'westus'),
                  (app + ('identity', 'type'), 'SystemAssigned'),
                  (app + ('properties', 'managedEnvironmentId'), '/synthetic/public-environment'),
                  (app + ('properties', 'workloadProfileName'), 'D4'),
                  (config + ('registries', 0, 'server'), 'public.example'),
                  (config + ('registries', 0, 'identity'), '/synthetic/other-identity'),
                  (config + ('secrets',), [{'name': 'secret', 'value': 'synthetic'}]),
                  (container + ('image',), 'public.example/bootstrap:latest'),
                  (container + ('command',), ['sh', '-c', 'unreviewed']),
                  (container + ('args',), ['--role', 'web']),
                  (container + ('env',), [{'name': 'SECRET', 'value': 'synthetic'}]),
                  (container + ('resources', 'cpu'), '2'),
                  (app + ('properties', 'template', 'scale', 'maxReplicas'), 5)]
    cases += [(('resources', 0, 'properties', 'configuration', 'ingress', 'external'), True),
              (('resources', 0, 'properties', 'configuration', 'ingress', 'allowInsecure'), True),
              (('resources', 0, 'properties', 'template', 'containers', 0, 'probes', 0, 'type'), 'Readiness'),
              (('resources', 1, 'properties', 'configuration', 'ingress'), {'external': True}),
              (('resources',), module['resources'] + [{'type': 'Microsoft.Authorization/roleAssignments'}])]
    for path, value in cases:
        denied(module_policy, mutate(module, path, value), '/'.join(map(str, path)))
    root_cases = [(('resources', 0, 'properties', 'mode'), 'Complete'),
                  (('resources', 0, 'properties', 'parameters', 'workerIdentityName', 'value'), "[parameters('webIdentityName')]"),
                  (('resources', 0, 'properties', 'parameters', 'imageDigest', 'value'), '0' * 64),
                  (('outputs',), {'secret': {'value': 'synthetic'}})]
    for path, value in root_cases:
        denied(lambda value: root_policy(value, module), mutate(root, path, value), 'root substitution')
    print(f"PASS: compiled root/module baselines; {len(cases) + len(root_cases)} unsafe hosting drifts denied.")
    print("NOT VERIFIED: ARM runtime guard execution, image build/run/scan, identity pull grants, DNS and deployed G1.")


if __name__ == '__main__':
    main()
