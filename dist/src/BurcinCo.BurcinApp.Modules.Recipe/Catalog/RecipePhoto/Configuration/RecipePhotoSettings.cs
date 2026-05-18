using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Configuration;

/// <summary>
/// Configuration for signed-URL issuance. Bound from <see cref="ConfigurationSectionName"/>.
/// In real life the secret would come from a key-vault binding, not appsettings — see the
/// SecretKey description for how to swap it.
/// </summary>
public sealed class RecipePhotoSettings
{
	public const string ConfigurationSectionName = "Modules:Recipe:Catalog:RecipePhoto";

	/// <summary>
	/// HMAC-SHA256 signing key for the opaque token. Tokens issued with one secret won't validate
	/// against another, so rotating the secret invalidates outstanding URLs. For production, source
	/// this from Azure Key Vault / AWS Secrets Manager / etc. via configuration provider chaining
	/// — never check the real secret into source.
	/// </summary>
	[Required]
	[MinLength(16)]
	public string SecretKey { get; set; } = "dev-only-not-for-production-32-bytes";

	/// <summary>How long an issued signed URL stays valid. Short windows reduce URL leakage blast radius.</summary>
	[Range(typeof(int), "5", "3600")]
	public int ExpirySeconds { get; set; } = 300;
}
