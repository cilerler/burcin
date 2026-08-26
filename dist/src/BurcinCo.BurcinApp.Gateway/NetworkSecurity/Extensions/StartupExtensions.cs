using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using BurcinCo.BurcinApp.Gateway.NetworkSecurity.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using SystemIpNetwork = System.Net.IPNetwork;

namespace BurcinCo.BurcinApp.Gateway.NetworkSecurity.Extensions;

internal static class StartupExtensions
{
	private static readonly string[] _requiredPolicies =
	[
		Constants.IpSafelistPolicies.Operations,
		Constants.IpSafelistPolicies.Proxy,
		Constants.IpSafelistPolicies.Webhook,
	];

	public static IServiceCollection AddGatewayNetworkSecurity(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var section = configuration.GetRequiredSection(NetworkSecuritySettings.ConfigurationSectionName);
		var settings = section.Get<NetworkSecuritySettings>()
			?? throw ValidationException("Gateway network-security configuration is required.");
		var validated = Validate(settings);

		// Trusted proxy boundaries and authorization policies are graph-changing configuration, so
		// capture and validate them once before the service provider is built.
		services.Configure<ForwardedHeadersOptions>(options =>
		{
			options.ForwardedHeaders = validated.ForwardedHeaders.Enabled
				? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
				: ForwardedHeaders.None;
			options.ForwardLimit = validated.ForwardedHeaders.ForwardLimit;
			options.KnownProxies.Clear();
			options.KnownIPNetworks.Clear();

			foreach (var proxy in validated.KnownProxies)
			{
				options.KnownProxies.Add(proxy);
			}

			foreach (var network in validated.KnownNetworks)
			{
				options.KnownIPNetworks.Add(network);
			}
		});

		services.AddAuthorization(options =>
		{
			foreach (var policy in validated.IpSafelists)
			{
				options.AddPolicy(
					policy.Key,
					builder => builder.AddRequirements(new IpSafelistRequirement(
						policy.Value.Enabled,
						policy.Value.AllowedNetworks)));
			}
		});
		services.AddSingleton<IAuthorizationHandler, IpSafelistAuthorizationHandler>();
		services.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler,
			IpSafelistAuthorizationMiddlewareResultHandler>());

