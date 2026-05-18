using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[Table("NutritionFact", Schema = "Nutrition")]
	[Index(nameof(RecipeId), Name = "IX_NutritionFact_RecipeId", IsUnique = true)]
	public partial class NutritionFact
	{
		[Key]
		public long Id { get; set; }

		public Guid RowGuid { get; set; }

		public byte[] RowVersion { get; set; } = null!;

		public DateTime CreatedAt { get; set; }

		public DateTime ModifiedAt { get; set; }

		[StringLength(261)]
		public string ModifiedBy { get; set; } = null!;

		public long RecipeId { get; set; }

		public float CaloriesPerYield { get; set; }

		public float ProteinGrams { get; set; }

		public float CarbsGrams { get; set; }

		public float FatGrams { get; set; }

		public float? FiberGrams { get; set; }

		public float? SodiumMilligrams { get; set; }
	}
}
