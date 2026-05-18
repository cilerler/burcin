namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Contracts;

/// <summary>
/// Issues + validates opaque signed-URL tokens for recipe photos. Stateless: tokens self-describe
/// the recipe id and expiry, signed with HMAC so the server doesn't need to remember which tokens
/// it issued. Compatible with horizontal scale-out and stateless services.
///
/// Public because the photo minimal-API endpoints (which live in this same module) take it via DI.
/// </summary>
public interface IRecipePhotoService
{
	/// <summary>Issue a token that the client can plug into the download URL.</summary>
	(string Token, System.DateTimeOffset ExpiresAt) IssueToken(long recipeId);

	/// <summary>
	/// Validate a token. Returns the recipe id if the token is well-formed, the HMAC verifies, and
	/// the expiry is in the future. Returns null otherwise (the caller should respond 404/410/etc.
	/// based on the rejection reason — kept opaque here because we don't want to leak which check
	/// failed to a downloader probing for valid tokens).
	/// </summary>
	long? ValidateToken(string token);
}
