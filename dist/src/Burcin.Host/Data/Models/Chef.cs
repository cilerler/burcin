using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Host.Data.Models
{
	public class Chef
	{
		public Chef()
		{
		}
		public long Id { get; set; }
		public Guid RowGuid { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime ModifiedAt { get; set; }
		public string Name { get; set; }
		public string Url { get; set; }
        public bool SoftDelete { get; set; }
	}
}
