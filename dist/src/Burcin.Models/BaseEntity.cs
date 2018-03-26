using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models
{
    public abstract class BaseEntity
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        //x [Required]
        //x [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        //!++ Do not uncomment below https://github.com/OData/RESTier/issues/491
        //x [Timestamp] //ROWVERSION
        //x public byte[] UpdateVersion { get; set; }

        //UNIQUEIDENTIFIER ROWGUIDCOL
        [Required]
        //!++ Do not uncomment below https://github.com/OData/RESTier/issues/491
        //x [ConcurrencyCheck]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public Guid RowGuid { set; get; }

        [Required]
        [DataType(DataType.DateTime)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime Inserted { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime LastUpdated { get; set; }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public bool Status { get; set; }
    }
}
