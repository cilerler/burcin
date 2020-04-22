using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Burcin.Models;

namespace Brainiac.Models.Burcin
{
    public partial class MyModel : IBaseEntity, IAuditable, ISoftDelete
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public Guid RowGuid { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public byte[] UpdateVersion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        [Required]
        [Column(TypeName = "datetime2(3)")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        [Required]
        [Column(TypeName = "datetime2(3)")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime ModifiedAt { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public bool SoftDelete { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
