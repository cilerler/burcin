using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
    [Table("MyModel3")]
    public partial class MyModel3 : BaseModel
    {
        public MyModel3()
        {
            MyModel = new HashSet<MyModel1>();
        }

        [Required]
        public short Code { get; set; }

        [Required]
        [StringLength(50)]
        public string Label { get; set; }
        [StringLength(2048)]
        public string Description { get; set; }
        public byte DisplayOrder { get; set; }

        [InverseProperty("ModelTypeNavigation")]
        public virtual ICollection<MyModel1> MyModel { get; set; }
    }
}
