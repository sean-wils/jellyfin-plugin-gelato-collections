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

            if (lc.ItemId.HasValue && lc.ItemId.Value != Guid.Empty)
            {
                continue;
            }

            // First look in the caller-supplied candidate list (items just added to the collection).
            BaseItem? realItem = candidates?
                .FirstOrDefault(item => string.Equals(item.Path, lc.Path, StringComparison.OrdinalIgnoreCase));

            // Fallback: search the full library by IMDb ID extracted from the gelato:// path.
            if (realItem is null)
            {
                var imdbId = ExtractImdbId(lc.Path);
                if (imdbId is not null)
                {
                    realItem = libraryManager.GetItemList(new InternalItemsQuery
                    {
                        HasAnyProviderId = new Dictionary<string, string>
                        {
                            { MetadataProvider.Imdb.ToString(), imdbId }
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

            newChildren[i] = new LinkedChild
            {
                Path = lc.Path,
                ItemId = realItem.Id,
                LibraryItemId = realItem.Id.ToString("N"),
                Type = lc.Type
            };
            patched++;
        }

        return (newChildren, patched);
    }

    internal static string? ExtractImdbId(string path)
    {
        // gelato://stub/ttXXXXXXX  →  ttXXXXXXX
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return null;
        }

        var candidate = path[(lastSlash + 1)..];
        return candidate.StartsWith("tt", StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }
}
