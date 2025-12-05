using MediaPlayer.NetCord;
using MediaPlayer.NetCord.Misc;
using MediaPlayer.NetCord.Player;
using MediaPlayer.Tracks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;

var builder = Host.CreateApplicationBuilder(args);


builder.Logging.ClearProviders();

builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Logging.AddSimpleConsole(options =>
{
    options.ColorBehavior = LoggerColorBehavior.Enabled;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    options.SingleLine = false;
});
#if !DEBUG
builder.Logging.AddLog4Net("log4net.config");
#endif

var services = builder.Services;

services.AddWindowsService();
services.AddSystemd();

services.AddEasyCaching(options =>
{
    options.UseInMemory("default");
});

services
    .ConfigureHttpClientDefaults(b => b.RemoveAllLoggers())
    .AddSingleton<ITrackRequestCache, EasyCachingCache>()
    .AddSingleton<ITrackResolver, YouTubeTrackResolver>()
    .AddSingleton<NetCordDiscordPlayerProvider>()
    .AddApplicationCommands()
    .AddDiscordGateway(options =>
    {
        options.Intents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates;
    })
    .AddHostedService<DiscordBotService>();

var host = builder
    .Build()
    .AddModules(typeof(Program).Assembly)
    .UseGatewayEventHandlers();

await host.RunAsync();
