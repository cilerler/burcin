using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
	public partial class Chef : ICloneable, IEquatable<Chef>, IBaseEntity, IAuditable, ISoftDelete
	{
		#region ICloneable Members

		public object Clone()
		{
			return MemberwiseClone();
		}

		#endregion

		#region IEquatable<MyModel1> Members

		public bool Equals(Chef other)
		{
			if (other == null)
			{
				return false;
			}

			if (ReferenceEquals(this, other))
			{
				return true;
			}

			bool objectPropertiesAreEqual = Id.Equals(other.Id) && Name.Equals(other.Name) && Url.Equals(other.Url);
			return objectPropertiesAreEqual;
		}

		public override bool Equals(object obj)
		{
			{
				if (obj == null)
				{
					return false;
				}

				bool objectCanBeCast = obj is Chef;
				if (!objectCanBeCast)
				{
					return false;
				}

				var other = obj as Chef;
				return Equals(other);
			}
		}

		public static bool operator ==(Chef source, Chef target) => source?.Equals(target) ?? ReferenceEquals(target, null);

		public static bool operator !=(Chef source, Chef target) => !source?.Equals(target) ?? !ReferenceEquals(target, null);

		#endregion

		public override string ToString()
		{
			return string.Format($"{Id},{CreatedAt:s},{ModifiedAt:s},{SoftDelete},{Name},{Url}");
		}

		public override int GetHashCode()
		{
			//x return base.GetHashCode();
			//x return new { Id, CreatedAt, ModifiedAt, SoftDelete, Name, Url }.GetHashCode();
			unchecked
			{
				int hash = 17; //x (int)2166136261; //x (int)397;
				const int primeNumber = 23; //x 16777619;

				//TODO do suitable nullity checks
				hash = hash * primeNumber ^ Id.GetHashCode();
				hash = hash * primeNumber ^ CreatedAt.GetHashCode();
				hash = hash * primeNumber ^ ModifiedAt.GetHashCode();
				hash = hash * primeNumber ^ SoftDelete.GetHashCode();
				hash = hash * primeNumber ^ (Name == null
											? 0
											: Name.GetHashCode());
				hash = hash * primeNumber ^ (Url == null
											? 0
											: Url.GetHashCode());
				return hash;
			}
		}
	}
}
