using System;

namespace BurcinCo.BurcinApp.Models.Abstractions
{
	public interface ITimestamp
    {
        DateTime CreatedAt { get; set; }
        DateTime ModifiedAt { get; set; }
    }
}
