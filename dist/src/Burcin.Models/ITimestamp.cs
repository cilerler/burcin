using System;

namespace Burcin.Models
{
	public interface ITimestamp
    {
        DateTime CreatedAt { get; set; }
        DateTime ModifiedAt { get; set; }
    }
}
