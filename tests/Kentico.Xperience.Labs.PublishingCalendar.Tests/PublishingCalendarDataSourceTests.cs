using System.Globalization;

using Kentico.Xperience.Labs.PublishingCalendar.Admin;

namespace Kentico.Xperience.Labs.PublishingCalendar.Tests;

public class PublishingCalendarDataSourceTests
{
    [Test]
    public void BuildFilterClause_No_Filters_Returns_Empty() => Assert.Multiple(() =>
                                                                     {
                                                                         Assert.That(PublishingCalendarDataSource.BuildFilterClause(null), Is.Empty);
                                                                         Assert.That(PublishingCalendarDataSource.BuildFilterClause(new(null, [], null)), Is.Empty);
                                                                     });

    [Test]
    public void BuildFilterClause_ContentTypes_Distinct_Ids()
    {
        string clause = PublishingCalendarDataSource.BuildFilterClause(new([3, 1, 3], null, null));

        Assert.That(clause, Is.EqualTo(" AND ci.ContentItemContentTypeID IN (3,1)"));
    }

    [Test]
    public void BuildFilterClause_Channels_Includes_Content_Hub_As_Null_Channel()
    {
        string clause = PublishingCalendarDataSource.BuildFilterClause(
            new(null, [5, PublishingCalendarChannelOption.CONTENT_HUB_CHANNEL_ID], null));

        Assert.That(clause, Is.EqualTo(" AND (ci.ContentItemChannelID IN (5) OR ci.ContentItemChannelID IS NULL)"));
    }

    [Test]
    public void BuildFilterClause_Only_Content_Hub_Channel()
    {
        string clause = PublishingCalendarDataSource.BuildFilterClause(
            new(null, [PublishingCalendarChannelOption.CONTENT_HUB_CHANNEL_ID], null));

        Assert.That(clause, Is.EqualTo(" AND (ci.ContentItemChannelID IS NULL)"));
    }

    [Test]
    public void BuildFilterClause_Combines_Filters_With_And()
    {
        string clause = PublishingCalendarDataSource.BuildFilterClause(new([1], [2], [7, 8]));

        Assert.That(clause, Is.EqualTo(
            " AND ci.ContentItemContentTypeID IN (1)"
            + " AND (ci.ContentItemChannelID IN (2))"
            + " AND lm.ContentItemLanguageMetadataContentWorkflowStepID IN (7,8)"));
    }

    [Test]
    public void BuildFilterClause_Languages_Distinct_Ids()
    {
        string clause = PublishingCalendarDataSource.BuildFilterClause(new(null, null, null, [2, 1, 2]));

        Assert.That(clause, Is.EqualTo(" AND lm.ContentItemLanguageMetadataContentLanguageID IN (2,1)"));
    }

    [Test]
    public void BuildFilterClause_Empty_Languages_Returns_Empty() =>
        Assert.That(PublishingCalendarDataSource.BuildFilterClause(new(null, null, null, [])), Is.Empty);

    [Test]
    public void BuildFilterClause_Combines_Language_With_Other_Filters()
    {
        string clause = PublishingCalendarDataSource.BuildFilterClause(new([1], [2], null, [3]));

        Assert.That(clause, Is.EqualTo(
            " AND ci.ContentItemContentTypeID IN (1)"
            + " AND (ci.ContentItemChannelID IN (2))"
            + " AND lm.ContentItemLanguageMetadataContentLanguageID IN (3)"));
    }

    [Test]
    public void BuildEventsCte_Adds_Day_Values_And_Filters()
    {
        PublishingCalendarDay[] days =
        [
            new("2026-11-01", DateTimeOffset.Parse("2026-11-01T00:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-11-02T00:00:00Z", CultureInfo.InvariantCulture)),
            new("2026-11-02", DateTimeOffset.Parse("2026-11-02T00:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-11-03T00:00:00Z", CultureInfo.InvariantCulture)),
        ];

        string cte = PublishingCalendarDataSource.BuildEventsCte(days, new([4], null, null));

        Assert.Multiple(() =>
        {
            Assert.That(cte, Does.Contain("(VALUES (@DayKey0, @DayStart0, @DayEnd0), (@DayKey1, @DayStart1, @DayEnd1))"));
            Assert.That(cte, Does.Contain("AND ci.ContentItemContentTypeID IN (4)"));
            Assert.That(cte, Does.Not.Contain("{0}"));
            Assert.That(cte, Does.Not.Contain("{1}"));
        });
    }

