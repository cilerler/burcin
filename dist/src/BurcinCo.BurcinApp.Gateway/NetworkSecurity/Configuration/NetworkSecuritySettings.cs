using System.Collections.Generic;

namespace BurcinCo.BurcinApp.Gateway.NetworkSecurity.Configuration;

internal sealed class NetworkSecuritySettings
{
	public const string ConfigurationSectionName = "Gateway:NetworkSecurity";

	public ForwardedHeadersSettings ForwardedHeaders { get; set; } = new();

	public Dictionary<string, IpSafelistSettings> IpSafelists { get; set; } = [];
}

internal sealed class ForwardedHeadersSettings
{
	public bool Enabled { get; set; }

	public int ForwardLimit { get; set; } = 1;

	public List<string> KnownProxies { get; set; } = [];

	public List<string> KnownNetworks { get; set; } = [];
}

internal sealed class IpSafelistSettings
{
	public bool Enabled { get; set; }

	public List<string> AllowedNetworks { get; set; } = [];
}
