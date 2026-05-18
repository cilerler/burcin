namespace BurcinCo.BurcinApp.Models.Abstractions
{
	/// <summary>
	/// Marker for entities that carry a <c>ModifiedBy</c> column. The column is fully DB-managed:
	/// <c>SUSER_SNAME()</c> default fires on INSERT, the table's <c>StampModifiedAt</c> trigger
	/// (or equivalent) refreshes on UPDATE. The app never stamps it.
	/// </summary>
	public interface IAudit
	{
		string ModifiedBy { get; set; }
	}
}
