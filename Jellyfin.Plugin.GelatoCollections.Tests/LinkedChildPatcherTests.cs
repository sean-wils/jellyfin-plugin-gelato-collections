using System;
using System.Collections.Generic;
using Jellyfin.Plugin.GelatoCollections.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.GelatoCollections.Tests;

public class LinkedChildPatcherTests
{
    private const string Prefix = "gelato://";
    private static readonly NullLogger Logger = NullLogger.Instance;

    // T1: A single gelato LinkedChild gets ItemId and LibraryItemId populated.
    [Fact]
    public void Patch_SingleGelatoEntry_PopulatesItemIdAndLibraryItemId()
    {
        var itemId = Guid.NewGuid();
        var imdbId = "tt1234567";
        var path = $"gelato://stub/{imdbId}";

        var children = new[]
        {
            new LinkedChild { Path = path, Type = LinkedChildType.Manual }
        };

        var libraryItem = new Movie { Id = itemId };
        libraryItem.SetProviderId(MetadataProvider.Imdb, imdbId);

        var libraryManager = MockLibraryManager(imdbId, libraryItem);

        var (patched, count) = LinkedChildPatcher.Patch(children, Prefix, libraryManager, null, Logger, "Test");

        Assert.Equal(1, count);
        Assert.Equal(itemId, patched[0].ItemId);
        Assert.Equal(itemId.ToString("N"), patched[0].LibraryItemId);
        Assert.Null(patched[0].Path);
    }

    // T2: Mixed gelato + filesystem LinkedChildren — only the gelato entry is touched.
    [Fact]
    public void Patch_MixedChildren_OnlyPatchesGelatoEntries()
    {
        var itemId = Guid.NewGuid();
        var imdbId = "tt9999999";
        var gelatoPath = $"gelato://stub/{imdbId}";
        var fsPath = "/data/media/movies/SomeMovie (2020)/SomeMovie.mkv";
        var fsItemId = Guid.NewGuid();

        var children = new[]
        {
            new LinkedChild { Path = gelatoPath, Type = LinkedChildType.Manual },
            new LinkedChild { Path = fsPath, ItemId = fsItemId, LibraryItemId = fsItemId.ToString("N"), Type = LinkedChildType.Manual }
        };

        var libraryItem = new Movie { Id = itemId };
        libraryItem.SetProviderId(MetadataProvider.Imdb, imdbId);

        var libraryManager = MockLibraryManager(imdbId, libraryItem);

        var (patched, count) = LinkedChildPatcher.Patch(children, Prefix, libraryManager, null, Logger, "Test");

        Assert.Equal(1, count);
        Assert.Equal(itemId, patched[0].ItemId);
        // Filesystem entry unchanged
        Assert.Equal(fsItemId, patched[1].ItemId);
    }

    // T3: A gelato LinkedChild that already has ItemId set has its Path cleared (no library lookup).
    [Fact]
    public void Patch_AlreadyPopulatedItemId_ClearsGelatoPath()
    {
        var existingId = Guid.NewGuid();
        var path = "gelato://stub/tt0000001";

        var children = new[]
        {
            new LinkedChild { Path = path, ItemId = existingId, LibraryItemId = existingId.ToString("N"), Type = LinkedChildType.Manual }
        };

        var libraryManager = new Mock<ILibraryManager>();

        var (patched, count) = LinkedChildPatcher.Patch(children, Prefix, libraryManager.Object, null, Logger, "Test");

        Assert.Equal(1, count);
        Assert.Equal(existingId, patched[0].ItemId);
        Assert.Null(patched[0].Path);
        libraryManager.Verify(
            m => m.GetItemList(It.IsAny<InternalItemsQuery>()),
            Times.Never);
    }

    // T4: An unresolvable gelato path logs a warning, leaves the entry unchanged, and does not throw.
    [Fact]
    public void Patch_UnresolvablePath_WarnsAndLeavesEntryUnchanged()
    {
        var path = "gelato://stub/tt0000404";

        var children = new[]
        {
            new LinkedChild { Path = path, Type = LinkedChildType.Manual }
        };

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(new List<BaseItem>().AsReadOnly());

        var (patched, count) = LinkedChildPatcher.Patch(children, Prefix, libraryManager.Object, null, Logger, "Test");

        Assert.Equal(0, count);
        Assert.Null(patched[0].ItemId);
    }

    // Helper

    private static ILibraryManager MockLibraryManager(string imdbId, BaseItem item)
    {
        var mock = new Mock<ILibraryManager>();
        mock.Setup(m => m.GetItemList(It.Is<InternalItemsQuery>(q =>
                q.HasAnyProviderId != null &&
                q.HasAnyProviderId.ContainsKey(MetadataProvider.Imdb.ToString()) &&
                q.HasAnyProviderId[MetadataProvider.Imdb.ToString()] == imdbId)))
            .Returns(new List<BaseItem> { item }.AsReadOnly());
        return mock.Object;
    }

    // T5: TMDB-format path (gelato://stub/tmdb:XXXX) is resolved correctly.
    [Fact]
    public void Patch_TmdbFormatPath_PopulatesItemId()
    {
        var itemId = Guid.NewGuid();
        var tmdbId = "155";
        var path = $"gelato://stub/tmdb:{tmdbId}";

        var children = new[]
        {
            new LinkedChild { Path = path, Type = LinkedChildType.Manual }
        };

        var libraryItem = new Movie { Id = itemId };
        libraryItem.SetProviderId(MetadataProvider.Tmdb, tmdbId);

        var mock = new Mock<ILibraryManager>();
        mock.Setup(m => m.GetItemList(It.Is<InternalItemsQuery>(q =>
                q.HasAnyProviderId != null &&
                q.HasAnyProviderId.ContainsKey(MetadataProvider.Tmdb.ToString()) &&
                q.HasAnyProviderId[MetadataProvider.Tmdb.ToString()] == tmdbId)))
            .Returns(new List<BaseItem> { libraryItem }.AsReadOnly());

        var (patched, count) = LinkedChildPatcher.Patch(children, Prefix, mock.Object, null, Logger, "Test");

        Assert.Equal(1, count);
        Assert.Equal(itemId, patched[0].ItemId);
    }
}
