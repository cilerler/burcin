using System.ComponentModel.DataAnnotations.Schema;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[Table(nameof(CategoryCodeGroupMapping), Schema = Constants.DefaultSchema)]
	public class CategoryCodeGroupMapping: BaseModel
	{
		public long CategoryCodeId { get; set; }
		[ForeignKey(nameof(CategoryCodeId))]
		[InverseProperty(nameof(CategoryCode.CategoryCodeGroupMappings))]
		public virtual CategoryCode Code { get; set; }

		public long CategoryGroupId { get; set; }
		[ForeignKey(nameof(CategoryGroupId))]
		[InverseProperty(nameof(CategoryGroup.CategoryCodeGroupMappings))]
		public virtual CategoryGroup Group { get; set; }
	}
}
