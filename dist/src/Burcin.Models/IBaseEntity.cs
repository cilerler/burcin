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

    public interface IAuditable
    {
        [Required]
        [Column(TypeName = "datetime2(3)")]
        //[DataType(DataType.DateTime)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        DateTime CreatedAt { get; set; }

        [Required]
        [Column(TypeName = "datetime2(3)")]
        //[DataType(DataType.DateTime)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        DateTime ModifiedAt { get; set; }
    }

    public interface ISoftDelete
    {
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        bool SoftDelete { get; set; }
    }
}
