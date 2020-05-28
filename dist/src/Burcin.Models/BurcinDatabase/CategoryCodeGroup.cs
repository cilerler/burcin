using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
	[Table(nameof(CategoryCodeGroup), Schema = Constants.DefaultSchema)]
	public class CategoryCodeGroup: BaseModel
	{
		public long CategoryCodeId { get; set; }
		[ForeignKey(nameof(CategoryCodeId))]
		[InverseProperty(nameof(CategoryCode.CategoryCodeGroups))]
		public virtual CategoryCode Code { get; set; }

		public long CategoryGroupId { get; set; }
		[ForeignKey(nameof(CategoryGroupId))]
		[InverseProperty(nameof(CategoryGroup.CategoryCodeGroups))]
		public virtual CategoryGroup Group { get; set; }
	}
}
