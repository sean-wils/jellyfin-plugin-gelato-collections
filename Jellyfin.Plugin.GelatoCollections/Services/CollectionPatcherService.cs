using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.GelatoCollections.Services;

/// <summary>
/// Hooks ICollectionManager.ItemsAddedToCollection and patches any Gelato stub LinkedChild entries
/// that are missing their ItemId/LibraryItemId GUIDs. Without this patch, Gelato stub items
/// (gelato://stub/ttXXXX paths) disappear from BoxSets on every Jellyfin restart because
/// LinkedChild.Create sets only Path, and gelato:// paths cannot be resolved on startup.
/// </summary>
public class CollectionPatcherService : IHostedService
{
    private readonly ICollectionManager _collectionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<CollectionPatcherService> _logger;

    public CollectionPatcherService(
        ICollectionManager collectionManager,
        ILibraryManager libraryManager,
        ILogger<CollectionPatcherService> logger)
    {
        _collectionManager = collectionManager;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _collectionManager.ItemsAddedToCollection += OnItemsAddedToCollection;
        _logger.LogInformation("Gelato Collections patcher started — watching ItemsAddedToCollection");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _collectionManager.ItemsAddedToCollection -= OnItemsAddedToCollection;
        _logger.LogInformation("Gelato Collections patcher stopped");
        return Task.CompletedTask;
    }

    private void OnItemsAddedToCollection(object? sender, CollectionModifiedEventArgs args)
    {
        if (Plugin.Instance?.Configuration.EnablePatcher != true)
        {
            return;
        }

        var boxSet = args.Collection;
        if (boxSet is null)
        {
            return;
        }

        var children = boxSet.LinkedChildren;
        if (children is null || children.Length == 0)
        {
            return;
        }

        var prefix = Plugin.Instance.Configuration.PatchedPathPrefix ?? "gelato://";
        var (newChildren, patched) = LinkedChildPatcher.Patch(
            children,
            prefix,
            _libraryManager,
            args.ItemsChanged,
            _logger,
            boxSet.Name);

        if (patched == 0)
        {
            return;
        }

        boxSet.LinkedChildren = newChildren;
        boxSet.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Gelato Collections: patched {Count} LinkedChild entries in BoxSet '{BoxSet}'",
            patched,
            boxSet.Name);
    }
}
