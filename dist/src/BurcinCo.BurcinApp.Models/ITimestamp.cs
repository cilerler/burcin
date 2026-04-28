using System;

namespace BurcinCo.BurcinApp.Models
{
	public interface ITimestamp
    {
        DateTime CreatedAt { get; set; }
        DateTime ModifiedAt { get; set; }
    }
}
