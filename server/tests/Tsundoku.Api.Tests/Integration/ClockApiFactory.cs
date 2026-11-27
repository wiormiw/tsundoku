using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>
/// The API with its own containers and a <see cref="ManualTimeProvider"/> as its <see cref="TimeProvider"/>, which the bearer handler
/// and the use cases read. A class fixture, so moving the clock never reaches another test class.
/// </summary>
public sealed class ClockApiFactory : ApiFactory
{
    public ManualTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Every log line of the app, to check the reasons that the wire does not tell.</summary>
    internal CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddLogging(logging => logging.AddProvider(Logs));
        });
    }
}
