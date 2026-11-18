using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using CMS.ContentEngine;

namespace DancingGoat.DemoData;

/// <summary>
/// Assigns every language variant a state and dates. Pure and deterministic: each variant's choices come from
/// a hash of its content item GUID and language, so every run gives the same layout, relative to <c>today</c>.
/// </summary>
/// <remarks>
/// Day rules: activity (last modification) dates fall on today or earlier; the calendar opens on today, so the
/// hashed past offset "yesterday" maps to today, and yesterday gets <see cref="YESTERDAY_ITEMS"/> published
/// items from the busy-day pool. Activity times are working hours (08:00–17:59) from the hash; on the refresh
/// day a time later than the refresh is replaced by an earlier one (see <see cref="NotAfter"/>). Scheduled
/// publish/unpublish dates fall from tomorrow on (a schedule for today would publish at once).
/// </remarks>
public static class DemoDataPlanner
{
    /// <summary>Activity dates fall within this many days before today.</summary>
    public const int PAST_DAYS = 90;

    /// <summary>Scheduled publish/unpublish dates fall within this many days after today.</summary>
    public const int FUTURE_DAYS = 60;

    /// <summary>Day (relative to today) that gets more published items than the calendar shows per day without paging.</summary>
    public const int BUSY_DAY_OFFSET = -2;

    /// <summary>Published items on the busy day; above the calendar's 10 per day, so the panel pages and the week view shows "+more".</summary>
    public const int BUSY_DAY_ITEMS = 14;

    /// <summary>Published items from the busy-day pool that land on yesterday instead (no other activity does).</summary>
    public const int YESTERDAY_ITEMS = 3;

    private const int UNPUBLISHED_ITEMS = 3;
    private const int SCHEDULED_UNPUBLISH_ITEMS = 5;
    private const int SCHEDULED_PUBLISH_PERCENT = 12;
    private const int DRAFT_PERCENT = 12;

