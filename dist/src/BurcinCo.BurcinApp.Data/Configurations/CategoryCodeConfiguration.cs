using System;
using System.Linq;
using BurcinCo.BurcinApp.Models.BurcinDatabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BurcinCo.BurcinApp.Data.Configurations;

internal sealed class CategoryCodeConfiguration : IEntityTypeConfiguration<CategoryCode>
{
	public void Configure(EntityTypeBuilder<CategoryCode> builder)
	{
		// RowGuid is derived deterministically from the enum value: EF Core 9+ throws
		// PendingModelChangesWarning if HasData uses non-deterministic values like Guid.NewGuid().
		// The pattern `11111111-1111-1111-1111-{value:D12}` keeps migrations stable across regens.
		var rows = Enum.GetValues<KnownCategoryCode>()
			.Where(v => v != KnownCategoryCode.Uncategorized)
			.Select(v => new CategoryCode
			{
				Id = (long)v,
				Code = (short)v,
				Name = v.ToString(),
				RowGuid = new Guid($"11111111-1111-1111-1111-{(int)v:D12}"),
			})
			.ToArray();

		builder.HasData(rows);
	}
}
