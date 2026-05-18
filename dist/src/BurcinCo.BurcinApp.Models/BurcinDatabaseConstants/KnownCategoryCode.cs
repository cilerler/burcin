namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	/// <summary>
	/// Type-safe mirror of the <c>Recipe.CategoryCode</c> lookup table's <c>Code</c> column.
	/// Source of truth — <c>CategoryCodeConfiguration</c> derives the seed rows from this enum,
	/// so adding a member automatically extends the seed. Backing column is <c>short</c>;
	/// widening the enum requires a matching column migration.
	/// </summary>
	public enum KnownCategoryCode : short
	{
		Uncategorized = 0,
		Italian = 1,
		French = 2,
		Turkish = 3,
	}
}
