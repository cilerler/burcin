using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
    public partial class MyModel2 : BaseModel
	{
		public ICollection<MyModel1MyModel2> MyModel1MyModel2s { get; set; }

		public MyModel2()
		{
		}
	}
}
