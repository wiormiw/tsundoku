using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tsundoku.Modules.Accounts.Features;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Api.Tests.Integration;

public sealed class LogEmailSenderTests
{
    private static async Task<string[]> Send(string environment)
    {
        var logs = new CapturingLoggerProvider();
        using var loggers = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var sender = new LogEmailSender(new Env(environment), loggers.CreateLogger<LogEmailSender>());
        await sender.SendConfirmationLinkAsync(new AccountUser(), "a@example.test", "http://localhost/api/auth/confirm-email?userId=1&code=SECRET");
        return [.. logs.Messages];
    }

    [Fact]
    public async Task Development_logs_the_link() => (await Send(Environments.Development)).ShouldContain(m => m.Contains("code=SECRET", StringComparison.Ordinal));

    [Fact]
    public async Task Any_other_environment_logs_neither_the_link_nor_the_address()
    {
        var messages = await Send(Environments.Production);

        messages.ShouldNotBeEmpty();
        messages.ShouldAllBe(m => !m.Contains("SECRET", StringComparison.Ordinal) && !m.Contains("example.test", StringComparison.Ordinal));
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = "";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