    [Test]
    public void CreateBaseParameters_Uses_Server_Local_Range_And_Day_Parameters()
    {
        var start = DateTimeOffset.Parse("2026-11-01T00:00:00+02:00", CultureInfo.InvariantCulture);
        PublishingCalendarDay[] days =
        [
            new("2026-11-02", start.AddDays(1), start.AddDays(2)),
            new("2026-11-01", start, start.AddDays(1)),
        ];

        var parameters = PublishingCalendarDataSource.CreateBaseParameters(days);
        object? Value(string name) => parameters.Single(p => p.Name == name).Value;

        Assert.Multiple(() =>
        {
            Assert.That(Value("@RangeStart"), Is.EqualTo(start.LocalDateTime));
            Assert.That(Value("@RangeEnd"), Is.EqualTo(start.AddDays(2).LocalDateTime));
            Assert.That(Value("@DayKey0"), Is.EqualTo("2026-11-02"));
            Assert.That(Value("@DayStart1"), Is.EqualTo(start.LocalDateTime));
            Assert.That(Value("@DayEnd1"), Is.EqualTo(start.AddDays(1).LocalDateTime));
        });
    }

    [TestCase("Ann", "Lee", "alee", ExpectedResult = "Ann Lee")]
    [TestCase("Ann", "", "alee", ExpectedResult = "Ann")]
    [TestCase("", "", "alee", ExpectedResult = "alee")]
    public string FormatUserName_Prefers_Full_Name(string firstName, string lastName, string userName) =>
        PublishingCalendarDataSource.FormatUserName(firstName, lastName, userName);

    [Test]
    public void ToServerOffset_Treats_Value_As_Server_Local_Time()
    {
        var value = new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Unspecified);

        var result = PublishingCalendarDataSource.ToServerOffset(value);

        Assert.Multiple(() =>
        {
            Assert.That(result.DateTime, Is.EqualTo(value));
            Assert.That(result.Offset, Is.EqualTo(TimeZoneInfo.Local.GetUtcOffset(value)));
        });
    }

    private static readonly DateTimeOffset dayStart = DateTimeOffset.Parse("2026-11-01T00:00:00+01:00", CultureInfo.InvariantCulture);

    private static PublishingCalendarDay Day(int offset, string? key = null) =>
        new(key ?? $"day-{offset}", dayStart.AddDays(offset), dayStart.AddDays(offset + 1));

    [Test]
    public void ValidateDays_Keeps_Calendar_Month()
    {
        var days = Enumerable.Range(0, 42).Select(i => Day(i));

        Assert.That(PublishingCalendarDataSource.ValidateDays(days), Has.Length.EqualTo(42));
    }

    [Test]
    public void ValidateDays_Null_Or_Empty_Returns_None() => Assert.Multiple(() =>
    {
        Assert.That(PublishingCalendarDataSource.ValidateDays(null), Is.Empty);
        Assert.That(PublishingCalendarDataSource.ValidateDays([null]), Is.Empty);
    });

    [Test]
    public void ValidateDays_Drops_Invalid_Days()
    {
        PublishingCalendarDay[] days =
        [
            Day(0),
            Day(1, key: ""),
            new("reversed", dayStart.AddDays(1), dayStart),
            new("too-long", dayStart, dayStart.AddHours(27)),
            new("too-early", new DateTimeOffset(1899, 12, 31, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Day(2, key: "day-0"),
            Day(3),
        ];

        Assert.That(PublishingCalendarDataSource.ValidateDays(days).Select(d => d.Key), Is.EqualTo(new[] { "day-0", "day-3" }));
    }

    [Test]
    public void ValidateDays_Allows_Dst_Day() =>
        Assert.That(PublishingCalendarDataSource.ValidateDays([new("dst", dayStart, dayStart.AddHours(25))]), Has.Length.EqualTo(1));

    [Test]
    public void ValidateDays_Caps_Day_Count()
    {
        var days = Enumerable.Range(0, PublishingCalendarDataSource.MAX_DAYS + 10).Select(i => Day(i));

        Assert.That(PublishingCalendarDataSource.ValidateDays(days), Has.Length.EqualTo(PublishingCalendarDataSource.MAX_DAYS));
    }

    [Test]
    public void ValidateDays_Rejects_Range_Wider_Than_Max_Days() =>
        Assert.That(PublishingCalendarDataSource.ValidateDays([Day(0), Day(PublishingCalendarDataSource.MAX_DAYS + 1)]), Is.Empty);

    [Test]
    public void AreFiltersValid_Caps_Ids_Per_Filter()
    {
        int[] max = [.. Enumerable.Range(1, PublishingCalendarDataSource.MAX_FILTER_IDS)];
        int[] tooMany = [.. Enumerable.Range(1, PublishingCalendarDataSource.MAX_FILTER_IDS + 1)];

        Assert.Multiple(() =>
        {
            Assert.That(PublishingCalendarDataSource.AreFiltersValid(null), Is.True);
            Assert.That(PublishingCalendarDataSource.AreFiltersValid(new(max, max, max, max)), Is.True);
            Assert.That(PublishingCalendarDataSource.AreFiltersValid(new(null, null, null, tooMany)), Is.False);
            Assert.That(PublishingCalendarDataSource.AreFiltersValid(new(null, tooMany, null)), Is.False);
        });
    }

    [Test]
    public void EventStatus_All_Contains_Every_Status() => Assert.That(PublishingCalendarEventStatus.All, Is.EquivalentTo(new[]
        {
            "published", "scheduled-publish", "scheduled-unpublish", "in-workflow", "draft", "unpublished"
        }));
}
