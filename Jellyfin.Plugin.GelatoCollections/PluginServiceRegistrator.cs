using Jellyfin.Plugin.GelatoCollections.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.GelatoCollections;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHostedService<CollectionPatcherService>();
        serviceCollection.AddSingleton<BoxSetRepairScheduledTask>();
    }
}
