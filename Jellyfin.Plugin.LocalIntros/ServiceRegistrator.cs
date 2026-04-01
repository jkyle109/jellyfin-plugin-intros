using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.LocalIntros;

public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Must explicitly register our background session listener so Jellyfin 10.9+ starts it
        serviceCollection.AddHostedService<IntroSessionManager>();
    }
}
