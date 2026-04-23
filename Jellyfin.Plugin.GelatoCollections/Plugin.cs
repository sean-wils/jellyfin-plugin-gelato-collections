using System;
using System.Collections.Generic;
using Jellyfin.Plugin.GelatoCollections.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.GelatoCollections;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginGuid = new("a3f7c2d1-84e5-4b6f-9c3a-2d1e5f8b0a4c");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Gelato Collections";

    public override Guid Id => PluginGuid;

    public override string Description =>
        "Patches Gelato stub item GUIDs into BoxSet LinkedChildren so collection membership survives Jellyfin restarts.";

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        };
    }
}
