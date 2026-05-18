using System;
using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Models;

/// <summary>
/// Demo non-database entity. Lives entirely in-memory via <c>InMemoryTagStore</c> — there is no
/// EF mapping, no DbSet, no migration. Use this pattern when you want OData-shaped CRUD over
/// a non-relational source: an external HTTP API, a Redis hash, a computed projection, or a
/// configuration-backed list.
///
/// The shape is intentionally minimal so the demo emphasises the wiring (controller + service
/// + EDM contribution + in-memory store) rather than the model. <c>Id</c> is server-generated
/// by the in-memory store on POST.
/// </summary>
public sealed class Tag
{
	[Key]
	public long Id { get; set; }

	[Required]
	[StringLength(50)]
	public string Name { get; set; } = string.Empty;

	[StringLength(7)]
	public string? Color { get; set; }

	public DateTimeOffset CreatedAt { get; set; }
}
