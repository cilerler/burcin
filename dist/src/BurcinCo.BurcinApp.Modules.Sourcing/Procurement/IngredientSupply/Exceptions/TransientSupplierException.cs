using System;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;

/// <summary>A supplier call failed in a way that is safe to retry with the same idempotency key.</summary>
internal sealed class TransientSupplierException : Exception
{
	public TransientSupplierException(string message)
		: base(message)
	{
	}

	public TransientSupplierException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