    // Structural pages the live site (home, navigation, store, checkout) needs: they stay published
    private static readonly HashSet<string> publishedOnlyClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DancingGoat.HomePage",
        "DancingGoat.NavigationItem",
        "DancingGoat.ArticlesSection",
        "DancingGoat.ProductsSection",
        "DancingGoat.ProductCategory",
        "DancingGoat.Store",
        "DancingGoat.Checkout",
        "DancingGoat.ShoppingCart",
        "DancingGoat.ConfirmationPage",
        "DancingGoat.ContactsPage",
        "DancingGoat.PrivacyPage",
        "DancingGoat.LandingPage",
    };

    // Only leaf pages are ever unpublished (now or scheduled); reusable items may be linked from pages
    private static readonly HashSet<string> unpublishableClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DancingGoat.ArticlePage",
        "DancingGoat.ProductPage",
    };


    /// <summary>
    /// Plans the states and dates.
    /// </summary>
    /// <param name="variants">All language variants of pages, reusable items, and emails.</param>
    /// <param name="workflowSteps">Custom step code names of each workflow (by workflow code name), in step order.</param>
    /// <param name="today">Server local date the layout is relative to.</param>
    /// <param name="now">Server local time of the refresh; no activity time on its date is later.</param>
    public static IReadOnlyList<DemoDataAssignment> Plan(
        IEnumerable<DemoDataVariant> variants,
        IReadOnlyDictionary<string, IReadOnlyList<string>> workflowSteps,
        DateTime today,
        DateTime now)
    {
        var ordered = variants
            .OrderBy(v => Slot(v, "order", int.MaxValue))
            .ThenBy(v => v.MetadataId)
            .ToList();

        var states = new Dictionary<DemoDataVariant, (DemoDataState State, string Step)>();

        foreach (var variant in ordered.Where(v => !IsManaged(v)))
        {
            states[variant] = (DemoDataState.Unchanged, "");
        }

        var managed = ordered.Where(IsManaged).ToList();

        foreach (var (workflowName, steps) in workflowSteps.OrderBy(w => w.Key, StringComparer.Ordinal))
        {
            var eligible = managed
                .Where(v => string.Equals(v.WorkflowName, workflowName, StringComparison.OrdinalIgnoreCase)
                    && !publishedOnlyClassNames.Contains(v.ClassName))
                .ToList();
            int perStep = eligible.Count >= (steps.Count * 3) + 6 ? 3 : 2;

            int index = 0;
            foreach (string step in steps)
            {
                for (int i = 0; i < perStep && index < eligible.Count; i++)
                {
                    states[eligible[index++]] = (DemoDataState.InWorkflow, step);
                }
            }
        }

        var free = managed.Where(v => !states.ContainsKey(v)).ToList();

        var unpublishable = free.Where(v => unpublishableClassNames.Contains(v.ClassName)).ToList();
        foreach (var variant in unpublishable.Take(UNPUBLISHED_ITEMS))
        {
            states[variant] = (DemoDataState.Unpublished, "");
        }
        foreach (var variant in unpublishable.Skip(UNPUBLISHED_ITEMS).Take(SCHEDULED_UNPUBLISH_ITEMS))
        {
            states[variant] = (DemoDataState.ScheduledUnpublish, "");
        }

        foreach (var variant in free.Where(v => !states.ContainsKey(v)))
        {
            int bucket = Slot(variant, "state", 100);
            var state = DemoDataState.Published;
            if (!publishedOnlyClassNames.Contains(variant.ClassName))
            {
                if (bucket < SCHEDULED_PUBLISH_PERCENT)
                {
                    state = DemoDataState.ScheduledPublish;
                }
                else if (bucket < SCHEDULED_PUBLISH_PERCENT + DRAFT_PERCENT)
                {
                    state = DemoDataState.Draft;
                }
            }
            states[variant] = (state, "");
        }

        // The first variant of each state (and workflow step) lands within 3 days of today (past states: today, 2 or 3 days ago), so every status shows near the current date
        var anchors = states
            .GroupBy(s => (s.Value.State, s.Value.Step))
            .Select(g => g.Select(s => s.Key).OrderBy(v => Slot(v, "anchor", int.MaxValue)).ThenBy(v => v.MetadataId).First())
            .ToHashSet();

        // Busy-day pool: the first few go to yesterday, the rest to the busy day
        var pool = states
            .Where(s => s.Value.State == DemoDataState.Published && !anchors.Contains(s.Key))
            .Select(s => s.Key)
            .OrderBy(v => Slot(v, "busy", int.MaxValue))
            .ThenBy(v => v.MetadataId)
            .Take(YESTERDAY_ITEMS + BUSY_DAY_ITEMS)
            .ToList();
        var yesterday = pool.Take(YESTERDAY_ITEMS).ToHashSet();
        var busy = pool.Skip(YESTERDAY_ITEMS).ToHashSet();

        return [.. ordered.Select(v =>
        {
            int? fixedOffset = null;
            if (yesterday.Contains(v))
            {
                fixedOffset = -1;
            }
            else if (busy.Contains(v))
            {
                fixedOffset = BUSY_DAY_OFFSET;
            }

            return CreateAssignment(v, states[v].State, states[v].Step, today, now, anchors.Contains(v), fixedOffset);
        })];
    }


    /// <summary>Variants whose state the refresh changes: published (at least once) pages and reusable items.</summary>
    private static bool IsManaged(DemoDataVariant variant) =>
        variant.ContentTypeType is "Website" or "Reusable"
        && variant.LatestVersionStatus != VersionStatus.InitialDraft;


    private static DemoDataAssignment CreateAssignment(DemoDataVariant variant, DemoDataState state, string step, DateTime today, DateTime now, bool isAnchor, int? fixedOffset)
    {
        // Hashed offset -1..-days; -1 (yesterday) becomes 0 (today)
        DateTime Activity(int offset)
        {
            int dayOffset = offset == -1 ? 0 : offset;

            return NotAfter(At(today.AddDays(dayOffset), variant), now, variant);
        }

        DateTime Past(int days) => Activity(isAnchor ? -1 - Slot(variant, "near", 3) : -1 - Slot(variant, "day", days));
        DateTime Future(int days) => At(today.AddDays(isAnchor ? 1 + Slot(variant, "near", 3) : 1 + Slot(variant, "day", days)), variant);

        return state switch
        {
            DemoDataState.Published when fixedOffset.HasValue => new(variant, state, NotAfter(At(today.AddDays(fixedOffset.Value), variant), now, variant), null, ""),
            DemoDataState.Published => new(variant, state, Past(PAST_DAYS), null, ""),
            DemoDataState.Draft => new(variant, state, Past(45), null, ""),
            DemoDataState.InWorkflow => new(variant, state, Past(30), null, step),
            DemoDataState.Unpublished => new(variant, state, Past(60), null, ""),
            DemoDataState.ScheduledPublish => new(variant, state, Activity(-1 - Slot(variant, "modified", 14)), Future(45), ""),
            DemoDataState.ScheduledUnpublish => new(variant, state, Activity(-1 - Slot(variant, "modified", PAST_DAYS)), Future(FUTURE_DAYS), ""),
            _ => new(variant, state, Past(PAST_DAYS), null, ""),
        };
    }


    /// <summary>Working-hours time of day (08:00–17:59) for a date.</summary>
    private static DateTime At(DateTime day, DemoDataVariant variant) =>
        day.Date.AddMinutes((8 * 60) + Slot(variant, "time", 600));


    /// <summary>
    /// Keeps an activity time on the refresh day from being later than the refresh (<paramref name="now"/>, to the minute).
    /// A later time is replaced by a hashed one between 08:00 and now, or between 00:00 and now when the refresh runs
    /// before 08:00. Times already past stay as they are, so later runs the same day move only the items still ahead of now.
    /// </summary>
    private static DateTime NotAfter(DateTime value, DateTime now, DemoDataVariant variant)
    {
        var limit = now.Date.AddMinutes(Math.Floor(now.TimeOfDay.TotalMinutes));
        if (value <= limit || value.Date != limit.Date)
        {
            return value;
        }

        var start = limit.TimeOfDay >= TimeSpan.FromHours(8) ? limit.Date.AddHours(8) : limit.Date;

        return start.AddMinutes(Slot(variant, "time", (int)(limit - start).TotalMinutes + 1));
    }


    /// <summary>Stable number in [0, modulo) for a variant and a purpose.</summary>
    internal static int Slot(DemoDataVariant variant, string purpose, int modulo)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{variant.Key}|{purpose}"));

        return (int)(BinaryPrimitives.ReadUInt64LittleEndian(hash) % (ulong)modulo);
    }
}
