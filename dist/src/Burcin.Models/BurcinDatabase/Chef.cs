using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
    [Table(nameof(Chef))]
    public partial class Chef
    {
	    public Chef()
	    {
		    Recipes = new HashSet<Recipe>();
	    }

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

        [Required, StringLength(50), MaxLength(50)]
		public string Name { get; set; }

		[Column("Youtube")]
		public string Url { get; set; }

		[InverseProperty(nameof(Recipe.Chef))]
        public virtual ICollection<Recipe> Recipes { get; set; }
	}
}
