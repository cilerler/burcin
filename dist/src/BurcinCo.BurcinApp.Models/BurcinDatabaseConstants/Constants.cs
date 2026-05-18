namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	public class Constants
	{
		/// <summary>Cross-cutting infrastructure tables: Outbox, Inbox, EF migrations history.</summary>
		public const string DefaultSchema = "dbo";

		public const string RecipeSchema = "Recipe";
		public const string NutritionSchema = "Nutrition";
		public const string SourcingSchema = "Sourcing";
	}
}
