using System;

namespace Burcin.Models
{
    public interface IAuditable
    {
        DateTime CreatedAt { get; set; }
        DateTime ModifiedAt { get; set; }
    }
}
