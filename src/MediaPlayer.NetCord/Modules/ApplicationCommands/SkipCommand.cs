using MediaPlayer.NetCord.Player;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class SkipCommand(
    ILogger<SkipCommand> logger,
    NetCordDiscordPlayerProvider playerProvider)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("skip", "Skips current song in the queue", Contexts = [InteractionContextType.Guild])]
    public async Task Skip()
    {
        try
        {
            logger.LogInformation(
                "{Command}: in {ChannelName} by {UserUsername}.",
                nameof(Skip),
                Context.Channel.ToString(),
                Context.User.Username);
            await RespondAsync(InteractionCallback.DeferredMessage());

            var discordPlayer = await DiscordCommandHelpers.TryGetPlayerForUserAsync(Context, playerProvider);
            if (discordPlayer is null)
            {
                await ModifyResponseAsync(DiscordCommandHelpers.RespondForNullPlayer);
                return;
            }

            await discordPlayer.SkipAsync();

            await FollowupAsync("Skipping current song.");
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(Skip));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }
}
