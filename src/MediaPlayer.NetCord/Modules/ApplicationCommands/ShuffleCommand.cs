using MediaPlayer.NetCord.Player;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class ShuffleCommand(
    ILogger<ShuffleCommand> logger,
    NetCordDiscordPlayerProvider playerProvider)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("shuffle", "Shuffles current queue", Contexts = [InteractionContextType.Guild])]
    public async Task Shuffle(bool shuffle)
    {
        try
        {
            logger.LogInformation(
                "{Command}: {Shuffle} in {ChannelName} by {UserUsername}.",
                nameof(Shuffle),
                shuffle,
                Context.Channel.ToString(), 
                Context.User.Username);
            await RespondAsync(InteractionCallback.DeferredMessage());

            var discordPlayer = await DiscordCommandHelpers.TryGetPlayerForUserAsync(Context, playerProvider);
            if (discordPlayer is null)
            {
                await ModifyResponseAsync(DiscordCommandHelpers.RespondForNullPlayer);
                return;
            }

            discordPlayer.Shuffle = shuffle;

            await FollowupAsync($"Shuffle mode is {(shuffle ? "on" : "off")}.");
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(Shuffle));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }
}
