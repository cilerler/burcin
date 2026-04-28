using System;

namespace BurcinCo.BurcinApp.Models
{
    public interface IBaseEntity
    {
        long Id { get; set; }

        Guid RowGuid { set; get; }

        byte[] RowVersion { get; set; }
    }
}
