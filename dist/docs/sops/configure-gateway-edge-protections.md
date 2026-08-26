# Standard Operating Procedure: Configure Gateway Edge Protections

## Metadata

- **SOP ID:** SOP-configure-gateway-edge-protections
- **Last Reviewed:** (document-date-compact)
- **Effective Date:** (document-date-compact)
- **Next Review:** Set a concrete `YYYYMMDD` date during repository adoption
- **Owner:** (authors)
- **Approver:** Repository owners

## Purpose

Configure and verify the Gateway's token-bucket rate limits, CIDR safelists, and forwarded-header trust without
accidentally trusting caller-supplied addresses or locking out an intended surface.

## Scope

**In Scope:**

- `Gateway:RateLimiting` policies for proxied traffic and the reference Webhook adapter.
- `Gateway:NetworkSecurity:IpSafelists` policies for proxy, operations, and Webhook surfaces.
- `Gateway:NetworkSecurity:ForwardedHeaders` when the Gateway runs behind known reverse proxies.
- Startup validation, deployment, verification, and rollback of those settings.

**Out of Scope:**

- Globally coordinated quotas at ingress, WAF, or API-management level.
- Host authentication and authorization, network firewalls, secret rotation, and denial-of-service capacity planning.
- Selecting trusted networks or acceptable traffic budgets on behalf of the owning security and platform teams.

## Definitions

| Term | Definition |
|------|------------|
| Client address | The trusted address used to partition rate limits and evaluate CIDR safelists. |
| Known proxy/network | A reverse-proxy address or canonical CIDR that is permitted to supply forwarded headers. |
| Safelist policy | A named authorization policy that permits only configured exact addresses or CIDR networks when enabled. |
| Token bucket | A rate limiter that spends one token per request and replenishes tokens on a fixed interval. |

## Frequency

Perform this procedure when a deployment topology, trusted proxy chain, allowed source network, or traffic
budget changes. Revalidate it before the affected deployment receives production traffic.

## Roles Responsible

- Application owner - approves which Gateway surfaces are exposed and the traffic budget for each surface.
- Platform or network owner - supplies authoritative proxy addresses, proxy-hop count, and source CIDRs.
- Implementer - changes configuration, restarts the Gateway, records evidence, and performs rollback if needed.
- Reviewer - verifies that the supplied trust boundary is narrow, explicit, and tested outside production first.

## Prerequisites

- Access to the deployment-owned Gateway configuration and its normal deployment/restart mechanism.
- Authoritative canonical CIDRs for allowed callers and exact addresses or CIDRs for every trusted reverse proxy.
- A non-production environment with the same proxy topology as the target environment.
- A saved copy or version-control reference for the currently deployed settings.
- PowerShell 7 for endpoint checks.

## Procedure

### 1. Record the intended trust boundary

For each protected surface, record its expected callers, whether traffic reaches the Gateway directly or through
reverse proxies, and the number of trusted proxy hops. Do not infer these values from an incoming
`X-Forwarded-For` header.

The named safelist policies are:

| Policy | Protected surface |
|---|---|
| `gateway-proxy-ip-safelist` | YARP routes to the Host and selected Web client. |
| `gateway-operations-ip-safelist` | Prometheus `/metrics`. |
| `gateway-webhook-ip-safelist` | The reference Webhook adapter when generated. Keep this required configuration entry even when that adapter is absent. |

**Expected result:** every address and network has an identified owner and a reason to be trusted.

### 2. Configure token-bucket limits

Start from the generated baseline in `src/BurcinCo.BurcinApp.Gateway/appsettings.json` and override it through the
deployment's normal configuration source. The complete rate-limiting section must retain valid positive settings
for both named policies:

```json
{
  "Gateway": {
    "RateLimiting": {
      "Proxy": {
        "TokenLimit": 200,
        "TokensPerPeriod": 50,
        "ReplenishmentPeriod": "00:00:05",
        "QueueLimit": 0
      },
      "Webhook": {
        "TokenLimit": 30,
        "TokensPerPeriod": 10,
        "ReplenishmentPeriod": "00:00:10",
        "QueueLimit": 0
      }
    }
  }
}
```

Set `TokenLimit` to the permitted burst, `TokensPerPeriod` to no more than that limit, and
`ReplenishmentPeriod` to a positive duration. `QueueLimit: 0` rejects excess work immediately; increase it only
when waiting is an intentional and capacity-tested part of the public contract.

**Expected result:** the values reflect measured capacity and the Gateway starts without rate-limit validation
errors.

### 3. Configure CIDR safelists

Safelists default to disabled. Enable only the policies that must be network-restricted, and supply at least one
allowed address or canonical CIDR for each enabled policy. For example, to restrict all proxied application and
Web traffic:

```json
{
  "Gateway": {
    "NetworkSecurity": {
      "IpSafelists": {
        "gateway-proxy-ip-safelist": {
          "Enabled": true,
          "AllowedNetworks": [
            "10.20.0.0/16",
            "2001:db8:1234::/48"
          ]
        }
      }
    }
  }
}
```

