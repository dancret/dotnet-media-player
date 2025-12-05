using MediaPlayer.NetCord.Player;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace MediaPlayer.NetCord.Modules;

internal static class DiscordCommandHelpers
{
    /// <summary>
    /// Attempts to retrieve a <see cref="NetCordDiscordPlayer"/> instance for the user in the specified context.
    /// </summary>
    /// <param name="context">
    /// The <see cref="ApplicationCommandContext"/> representing the command execution context.
    /// </param>
    /// <param name="provider">
    /// The <see cref="NetCordDiscordPlayerProvider"/> used to provide the player instance.
    /// </param>
    /// <returns>
    /// The result contains the <see cref="NetCordDiscordPlayer"/> instance
    /// if the user is in a voice channel; otherwise, <c>null</c>.
    /// </returns>
    public static async Task<NetCordDiscordPlayer?> TryGetPlayerForUserAsync(
        ApplicationCommandContext context,
        NetCordDiscordPlayerProvider provider)
    {
        var guild = context.Guild!;
        var user = context.User;

        if (!guild.VoiceStates.TryGetValue(user.Id, out var voiceState) ||
            voiceState.ChannelId is null)
        {
            return null;
        }

        return await provider.GetPlayerAsync(guild, voiceState);
    }

    /// <summary>
    /// Sets the content of the provided <see cref="MessageOptions"/> to a predefined message indicating a command execution failure.
    /// </summary>
    /// <param name="msg">
    /// The <see cref="MessageOptions"/> instance to update with the failure message.
    /// </param>
    public static void RespondForCommandFailure(MessageOptions msg)
    { 
        msg.Content = "Sorry, something went wrong running this command.";
    }

    /// <summary>
    /// Sets the response message indicating that the user must be in a voice channel to execute the command.
    /// </summary>
    /// <param name="msg">
    /// The <see cref="MessageOptions"/> used to modify the response message.
    /// </param>
    public static void RespondForNullPlayer(MessageOptions msg)
    {
        msg.Content = "You must be in a voice channel.";
    }
}
