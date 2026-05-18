using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[Table("Chef", Schema = "Recipe")]
	[Index(nameof(SoftDelete), nameof(ModifiedAt), Name = "IX_Chef_SoftDelete_ModifiedAt")]
	public partial class Chef
	{
		[Key]
		public long Id { get; set; }

		public Guid RowGuid { get; set; }

		public byte[] RowVersion { get; set; } = null!;

		public DateTime CreatedAt { get; set; }

		public DateTime ModifiedAt { get; set; }

		[StringLength(261)]
		public string ModifiedBy { get; set; } = null!;

		public bool SoftDelete { get; set; }

		[StringLength(50)]
		public string Name { get; set; } = null!;

		public string Url { get; set; } = null!;

		[InverseProperty(nameof(Recipe.Chef))]
		public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
	}
}
