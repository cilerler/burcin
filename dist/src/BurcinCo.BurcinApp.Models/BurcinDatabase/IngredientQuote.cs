using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	/// <summary>
	/// A quote-request lifecycle row owned by Modules.Sourcing.
	/// Created when the producer side asks an external supplier for an ingredient quote;
	/// transitions through Status as the request flows out (Pending → Sent), and is
	/// updated again when the supplier's response arrives via webhook → Gateway → broker
	/// → Modules.Sourcing's Inbox handler (Sent → ResponseReceived).
	/// </summary>
	[Table("IngredientQuote", Schema = "Sourcing")]
	[Index(nameof(Status), nameof(RequestedAt), Name = "IX_IngredientQuote_Status_RequestedAt")]
	[Index(nameof(SupplierKey), Name = "IX_IngredientQuote_SupplierKey")]
	public partial class IngredientQuote
	{
		[Key]
		public long Id { get; set; }

		public Guid RowGuid { get; set; }

		public byte[] RowVersion { get; set; } = null!;

		public DateTime CreatedAt { get; set; }

		public DateTime ModifiedAt { get; set; }

		[StringLength(261)]
		public string ModifiedBy { get; set; } = null!;

		public long? RecipeId { get; set; }

		[StringLength(100)]
		public string SupplierKey { get; set; } = null!;

		public string IngredientsJson { get; set; } = null!;

		[StringLength(20)]
		public string Status { get; set; } = IngredientQuoteStatus.Pending;

		public DateTime RequestedAt { get; set; }

		public DateTime? SentAt { get; set; }

		public DateTime? ResponseReceivedAt { get; set; }

		public string? ResponseJson { get; set; }

		[StringLength(500)]
		public string? FailureReason { get; set; }
	}

	/// <summary>
	/// Hand-maintained status values for <see cref="IngredientQuote.Status"/>.
	/// Owned by this entity's lifecycle, not a shared DB lookup table.
	/// </summary>
	public static class IngredientQuoteStatus
	{
		public const string Pending = nameof(Pending);
		public const string Sent = nameof(Sent);
		public const string ResponseReceived = nameof(ResponseReceived);
		public const string Failed = nameof(Failed);
	}
}