		return services;
	}

	private static ValidatedNetworkSecuritySettings Validate(NetworkSecuritySettings settings)
	{
		var failures = new List<string>();
		if (string.Equals(
			Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED"),
			bool.TrueString,
			StringComparison.OrdinalIgnoreCase))
		{
			failures.Add(
				"ASPNETCORE_FORWARDEDHEADERS_ENABLED cannot be used by the Gateway because it trusts arbitrary proxies. Configure Gateway:NetworkSecurity:ForwardedHeaders instead.");
		}

		if (settings.ForwardedHeaders.ForwardLimit <= 0)
		{
			failures.Add("ForwardedHeaders:ForwardLimit must be greater than zero.");
		}

		var knownProxies = ParseAddresses(
			settings.ForwardedHeaders.KnownProxies,
			"ForwardedHeaders:KnownProxies",
			failures);
		var knownNetworks = ParseNetworks(
			settings.ForwardedHeaders.KnownNetworks,
			"ForwardedHeaders:KnownNetworks",
			allowSingleAddress: false,
			failures);
		if (settings.ForwardedHeaders.Enabled && knownProxies.Length == 0 && knownNetworks.Length == 0)
		{
			failures.Add(
				"ForwardedHeaders requires at least one trusted KnownProxy or KnownNetwork when enabled.");
		}

		var safelists = new Dictionary<string, ValidatedIpSafelist>(StringComparer.Ordinal);
		foreach (var configuredPolicy in settings.IpSafelists)
		{
			if (string.IsNullOrWhiteSpace(configuredPolicy.Key))
			{
				failures.Add("IP safelist policy names cannot be empty.");
				continue;
			}

			var networks = ParseNetworks(
				configuredPolicy.Value.AllowedNetworks,
				$"IpSafelists:{configuredPolicy.Key}:AllowedNetworks",
				allowSingleAddress: true,
				failures);
			if (configuredPolicy.Value.Enabled && networks.Length == 0)
			{
				failures.Add(
					$"IP safelist policy '{configuredPolicy.Key}' requires at least one allowed network when enabled.");
			}

			safelists[configuredPolicy.Key] = new ValidatedIpSafelist(
				configuredPolicy.Value.Enabled,
				networks);
		}

		foreach (var requiredPolicy in _requiredPolicies.Where(policy => !safelists.ContainsKey(policy)))
		{
			failures.Add($"Required IP safelist policy '{requiredPolicy}' is not configured.");
		}

		if (failures.Count > 0)
		{
			throw ValidationException(failures.ToArray());
		}

		return new ValidatedNetworkSecuritySettings(
			settings.ForwardedHeaders,
			knownProxies,
			knownNetworks,
			safelists);
	}

	private static IPAddress[] ParseAddresses(
		IEnumerable<string> configuredAddresses,
		string settingName,
		List<string> failures)
	{
		var addresses = new List<IPAddress>();
		foreach (var configuredAddress in configuredAddresses)
		{
			if (!IPAddress.TryParse(configuredAddress, out var address))
			{
				failures.Add($"{settingName} contains invalid IP address '{configuredAddress}'.");
				continue;
			}

			if (address.IsIPv4MappedToIPv6)
			{
				address = address.MapToIPv4();
			}

			addresses.Add(address);
		}

		return addresses.ToArray();
	}

	private static SystemIpNetwork[] ParseNetworks(
		IEnumerable<string> configuredNetworks,
		string settingName,
		bool allowSingleAddress,
		List<string> failures)
	{
		var networks = new List<SystemIpNetwork>();
		foreach (var configuredNetwork in configuredNetworks)
		{
			if (string.IsNullOrWhiteSpace(configuredNetwork))
			{
				failures.Add($"{settingName} contains an empty network.");
				continue;
			}

			var candidate = configuredNetwork.Trim();
			if (allowSingleAddress && IPAddress.TryParse(candidate, out var singleAddress))
			{
				if (singleAddress.IsIPv4MappedToIPv6)
				{
					singleAddress = singleAddress.MapToIPv4();
				}

				candidate = $"{singleAddress}/{(singleAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128)}";
			}

			if (!SystemIpNetwork.TryParse(candidate, out var network))
			{
				failures.Add($"{settingName} contains invalid CIDR network '{configuredNetwork}'.");
				continue;
			}

			if (network.BaseAddress.IsIPv4MappedToIPv6)
			{
				failures.Add(
					$"{settingName} contains IPv4-mapped IPv6 network '{configuredNetwork}'; configure its IPv4 CIDR form instead.");
				continue;
			}

			var separatorIndex = candidate.LastIndexOf('/');
			if (separatorIndex <= 0 ||
				!IPAddress.TryParse(candidate[..separatorIndex], out var configuredBaseAddress) ||
				!configuredBaseAddress.Equals(network.BaseAddress))
			{
				failures.Add(
					$"{settingName} contains non-canonical CIDR network '{configuredNetwork}'. Use '{network}'.");
				continue;
			}

			if (network.PrefixLength == 0)
			{
				failures.Add(
					$"{settingName} cannot contain universal network '{configuredNetwork}'. Disable the policy explicitly instead.");
				continue;
			}

			networks.Add(network);
		}

		return networks.ToArray();
	}

	private static OptionsValidationException ValidationException(params string[] failures) =>
		new(Options.DefaultName, typeof(NetworkSecuritySettings), failures);

	private sealed record ValidatedNetworkSecuritySettings(
		ForwardedHeadersSettings ForwardedHeaders,
		IPAddress[] KnownProxies,
		SystemIpNetwork[] KnownNetworks,
		IReadOnlyDictionary<string, ValidatedIpSafelist> IpSafelists);

	private sealed record ValidatedIpSafelist(bool Enabled, SystemIpNetwork[] AllowedNetworks);
}
