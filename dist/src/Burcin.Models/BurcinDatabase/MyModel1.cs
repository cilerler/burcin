using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
    [Table("MyModel1", Schema = "dbo")]
    public partial class MyModel1
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [ConcurrencyCheck]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column(TypeName="UNIQUEIDENTIFIER ROWGUIDCOL")]
        public Guid RowGuid { get; set; }

        [Timestamp]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public byte[] UpdateVersion { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime ModifiedAt { get; set; }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public bool SoftDelete { get; set; }

        [ForeignKey(nameof(ModelType))]
        [InverseProperty(nameof(MyModel3.MyModel))]
        public virtual MyModel3 ModelTypeNavigation { get; set; }

		public ICollection<MyModel1MyModel2> MyModel1MyModel2s { get; set; }
    }
}
