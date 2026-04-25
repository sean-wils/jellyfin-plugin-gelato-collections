using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.GelatoCollections.Services;

internal static class LinkedChildPatcher
{
    /// <summary>
    /// Iterates <paramref name="children"/> and populates ItemId/LibraryItemId for any entry
    /// whose Path starts with <paramref name="prefix"/> and has no ItemId set.
    /// Returns the patched array and a count of entries that were updated.
    /// The caller is responsible for persisting the BoxSet if count > 0.
    /// </summary>
    internal static (LinkedChild[] PatchedChildren, int Count) Patch(
        LinkedChild[] children,
        string prefix,
        ILibraryManager libraryManager,
        IReadOnlyCollection<BaseItem>? candidates,
        ILogger logger,
        string boxSetName)
    {
        var newChildren = children.ToArray();
        var patched = 0;

        for (var i = 0; i < newChildren.Length; i++)
        {
            var lc = newChildren[i];

            if (string.IsNullOrEmpty(lc.Path)
                || !lc.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Already resolved: just clear the gelato:// path so Jellyfin's cleanup task
            // won't remove it (cleanup removes entries whose path can't be found on disk).
            if (lc.ItemId.HasValue && lc.ItemId.Value != Guid.Empty)
            {
                newChildren[i] = new LinkedChild
                {
                    Path = null,
                    ItemId = lc.ItemId,
                    LibraryItemId = lc.LibraryItemId,
                    Type = lc.Type
                };
                patched++;
                continue;
            }

            // First look in the caller-supplied candidate list (items just added to the collection).
            BaseItem? realItem = candidates?
                .FirstOrDefault(item => string.Equals(item.Path, lc.Path, StringComparison.OrdinalIgnoreCase));

            // Fallback: search the full library by provider ID extracted from the gelato:// path.
            // Gelato uses gelato://stub/ttXXXX (IMDb) or gelato://stub/tmdb:XXXX (TMDB).
            if (realItem is null)
            {
                var providerId = ExtractProviderId(lc.Path);
                if (providerId is not null)
                {
                    realItem = libraryManager.GetItemList(new InternalItemsQuery
                    {
                        HasAnyProviderId = new Dictionary<string, string>
                        {
                            { providerId.Value.Provider, providerId.Value.Id }
                        },
                        Recursive = true
                    }).FirstOrDefault();
                }
            }

            if (realItem is null)
            {
                logger.LogWarning(
                    "Gelato Collections: could not resolve stub path '{Path}' in BoxSet '{BoxSet}'",
                    lc.Path,
                    boxSetName);
                continue;
            }

            // Clear the gelato:// path: Jellyfin's cleanup task removes LinkedChildren
            // whose path can't be resolved on disk. Omitting the path (as Jellyfin does
            // for API-added collection members) lets cleanup validate by ItemId instead.
            newChildren[i] = new LinkedChild
            {
                Path = null,
                ItemId = realItem.Id,
                LibraryItemId = realItem.Id.ToString("N"),
                Type = lc.Type
            };
            patched++;
        }

        return (newChildren, patched);
    }

    internal static (string Provider, string Id)? ExtractProviderId(string path)
    {
        // gelato://stub/ttXXXXXXX   →  (Imdb, ttXXXXXXX)
        // gelato://stub/tmdb:XXXXX  →  (Tmdb, XXXXX)
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return null;
        }

        var segment = path[(lastSlash + 1)..];

        if (segment.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
        {
            return (MetadataProvider.Imdb.ToString(), segment);
        }

        if (segment.StartsWith("tmdb:", StringComparison.OrdinalIgnoreCase))
        {
            return (MetadataProvider.Tmdb.ToString(), segment[5..]);
        }

        return null;
    }
}
