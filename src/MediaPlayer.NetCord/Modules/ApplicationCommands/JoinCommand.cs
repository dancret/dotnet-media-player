using MediaPlayer.NetCord.Player;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class JoinCommand(
    ILogger<JoinCommand> logger,
    NetCordDiscordPlayerProvider playerProvider)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("join", "Joins the user's current voice channel", Contexts = [InteractionContextType.Guild])]
    public async Task Join()
    {
        try
        {
            logger.LogInformation(
                "{Command}: in {ChannelName} by {UserUsername}.",
                nameof(Join),
                Context.Channel.ToString(),
                Context.User.Username);
            await RespondAsync(InteractionCallback.DeferredMessage());

            var player = await DiscordCommandHelpers.TryGetPlayerForUserAsync(Context, playerProvider);

            await ModifyResponseAsync(msg =>
            {
                msg.Content = player is null
                    ? "Couldn't join, make sure you are in a voice channel."
                    : "Joined voice channel.";
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(Join));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }
}
