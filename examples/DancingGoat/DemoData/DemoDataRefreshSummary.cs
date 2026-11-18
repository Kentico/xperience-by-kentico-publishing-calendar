using System.Collections.Generic;

namespace DancingGoat.DemoData;

/// <summary>
/// Result of a demo data refresh. Every value except <see cref="Changed"/> is the same for runs on the same day.
/// </summary>
/// <param name="Today">Server local date the layout is relative to (yyyy-MM-dd).</param>
/// <param name="RangeStart">Earliest calendar date set (yyyy-MM-dd).</param>
/// <param name="RangeEnd">Latest calendar date set (yyyy-MM-dd).</param>
/// <param name="TodayItems">Variants with activity (last modification) today, the day the calendar opens on.</param>
/// <param name="YesterdayItems">Variants with activity yesterday.</param>
/// <param name="BusyDay">Day with more published items than the calendar shows without paging (yyyy-MM-dd).</param>
/// <param name="BusyDayPublished">Published items on <paramref name="BusyDay"/>.</param>
/// <param name="Variants">Language variants refreshed (pages, reusable items, emails).</param>
/// <param name="Changed">Variants whose state the run changed through the content APIs (0 when already refreshed today).</param>
/// <param name="Statuses">Calendar status counts after the refresh (activity entry per variant, plus scheduled unpublish entries).</param>
/// <param name="WorkflowSteps">Variants per custom workflow step ("Workflow / Step").</param>
public sealed record DemoDataRefreshSummary(
    string Today,
    string RangeStart,
    string RangeEnd,
    int TodayItems,
    int YesterdayItems,
    string BusyDay,
    int BusyDayPublished,
    int Variants,
    int Changed,
    IReadOnlyDictionary<string, int> Statuses,
    IReadOnlyDictionary<string, int> WorkflowSteps);
