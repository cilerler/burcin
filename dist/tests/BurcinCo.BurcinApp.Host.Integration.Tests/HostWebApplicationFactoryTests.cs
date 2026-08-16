using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Host.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.Host.Integration.Tests;

/// <summary>
/// Process-local HTTP tests for Host-owned process contracts. Module and service behavior belongs to its owning
/// integration suite; cross-process and AppHost-orchestrated resource behavior belongs to Aspire end-to-end tests.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class HostWebApplicationFactoryTests
{
	private const string TestIssuer = "https://identity.test.invalid";
	private const string TestAudience = "host-waf-tests";
	private static readonly SymmetricSecurityKey TestSigningKey = new(RandomNumberGenerator.GetBytes(32));

	[TestMethod]
	public async Task GetMe_Authenticated_ReturnsExactBoundedIdentity()
	{
		using var environment = ConfigureEnvironment();
		await using var factory = new HostWebApplicationFactory();
		using var http = factory.CreateClient();
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
			JwtBearerDefaults.AuthenticationScheme,
			CreateAccessToken());

		using var response = await http.GetAsync(new Uri("/me", UriKind.Relative));

		Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		Assert.AreEqual(JsonValueKind.Object, body.RootElement.ValueKind);
		Assert.AreEqual(2, body.RootElement.GetPropertyCount(), "The identity response must expose no additional claims.");
		Assert.AreEqual("test-subject", body.RootElement.GetProperty("subject").GetString());
		Assert.AreEqual("Test User", body.RootElement.GetProperty("name").GetString());
	}

	private static EnvironmentVariableScope ConfigureEnvironment() =>
		new(
			("DOTNET_ENVIRONMENT", "Development"),
			("ASPNETCORE_ENVIRONMENT", "Development"),
			("EnvironmentVariablesPrefix", "BURCINCO_"),
			("FeatureManagement__Modules.Recipe", bool.FalseString),
			("BURCINCO_FeatureManagement__Modules.Recipe", bool.FalseString),
			("FeatureManagement__Modules.Nutrition", bool.FalseString),
			("BURCINCO_FeatureManagement__Modules.Nutrition", bool.FalseString),
			("FeatureManagement__Modules.Sourcing", bool.FalseString),
			("BURCINCO_FeatureManagement__Modules.Sourcing", bool.FalseString),
			("Authentication__Schemes__Bearer__Authority", TestIssuer),
			("BURCINCO_Authentication__Schemes__Bearer__Authority", TestIssuer),
			("Authentication__Schemes__Bearer__Audience", TestAudience),
			("BURCINCO_Authentication__Schemes__Bearer__Audience", TestAudience),
			("LOGGING__CONSOLE__FORMATTERNAME", null),
			// Keep the in-process Prometheus exporter registered because production maps its endpoint,
			// but prevent the WAF process from exporting to an external collector.
			("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty),
			("BURCINCO_OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty));

	private sealed class HostWebApplicationFactory : WebApplicationFactory<CapabilitySelection>
	{
		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			ArgumentNullException.ThrowIfNull(builder);
			// EventLog is a production host provider and can require machine-level permissions.
			// The WAF layer owns HTTP behavior, so it deliberately substitutes a silent logger.
			builder.ConfigureLogging(logging => logging.ClearProviders());
			builder.ConfigureTestServices(services =>
			{
				// Keep the production Bearer handler and authorization pipeline. Only replace its
				// external identity-provider metadata/signing-key source with an in-process key.
				services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
					{
						options.ConfigurationManager = null;
						options.TokenValidationParameters = new TokenValidationParameters
						{
							ValidateIssuerSigningKey = true,
							IssuerSigningKey = TestSigningKey,
							ValidateIssuer = true,
							ValidIssuer = TestIssuer,
							ValidateAudience = true,
							ValidAudience = TestAudience,
							ValidateLifetime = true,
							ClockSkew = TimeSpan.Zero,
						};
					});
			});
		}
	}

	private static string CreateAccessToken()
	{
		var now = DateTime.UtcNow;
		Claim[] claims =
		[
			new Claim(JwtRegisteredClaimNames.Sub, "test-subject"),
			new Claim(JwtRegisteredClaimNames.Name, "Test User"),
		];
		var token = new JwtSecurityToken(
			issuer: TestIssuer,
			audience: TestAudience,
			claims: claims,
			notBefore: now.AddMinutes(-1),
			expires: now.AddMinutes(5),
			signingCredentials: new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256));
		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	private sealed class EnvironmentVariableScope : IDisposable
	{
		private readonly Dictionary<string, string?> _originalValues;
		private bool _disposed;

		public EnvironmentVariableScope(params (string Name, string? Value)[] values)
		{
			_originalValues = new Dictionary<string, string?>(values.Length, StringComparer.Ordinal);
			foreach (var (name, value) in values)
			{
				_originalValues.Add(name, Environment.GetEnvironmentVariable(name));
				Environment.SetEnvironmentVariable(name, value);
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			foreach (var (name, value) in _originalValues)
			{
				Environment.SetEnvironmentVariable(name, value);
			}

			_disposed = true;
		}
	}
}
