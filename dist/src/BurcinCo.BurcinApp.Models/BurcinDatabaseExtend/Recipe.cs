using BurcinCo.BurcinApp.Models.Abstractions;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	public partial class Recipe : ITimestamp, IAudit, IHistory
	{
	}
}
