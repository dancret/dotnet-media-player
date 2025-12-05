using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using System.Collections.Concurrent;

namespace MediaPlayer.NetCord.Player;

public sealed class NetCordDiscordPlayerProvider : IAsyncDisposable
{
    /// <summary>
    /// We will store all instances of players in a single instance of this provider, in memory.
    /// Players will be stored as a pair of voice channels and players, since only one voice channel use is allowed per guild, per bot.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, NetCordDiscordPlayer> _players = new();

    private readonly GatewayClient _gatewayClient;
    private readonly ILogger<NetCordDiscordPlayerProvider> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public NetCordDiscordPlayerProvider(
        ILogger<NetCordDiscordPlayerProvider> logger,
        ILoggerFactory loggerFactory,
        GatewayClient gatewayClient)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _gatewayClient = gatewayClient;

        _gatewayClient.VoiceStateUpdate += GatewayClientOnVoiceStateUpdate;
    }

    /// <summary>
    /// Auto-leaves when the last non-bot user leaves the bot's voice channel.
    /// </summary>
    private async ValueTask GatewayClientOnVoiceStateUpdate(VoiceState state)
    {
        try
        {
            // Get the guild from cache
            _gatewayClient.Cache.Guilds.TryGetValue(state.GuildId, out var guild);
            if (guild is null)
                return;

            // If the bot has no voice state in this guild, we’re not connected -> nothing to do
            if (!guild.VoiceStates.TryGetValue(_gatewayClient.Id, out var botVoiceState))
                return;

            // Bot isn't actually in a channel (shouldn't really happen, but be safe)
            var botChannelId = botVoiceState.ChannelId;
            if (botChannelId is null)
                return;

            // Ignore events about the bot's own voice state – we care about other users leaving
            if (state.UserId == _gatewayClient.Id)
                return;

            // Recalculate how many users are still in the same channel as the bot
            var usersInBotChannel = guild.VoiceStates.Values.Count(vs => vs.ChannelId == botChannelId);

            // At this point, guild.VoiceStates may still think the user is in botChannel.
            // If the *new* state says they LEFT or MOVED AWAY from botChannel,
            // manually subtract them from the count.
            if (guild.VoiceStates.TryGetValue(state.UserId, out var cachedUserVoiceState))
            {
                var cachedWasInBotChannel = cachedUserVoiceState.ChannelId == botChannelId;
                var nowInBotChannel = state.ChannelId == botChannelId;

                if (cachedWasInBotChannel && !nowInBotChannel)
                {
                    usersInBotChannel--;
                }
            }

            // If there is at least one non-bot user left, do nothing
            if (usersInBotChannel > 1)
                return;

            // Find the player that corresponds to this voice channel
            var player = _players.Values.SingleOrDefault(p => p.VoiceChannelId == botChannelId.Value);
            if (player is null)
                return;

            _logger.LogInformation(
                "No more listeners in guild {GuildId} / channel {ChannelId}, stopping and disposing player.",
                state.GuildId,
                botChannelId);

            // Stop playback and dispose player
            await player.DisposeAsync();

            // Update to latest voice channel status to show as left
            await _gatewayClient.UpdateVoiceStateAsync(new VoiceStateProperties(state.GuildId, state.ChannelId));

            foreach (var (key, value) in _players.ToArray())
            {
                if (!ReferenceEquals(value, player)) continue;

                _players.TryRemove(key, out _);
                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in GatewayClientOnVoiceStateUpdate (NetCord VoiceStateUpdate handler).");
        }
    }

    public async Task<NetCordDiscordPlayer> GetPlayerAsync(Guild guild, VoiceState voiceState)
    {
        var channelId = voiceState.ChannelId!.Value;

        if (_players.TryGetValue(channelId, out var existing))
        {
            return existing;
        }

        var voiceClient = await _gatewayClient.JoinVoiceChannelAsync(
            guild.Id,
            channelId);

        await voiceClient.StartAsync();

        var logger = _loggerFactory.CreateLogger<NetCordDiscordPlayer>();
        var player = new NetCordDiscordPlayer(channelId, voiceClient, logger, _loggerFactory);

        await player.InitializeAsync();
        _players[channelId] = player;

        return player;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var netCordDiscordPlayer in _players)
        {
            try
            {
                await netCordDiscordPlayer.Value.DisposeAsync();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to dispose players.");
            }
        }

        _gatewayClient.VoiceStateUpdate -= GatewayClientOnVoiceStateUpdate;
        _gatewayClient.Dispose();
        _loggerFactory.Dispose();
    }
}
