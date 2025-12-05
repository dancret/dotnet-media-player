using MediaPlayer.NetCord.Player;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class CurrentCommand(
    ILogger<CurrentCommand> logger,
    NetCordDiscordPlayerProvider playerProvider)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("current", "Get song title currently playing", Contexts = [InteractionContextType.Guild])]
    public async Task GetCurrentPlaying()
    {
        try
        {
            logger.LogInformation(
                "{Command}: in {ChannelName} by {UserUsername}.",
                nameof(GetCurrentPlaying),
                Context.Channel.ToString(),
                Context.User.Username);
            await RespondAsync(InteractionCallback.DeferredMessage());

            var discordPlayer = await DiscordCommandHelpers.TryGetPlayerForUserAsync(Context, playerProvider);
            if (discordPlayer is null)
            {
                await ModifyResponseAsync(DiscordCommandHelpers.RespondForNullPlayer);
                return;
            }

            var current = discordPlayer.CurrentSession;

            await FollowupAsync(current is null || current.State == PlayerState.Idle || current.State == PlayerState.Stopped
                ? "No song currently playing."
                : $"State: {current.State}. Track: {current.Track.Title}.");
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(GetCurrentPlaying));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }
}
