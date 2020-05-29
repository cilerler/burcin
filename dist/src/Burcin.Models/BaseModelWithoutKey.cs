using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models
{
	public abstract class BaseModelWithoutKey : IAuditable, ISoftDelete
    {
        ////UNIQUEIDENTIFIER ROWGUIDCOL
        ////!++ Do not uncomment below https://github.com/OData/RESTier/issues/491
        [ConcurrencyCheck]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
		[Column(TypeName="UNIQUEIDENTIFIER ROWGUIDCOL")]
        public Guid RowGuid { get; set; }

        ////ROWVERSION
        ////!++ Do not uncomment below https://github.com/OData/RESTier/issues/491
        [Timestamp]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public byte[] UpdateVersion { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        //[Column(TypeName = "datetime2(7)")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        //[Column(TypeName = "datetime2(7)")]
        public DateTime ModifiedAt { get; set; }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public bool SoftDelete { get; set; }
    }
}
