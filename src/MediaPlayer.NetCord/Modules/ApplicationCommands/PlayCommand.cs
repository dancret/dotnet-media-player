using MediaPlayer.NetCord.Player;
using MediaPlayer.Tracks;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class PlayCommand(
    ILogger<PlayCommand> logger,
    NetCordDiscordPlayerProvider playerProvider,
    ITrackResolver trackResolver)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("play", "Play youtube video or playlist", Contexts = [InteractionContextType.Guild])]
    public async Task Play(string url)
    {
        try
        {
            logger.LogInformation("{Command}: in {ChannelName} by {UserUsername} url {Url}.", 
                nameof(Play),
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
            
            var tracks = await trackResolver.ResolveAsync(trackRequest).ToListAsync();
            if (!tracks.Any())
            {
                await FollowupAsync(notFoundMessage);
                return;
            }
            
            await discordPlayer.EnqueueAsync(tracks);
            
            await FollowupAsync(tracks.Count > 1
                ? $"Added {tracks.Count.ToString()} tracks to queue."
                : $"Playing {tracks.First().Title}.");
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(Play));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }

}