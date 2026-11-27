using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Tsundoku.Common.Messaging;

/// <summary>
/// Explicit DI for the use cases: no assembly scan and no mediator (tip 25.04).
/// Each registration writes its decorator chain with <c>new</c>, so you can see the order here and the compiler checks it.
/// </summary>
public static class HandlerRegistration
{
    /// <summary>
    /// The command pipeline: Logging → Validation → UnitOfWork → handler. <typeparamref name="TDbContext"/> is the
    /// module's context: the unit of work saves it once after a success.
    /// </summary>
    public static IServiceCollection AddCommand<TCommand, TResult, THandler, TDbContext>(this IServiceCollection services)
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
                        provider.GetRequiredService<TDbContext>()),
                    provider.GetService<IValidator<TCommand>>()),
                provider.GetRequiredService<ILoggerFactory>(),
                provider.GetRequiredService<AppMetrics>()));
        return services;
    }

    /// <summary>The query pipeline: Logging → Validation → handler.</summary>
    public static IServiceCollection AddQuery<TQuery, TResult, THandler>(this IServiceCollection services)
        where TQuery : IQuery<TResult>
        where TResult : notnull
        where THandler : class, IQueryHandler<TQuery, TResult>
    {
        services.AddScoped<THandler>();
        services.AddScoped<IQueryHandler<TQuery, TResult>>(provider =>
            new LoggingDecorator.QueryHandler<TQuery, TResult>(
                new ValidationDecorator.QueryHandler<TQuery, TResult>(
                    provider.GetRequiredService<THandler>(),
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
