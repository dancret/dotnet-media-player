using MediaPlayer.NetCord.Player;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class QueueCommand(
    ILogger<QueueCommand> logger,
    NetCordDiscordPlayerProvider playerProvider)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    private const int MaxDisplayItems = 10;

    [SlashCommand("queue", "Get the next 10 tracks in the queue of the player",
        Contexts = [InteractionContextType.Guild])]
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

            var queue = discordPlayer.QueueSnapshot;

            if (!queue.Any())
            {
                await FollowupAsync("No song currently in the queue.");
            }
            else
            {
                var contentSb = new StringBuilder();
                contentSb.AppendLine("Current queue:");
                var index = 1;
                foreach (var item in queue.Take(MaxDisplayItems))
                {
                    contentSb.AppendLine($"  {index++}: {item.Title}");
                }

                if (queue.Count > MaxDisplayItems)
                {
                    contentSb.AppendLine($"({queue.Count - MaxDisplayItems} more tracks).");
                }

                var message = new InteractionMessageProperties
                {
                    Content = contentSb.ToString(),
                };

                await FollowupAsync(message);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(GetCurrentPlaying));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }
}