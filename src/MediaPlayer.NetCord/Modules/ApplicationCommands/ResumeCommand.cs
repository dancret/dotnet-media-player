using MediaPlayer.NetCord.Player;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class ResumeCommand(
    ILogger<ResumeCommand> logger,
    NetCordDiscordPlayerProvider playerProvider)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("resume", "Resume the player", Contexts = [InteractionContextType.Guild])]
    public async Task Resume()
    {
        try
        {
            logger.LogInformation(
                "{Command}: in {ChannelName} by {UserUsername}.",
                nameof(Resume),
                Context.Channel.ToString(), 
                Context.User.Username);
            await RespondAsync(InteractionCallback.DeferredMessage());

            var discordPlayer = await DiscordCommandHelpers.TryGetPlayerForUserAsync(Context, playerProvider);
            if (discordPlayer is null)
            {
                await ModifyResponseAsync(DiscordCommandHelpers.RespondForNullPlayer);
                return;
            }

            await discordPlayer.ResumeAsync();

            await FollowupAsync("Player resumed.");
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(Resume));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }
}
