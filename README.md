# jellyfin-plugin-gelato-collections

A lightweight Jellyfin plugin that fixes BoxSet collection membership for [Gelato](https://github.com/steff-ar/Gelato) virtual-item libraries.

## Problem

Gelato creates virtual library items with `gelato://stub/ttXXXX` paths. When these items are added to a BoxSet collection, Jellyfin stores only the `Path` field in `LinkedChild` — not the `ItemId`/`LibraryItemId` GUIDs. On restart, Jellyfin can't resolve `gelato://` paths and `LibraryItemId` is null, so all collection members are lost.

## Fix

This plugin hooks `ICollectionManager.ItemsAddedToCollection`, detects LinkedChild entries with `gelato://` paths, resolves the actual item GUID by matching on the IMDb ID embedded in the path, and patches both `ItemId` and `LibraryItemId` so entries survive restarts.

A scheduled task (`Repair Gelato BoxSet Collections`) handles pre-existing unpopulated entries and runs on startup plus daily at 04:00.

## Sunset

This plugin is a workaround. If [Jellyfin PR #16062](https://github.com/jellyfin/jellyfin/pull/16062) is merged, the server will handle this natively and this plugin can be removed.

## Installation

1. Download `Jellyfin.Plugin.GelatoCollections.dll` from [Releases](https://github.com/sean-wils/jellyfin-plugin-gelato-collections/releases).
2. Create `/config/plugins/GelatoCollections_0.1.0/` inside your Jellyfin data directory.
3. Copy the `.dll` and `build.yaml` (renamed to `meta.json`) into that directory.
4. Restart Jellyfin.
5. The `Repair Gelato BoxSet Collections` scheduled task will run automatically on startup.

## Configuration

Navigate to **Dashboard → Plugins → Gelato Collections** to:
- Enable/disable the patcher
- Enable/disable the startup repair task
- Change the Gelato path prefix (default: `gelato://`)

## Architecture

```
CollectionPatcherService   (IHostedService)
  └─ OnItemsAddedToCollection
       └─ LinkedChildPatcher.Patch()

BoxSetRepairScheduledTask  (IScheduledTask)
  └─ ExecuteAsync
       └─ LinkedChildPatcher.Patch()
```

Both components share `LinkedChildPatcher`, which:
1. Checks each LinkedChild for a `gelato://` path with a missing ItemId
2. Looks for the item in the caller-supplied candidate list first (fast path)
3. Falls back to a library search by IMDb ID extracted from the path
4. Sets `ItemId` and `LibraryItemId` on any resolved entries
