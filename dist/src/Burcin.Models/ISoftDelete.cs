using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models
{
    public interface ISoftDelete
    {
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        bool SoftDelete { get; set; }
    }
}
