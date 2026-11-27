using Tsundoku.Common.Events;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Common;

/// <summary>The services that every module uses. The host calls it once, before the modules.</summary>
public static class CommonRegistration
{
    public static IHostApplicationBuilder AddCommon(this IHostApplicationBuilder builder)
    {
        // One clock for the whole app. Tests replace it with a fake clock.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddDomainEvents();
        // The host adds the meter factory.
        builder.Services.AddSingleton<AppMetrics>();
        return builder;
    }
}
