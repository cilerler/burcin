using System;

namespace Burcin.Models
{
    public interface IBaseEntity
    {
        long Id { get; set; }

        Guid RowGuid { set; get; }

        byte[] UpdateVersion { get; set; }
    }
}
