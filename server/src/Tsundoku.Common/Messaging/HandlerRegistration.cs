using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Identity;

namespace Tsundoku.Common.Messaging;

/// <summary>
/// Explicit DI for the use cases: no assembly scan and no mediator (tip 25.04).
/// Each registration writes its decorator chain with <c>new</c>, so you can see the order here and the compiler checks it.
/// </summary>
public static class HandlerRegistration
{
    /// <summary>
    /// The command pipeline: Logging → Validation → UnitOfWork → handler. <typeparamref name="TDbContext"/> is the
    /// module's context: the unit of work runs the command in a transaction of the current user and saves it once after a success.
    /// </summary>
    public static IServiceCollection AddCommand<TCommand, TResult, THandler, TDbContext>(this IServiceCollection services)
        where TCommand : ICommand<TResult>
        where TResult : notnull
        where THandler : class, ICommandHandler<TCommand, TResult>
        where TDbContext : DbContext =>
        services.AddCommandChain<TCommand, TResult, THandler, TDbContext>(provider => provider.GetRequiredService<ICurrentUser>().Id);

    /// <summary>
    /// <see cref="AddCommand{TCommand, TResult, THandler, TDbContext}"/> without a user, for sign-up and sign-in. Such a command may
    /// touch only tables without an owner policy: row-level security shows it no owned row and refuses its writes to one.
    /// </summary>
    public static IServiceCollection AddAnonymousCommand<TCommand, TResult, THandler, TDbContext>(this IServiceCollection services)
        where TCommand : ICommand<TResult>
        where TResult : notnull
        where THandler : class, ICommandHandler<TCommand, TResult>
        where TDbContext : DbContext =>
        services.AddCommandChain<TCommand, TResult, THandler, TDbContext>(_ => null);

    private static IServiceCollection AddCommandChain<TCommand, TResult, THandler, TDbContext>(
        this IServiceCollection services, Func<IServiceProvider, UserId?> user)
        where TCommand : ICommand<TResult>
        where TResult : notnull
        where THandler : class, ICommandHandler<TCommand, TResult>
        where TDbContext : DbContext
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResult>>(provider =>
            new LoggingDecorator.CommandHandler<TCommand, TResult>(
                new ValidationDecorator.CommandHandler<TCommand, TResult>(
                    new UnitOfWorkDecorator.CommandHandler<TCommand, TResult, TDbContext>(
                        provider.GetRequiredService<THandler>(),
                        provider.GetRequiredService<TDbContext>(),
                        user(provider)),
                    provider.GetService<IValidator<TCommand>>()),
                provider.GetRequiredService<ILoggerFactory>(),
                provider.GetRequiredService<AppMetrics>()));
        return services;
    }

    /// <summary>
    /// The query pipeline: Logging → Validation → QueryTransaction → handler. The query runs in a transaction of the current user,
    /// so row-level security applies to reads too.
    /// </summary>
    public static IServiceCollection AddQuery<TQuery, TResult, THandler, TDbContext>(this IServiceCollection services)
        where TQuery : IQuery<TResult>
        where TResult : notnull
        where THandler : class, IQueryHandler<TQuery, TResult>
        where TDbContext : DbContext
    {
        services.AddScoped<THandler>();
        services.AddScoped<IQueryHandler<TQuery, TResult>>(provider =>
            new LoggingDecorator.QueryHandler<TQuery, TResult>(
                new ValidationDecorator.QueryHandler<TQuery, TResult>(
                    new QueryTransactionDecorator.QueryHandler<TQuery, TResult, TDbContext>(
                        provider.GetRequiredService<THandler>(),
                        provider.GetRequiredService<TDbContext>(),
                        provider.GetRequiredService<ICurrentUser>().Id),
                    provider.GetService<IValidator<TQuery>>()),
                provider.GetRequiredService<ILoggerFactory>(),
                provider.GetRequiredService<AppMetrics>()));
        return services;
    }

    /// <summary>Validators have no state, so one instance serves the full app.</summary>
    public static IServiceCollection AddValidator<T, TValidator>(this IServiceCollection services)
        where TValidator : class, IValidator<T>
    {
        services.AddSingleton<IValidator<T>, TValidator>();
        return services;
    }
}
