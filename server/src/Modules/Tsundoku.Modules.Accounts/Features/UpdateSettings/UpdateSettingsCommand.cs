using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.UpdateSettings;

internal sealed record UpdateSettingsCommand(string Timezone) : ICommand<SettingsResponse>;
