using System;
using System.Collections.Generic;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;

/// <summary>One or more caller-supplied quote fields violate the service boundary.</summary>
internal sealed class IngredientSupplyValidationException : Exception
{
	public IngredientSupplyValidationException(IReadOnlyList<string> errors)
		: base("The ingredient quote request is invalid.")
	{
		Errors = errors;
	}

	public IReadOnlyList<string> Errors { get; }
}
