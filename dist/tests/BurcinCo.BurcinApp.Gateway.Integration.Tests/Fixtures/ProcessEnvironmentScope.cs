using System;
using System.Collections.Generic;

namespace BurcinCo.BurcinApp.Gateway.Integration.Tests.Fixtures;

internal sealed class ProcessEnvironmentScope : IDisposable
{
	private readonly IReadOnlyDictionary<string, string?> _originalValues;
	private bool _disposed;

	private ProcessEnvironmentScope(IReadOnlyDictionary<string, string?> values)
	{
		ArgumentNullException.ThrowIfNull(values);

		var originalValues = new Dictionary<string, string?>(StringComparer.Ordinal);
		foreach (var key in values.Keys)
		{
			originalValues.Add(key, Environment.GetEnvironmentVariable(key));
		}
		_originalValues = originalValues;

		try
		{
			foreach (var (key, value) in values)
			{
				Environment.SetEnvironmentVariable(key, value);
			}
		}
		catch
		{
			Restore();
			throw;
		}
	}

	public static ProcessEnvironmentScope Apply(IReadOnlyDictionary<string, string?> values) => new(values);

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		Restore();
		_disposed = true;
	}

	private void Restore()
	{
		foreach (var (key, value) in _originalValues)
		{
			Environment.SetEnvironmentVariable(key, value);
		}
	}
}
