using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.GelatoCollections.Services;

/// <summary>
/// Scheduled task that repairs all existing BoxSets by resolving any Gelato stub LinkedChild
/// entries that are missing their ItemId/LibraryItemId GUIDs. Runs on startup and daily at 04:00
/// to catch any entries the event handler missed (e.g. items added before the plugin was installed).
/// </summary>
public class BoxSetRepairScheduledTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<BoxSetRepairScheduledTask> _logger;

    public BoxSetRepairScheduledTask(ILibraryManager libraryManager, ILogger<BoxSetRepairScheduledTask> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public string Name => "Repair Gelato BoxSet Collections";

    public string Key => "GelatoCollectionsRepair";

    public string Description =>
        "Resolves Gelato stub item GUIDs for all BoxSet LinkedChildren so collections survive restarts.";

    public string Category => "Library";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        if (Plugin.Instance?.Configuration.RunRepairTaskOnStartup == true)
        {
            yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger };
        }

        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
        };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration.EnablePatcher != true)
        {
            _logger.LogInformation("Gelato Collections repair task skipped — patcher disabled");
            progress.Report(100);
            return;
        }

        var prefix = Plugin.Instance.Configuration.PatchedPathPrefix ?? "gelato://";

        var boxSets = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.BoxSet],
            Recursive = true
        });

        if (boxSets.Count == 0)
        {
            _logger.LogInformation("Gelato Collections repair: no BoxSets found");
            progress.Report(100);
            return;
        }

        _logger.LogInformation("Gelato Collections repair: scanning {Count} BoxSets", boxSets.Count);

        var totalPatched = 0;
        var boxSetList = boxSets.ToList();

        for (var idx = 0; idx < boxSetList.Count; idx++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (boxSetList[idx] is not Folder boxSet)
            {
                progress.Report((idx + 1) * 100.0 / boxSetList.Count);
                continue;
            }

            var children = boxSet.LinkedChildren;

            if (children is null || children.Length == 0)
            {
                progress.Report((idx + 1) * 100.0 / boxSetList.Count);
                continue;
            }

            var (newChildren, patched) = LinkedChildPatcher.Patch(
                children,
                prefix,
                _libraryManager,
                candidates: null,
                _logger,
                boxSet.Name);

            if (patched > 0)
            {
                boxSet.LinkedChildren = newChildren;
                await boxSet.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken)
                    .ConfigureAwait(false);
                totalPatched += patched;
            }

            progress.Report((idx + 1) * 100.0 / boxSetList.Count);
        }

        _logger.LogInformation(
            "Gelato Collections repair: patched {Total} LinkedChild entries across {Count} BoxSets",
            totalPatched,
            boxSetList.Count);
    }
}
