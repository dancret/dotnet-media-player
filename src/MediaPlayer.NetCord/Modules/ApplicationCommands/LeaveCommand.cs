using MediaPlayer.NetCord.Player;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Diagnostics.CodeAnalysis;
using NetCord.Gateway;

namespace MediaPlayer.NetCord.Modules.ApplicationCommands;

[SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called via reflection")]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Called via reflection")]
public class LeaveCommand(
    ILogger<LeaveCommand> logger,
    NetCordDiscordPlayerProvider playerProvider)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("leave", "Leaves the user's current voice channel", Contexts = [InteractionContextType.Guild])]
    public async Task Leave()
    {
        try
        {
            logger.LogInformation(
                "{Command}: in {ChannelName} by {UserUsername}.",
                nameof(Leave),
                Context.Channel.ToString(), 
                Context.User.Username);
            await RespondAsync(InteractionCallback.DeferredMessage());

            var guild = Context.Guild!;
            var user = Context.User;

            if (!guild.VoiceStates.TryGetValue(user.Id, out var voiceState) ||
                voiceState.ChannelId is null)
            {
                await FollowupAsync("User must be in a voice channel.");
                return;
            }

            var discordPlayer = await DiscordCommandHelpers.TryGetPlayerForUserAsync(Context, playerProvider);
            if (discordPlayer is null)
            {
                await ModifyResponseAsync(DiscordCommandHelpers.RespondForNullPlayer);
                return;
            }

            // Stop playback and dispose player
            await discordPlayer.DisposeAsync();

            // Update to the latest voice channel status to show as left
            await Context.Client.UpdateVoiceStateAsync(new VoiceStateProperties(voiceState.GuildId, voiceState.ChannelId));

            var channelId = voiceState.ChannelId.Value;

            var channelName = guild.Channels.TryGetValue(channelId, out var channel) ? channel.Name : channelId.ToString();

            await FollowupAsync($"Left channel {channelName}.");
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(Leave));
            await ModifyResponseAsync(DiscordCommandHelpers.RespondForCommandFailure);
        }
    }
}
