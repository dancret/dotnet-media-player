using MediaPlayer.NetCord.Player;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;
using MediaPlayer.Tracks;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class PlayNowCommand(
    ILogger<PlayNowCommand> logger,
    NetCordDiscordPlayerProvider playerProvider,
    ITrackResolver trackResolver)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("playnow", "Play YouTube video NOW", Contexts = [InteractionContextType.Guild])]
    public async Task PlayNow(string url)
    {
        try
        {
            logger.LogInformation(
                "{Command}: in {ChannelName} by {UserUsername} url {Url}.",
                nameof(PlayNow),
                Context.Channel.ToString(), 
                Context.User.Username, url);
            await RespondAsync(InteractionCallback.DeferredMessage());

            var discordPlayer = await DiscordCommandHelpers.TryGetPlayerForUserAsync(Context, playerProvider);
            if (discordPlayer is null)
            {
                await ModifyResponseAsync(DiscordCommandHelpers.RespondForNullPlayer);
                return;
            }

            var trackRequest = new TrackRequest(url);
            var notFoundMessage = $"Sorry, I can't resolve '{url}'. Please check the URL or try a different one.";
            if (!trackResolver.CanResolve(trackRequest))
            {
                await FollowupAsync(notFoundMessage);
                return;
            }

            var track = await trackResolver.ResolveSingleOrDefaultAsync(trackRequest);
            if (track is null)
            {
                await FollowupAsync(notFoundMessage);
                return;
            }

            await discordPlayer.PlayNowAsync(track);

            await FollowupAsync($"Playing now {track.Title}.");
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(PlayNow));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }
}
