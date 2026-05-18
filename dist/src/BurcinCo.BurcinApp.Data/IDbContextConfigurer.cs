using System;
using Microsoft.EntityFrameworkCore;

namespace BurcinCo.BurcinApp.Data;

/// <summary>
/// Extension point for cross-cutting concerns that need to participate in the DbContext options
/// build-up — interceptors, EF service replacements (e.g. <see cref="Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer"/>),
/// command-timeout overrides, etc. — without forcing this Data project to take a hard dependency
/// on the concern's library.
///
/// Implementations are resolved from DI by <c>AddBurcinDatabaseDbContext</c> and invoked in registration
/// order inside the <see cref="DbContextOptionsBuilder"/> callback. The classic example: the
/// reliable-messaging chain registers an <c>IDbContextConfigurer&lt;BurcinDatabaseDbContext&gt;</c> that
/// attaches the Outbox SaveChanges interceptor and replaces <c>IModelCustomizer</c> so the Outbox
/// + Inbox tables get added to the model — none of which Data needs to know about by name.
/// </summary>
public interface IDbContextConfigurer<TContext> where TContext : DbContext
{
	void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder);
}
