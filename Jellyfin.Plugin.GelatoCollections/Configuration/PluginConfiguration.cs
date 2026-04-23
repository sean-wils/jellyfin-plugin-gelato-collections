using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.GelatoCollections.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool EnablePatcher { get; set; } = true;

    public string PatchedPathPrefix { get; set; } = "gelato://";

    public bool RunRepairTaskOnStartup { get; set; } = true;
}
