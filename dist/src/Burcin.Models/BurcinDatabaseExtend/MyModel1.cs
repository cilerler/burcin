using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
    public partial class MyModel1 : ICloneable, IEquatable<MyModel1>
	{

		public MyModel1()
		{
		}

		public long ModelType { get; set; }

		[Column("Note")]
		[Required, StringLength(20), MaxLength(20)]
		public string Description { get; set; }

		public string AdditionalDescription { get; set; }

		#region ICloneable Members

		public object Clone()
		{
			return MemberwiseClone();
		}

		#endregion

		#region IEquatable<MyModel1> Members

		public bool Equals(MyModel1 other)
		{
			if (other == null)
			{
				return false;
			}

			if (ReferenceEquals(this, other))
			{
				return true;
			}

			bool objectPropertiesAreEqual = Id.Equals(other.Id) && Description.Equals(other.Description) && AdditionalDescription.Equals(other.AdditionalDescription);
			return objectPropertiesAreEqual;
		}

		public override bool Equals(object obj)
		{
			{
				if (obj == null)
				{
					return false;
				}

				bool objectCanBeCast = obj is MyModel1;
				if (!objectCanBeCast)
				{
					return false;
				}

				var other = obj as MyModel1;
				return Equals(other);
			}
		}

		public static bool operator == (MyModel1 source, MyModel1 target)
		{
			return source?.Equals(target) ?? ReferenceEquals(target, null);
		}

		public static bool operator != (MyModel1 source, MyModel1 target)
		{
			return !source?.Equals(target) ?? !ReferenceEquals(target, null);
		}

		#endregion

		public override string ToString()
		{
			return string.Format($"{Id},{CreatedAt:s},{ModifiedAt:s},{SoftDelete},{Description},{AdditionalDescription}");
		}

		public override int GetHashCode()
		{
			//x return base.GetHashCode();
			//x return new { Id, CreatedAt, ModifiedAt, SoftDelete, Description, AdditionalDescription }.GetHashCode();
			unchecked
			{
				int hash = 17; //x (int)2166136261; //x (int)397;
				const int primeNumber = 23; //x 16777619;

				//TODO do suitable nullity checks
				hash = hash * primeNumber ^ Id.GetHashCode();
				hash = hash * primeNumber ^ CreatedAt.GetHashCode();
				hash = hash * primeNumber ^ ModifiedAt.GetHashCode();
				hash = hash * primeNumber ^ SoftDelete.GetHashCode();
				hash = hash * primeNumber ^ (Description == null
											? 0
											: Description.GetHashCode());
				hash = hash * primeNumber ^ (AdditionalDescription == null
											? 0
											: AdditionalDescription.GetHashCode());
				return hash;
			}
		}
	}
}
