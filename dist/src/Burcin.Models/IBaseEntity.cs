using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models
{
	public interface IBaseEntity
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        long Id { get; set; }

        ////UNIQUEIDENTIFIER ROWGUIDCOL
        ////!++ Do not uncomment below https://github.com/OData/RESTier/issues/491
        //[ConcurrencyCheck]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        Guid RowGuid { set; get; }

        ////ROWVERSION
        ////!++ Do not uncomment below https://github.com/OData/RESTier/issues/491
        //[Timestamp]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        byte[] UpdateVersion { get; set; }
    }
}
