using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediaPlayer.Tracks;

// ReSharper disable once UnusedMember.Global
/// <summary>
/// Provides extension methods for registering track resolvers with the dependency injection framework.
/// </summary>
public static class TrackResolverServiceCollectionExtensions
{
    public static IServiceCollection AddTrackResolvers(this IServiceCollection services)
    {
        services.AddSingleton<YouTubeTrackResolver>();
        services.AddSingleton<LocalFileTrackResolver>();

        // The public ITrackResolver is the routing one:
        services.AddSingleton<ITrackResolver>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RoutingTrackResolver>>();

            // Order matters: YouTube first, LocalFile last.
            var inner = new ITrackResolver[]
            {
                sp.GetRequiredService<YouTubeTrackResolver>(),
                sp.GetRequiredService<LocalFileTrackResolver>(),
            };

            return new RoutingTrackResolver(inner, logger);
        });

        return services;
    }
}
