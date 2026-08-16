using System;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;

/// <summary>A delivered quote message violates a permanent identity, routing, or payload invariant.</summary>
internal sealed class InvalidIngredientQuoteMessageException : Exception
{
	public InvalidIngredientQuoteMessageException(string message)
		: base(message)
	{
	}
}
