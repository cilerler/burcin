using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models
{
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
}
