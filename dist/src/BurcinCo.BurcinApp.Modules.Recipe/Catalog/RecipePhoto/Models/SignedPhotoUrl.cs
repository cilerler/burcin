using System;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Models;

/// <summary>
/// Response payload for the signed-URL endpoint. <see cref="Url"/> is opaque — the client should
/// just GET it without parsing. <see cref="ExpiresAt"/> tells the client when to ask for a new
/// one (it'll be rejected with 410 Gone after this instant).
/// </summary>
public sealed record SignedPhotoUrl(string Url, DateTimeOffset ExpiresAt);
