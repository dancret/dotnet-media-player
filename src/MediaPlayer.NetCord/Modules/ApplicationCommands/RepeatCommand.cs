using MediaPlayer.NetCord.Player;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class RepeatCommand(
    ILogger<RepeatCommand> logger,
    NetCordDiscordPlayerProvider playerProvider)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("repeat", "Repeats all songs in the queue", Contexts = [InteractionContextType.Guild])]
    public async Task Repeat(bool repeat)
    {
        try
        {
            logger.LogInformation(
                "{Command}: {Repeat} in {ChannelName} by {UserUsername}.",
                nameof(Repeat),
                repeat,
                Context.Channel.ToString(),
                Context.User.Username);
            await RespondAsync(InteractionCallback.DeferredMessage());

            var discordPlayer = await DiscordCommandHelpers.TryGetPlayerForUserAsync(Context, playerProvider);
            if (discordPlayer is null)
            {
                await ModifyResponseAsync(DiscordCommandHelpers.RespondForNullPlayer);
                return;
            }

            discordPlayer.RepeatMode = repeat ? RepeatMode.All : RepeatMode.None;

            await FollowupAsync($"Repeat mode is {(repeat ? "on" : "off")}.");
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(Repeat));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }
}