Merge that policy into the existing `IpSafelists` object; do not remove the other required policy entries.
Exact IP addresses are accepted and normalized to `/32` or `/128`. Do not use an empty enabled list,
non-canonical CIDR, IPv4-mapped IPv6 network, or universal `/0` network; startup validation rejects them.

**Expected result:** each enabled policy contains only the smallest networks required for its callers.

### 4. Configure forwarded-header trust

Leave forwarded headers disabled when callers connect directly to Kestrel. When a known reverse proxy is in
front of the Gateway, enable the feature with a positive finite hop count and at least one explicit trusted proxy
or network:

```json
{
  "Gateway": {
    "NetworkSecurity": {
      "ForwardedHeaders": {
        "Enabled": true,
        "ForwardLimit": 1,
        "KnownProxies": [ "10.20.1.10" ],
        "KnownNetworks": [ "2001:db8:10::/64" ]
      }
    }
  }
}
```

`KnownProxies` contains exact proxy addresses. `KnownNetworks` contains canonical proxy-network CIDRs, not
client networks. Never set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; the Gateway rejects that trust-all switch
because safelist and rate-limit decisions must not use an address supplied through an untrusted proxy.

**Expected result:** the configuration describes the actual proxy chain and cannot be widened by a caller.

### 5. Deploy and restart the Gateway

Apply the reviewed configuration through the environment's normal configuration provider, then restart or
redeploy every Gateway replica. These policies are captured and validated once during startup; changing the
backing configuration without a restart does not update an existing process.

For local development, restart the Gateway resource from the Aspire dashboard. If the resource does not start,
read its startup log before changing the policy again; validation reports the exact invalid setting.

### 6. Verify Completion

First verify basic readiness using the deployment-specific Gateway URL:

```pwsh
$gatewayUrl = Read-Host "Gateway base URL, without a trailing slash"
$response = Invoke-WebRequest -Uri "$gatewayUrl/healthz" -SkipHttpErrorCheck
$response | Select-Object StatusCode, StatusDescription
if ($response.StatusCode -ne 200) { throw "Gateway readiness returned HTTP $($response.StatusCode)." }
```

Then collect evidence for each changed policy from the correct network locations:

- An allowed source reaches the protected surface and receives its normal application response.
- A disallowed source receives `403 Forbidden`, and the response does not disclose configured networks.
- In a non-production load check, traffic beyond the selected token budget receives `429 Too Many Requests`
  with Problem Details and `Retry-After` when it can be calculated.
- When forwarded headers are enabled, the expected original client is honored through the trusted proxy, while
  a direct request with a spoofed `X-Forwarded-For` value is not trusted.
- `/metrics` follows the operations safelist; health endpoints remain available for their intended probes.
- All Gateway replicas report healthy after restart.

**Expected result:** startup succeeds and every positive and negative check matches the approved trust boundary.

## Rollback Procedure

If the deployment fails validation or blocks intended traffic:

1. Restore the previously deployed configuration from the saved copy or version-control reference.
2. Restart or redeploy every Gateway replica so the restored startup-bound policies take effect.
3. Repeat the readiness and previously passing access checks.
4. Record the failed configuration and validation output; do not leave a broad `/0` network or trust-all
   forwarded-header setting as a temporary bypass.

## Troubleshooting

| Problem | Possible Cause | Resolution |
|---------|---------------|------------|
| Gateway fails during startup | Empty enabled safelist, invalid/non-canonical CIDR, `/0`, invalid rate value, or forwarded headers without a trusted proxy | Use the named setting in the startup validation error, correct it, and restart. |
| All proxied callers receive `403` | The trusted client address resolves to the proxy because forwarded-header trust is missing or incorrect | Verify the immediate proxy address, hop count, and known-proxy/network configuration; do not safelist the proxy merely to hide the error. |
| A supplied `X-Forwarded-For` value is ignored | The direct sender is not in the configured trusted proxy boundary | Confirm the actual proxy chain and add only its authoritative address or network. |
| Rate limits appear multiplied | Limits are maintained independently by each Gateway replica | Enforce a global quota at ingress, WAF, or API management when the requirement is cross-replica. |
| A custom environment variable has no effect | The key does not follow the configured `BURCINCO_` prefix and double-underscore hierarchy | Compare it with the JSON hierarchy and the environment configuration conventions for the deployment. |

## Audit Log

Record the change request or pull request, approver, old and new values, authoritative source for every trusted
network, deployment identifiers, restart time, and positive/negative verification evidence. Keep secrets and
sensitive network inventories in the approved operational system rather than this repository.

## References

- [System architecture - Security](../architectures/system.md#security)
- Repository local-development onboarding: root `README.md`
- Gateway baseline configuration: `src/BurcinCo.BurcinApp.Gateway/appsettings.json`

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | (document-date-compact) | (authors) | Initial version |
