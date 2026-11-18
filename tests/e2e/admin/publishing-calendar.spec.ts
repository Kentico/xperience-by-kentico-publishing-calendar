import { expect, type Locator, test } from "@playwright/test";
import { PublishingCalendarPage } from "./publishingCalendarPage";

// Publishing calendar admin application (UIPages/PublishingCalendar + Client/src/publishing-calendar).
// Asserts on data-testid / data-* hooks, not on captions or seeded content.
// The `demo-data` setup project refreshes DancingGoat's demo data first (examples/DancingGoat/DemoData):
// every status within a few days of today, at least two items in each custom workflow step, and a busy
// day (two days before today) with more published items than the calendar shows without paging.

const statuses = [
  "scheduled-publish",
  "scheduled-unpublish",
  "in-workflow",
  "draft",
  "published",
  "unpublished",
];

// Calendar shows this many published items per day before paging (PublishingCalendarDataSource.PUBLISHED_ITEMS_PER_DAY)
const publishedItemsPerDay = 10;

async function sumLegendCounts(
  calendar: PublishingCalendarPage,
): Promise<number> {
  const counts = await calendar
    .statusFilters()
    .getByTestId("publishing-calendar-status-filter-count")
    .allTextContents();
  return counts.reduce((sum, c) => sum + Number(c), 0);
}

/** The month cell containing a badge */
function cellOf(badge: Locator): Locator {
  return badge.locator(
    'xpath=ancestor::*[@data-testid="publishing-calendar-month-day"]',
  );
}

test.describe("publishing calendar", () => {
  test("every status has items around today", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.openWithItems();

    const totals = await calendar.legendTotalsAroundToday();

    for (const status of statuses) {
      expect(totals.get(status) ?? 0, status).toBeGreaterThan(0);
    }
  });

  test("status legend toggles filter all views", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.openWithItems();

    await expect(calendar.statusFilters()).toHaveCount(statuses.length);
    for (const status of statuses) {
      await expect(calendar.statusFilter(status)).toHaveAttribute(
        "aria-pressed",
        "true",
      );
    }

    const badge = calendar.dayStatusCounts().first();
    await expect(badge).toBeVisible();
    const status = (await badge.getAttribute("data-status")) ?? "";

    await calendar.statusFilter(status).click();
    await expect(calendar.statusFilter(status)).toHaveAttribute(
      "aria-pressed",
      "false",
    );
    await expect(
      page.locator(
        `[data-testid="publishing-calendar-day-status-count"][data-status="${status}"]`,
      ),
    ).toHaveCount(0);
  });

  test("filter panel collapses, remembers state, and clears filters", async ({
    page,
  }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();

    await expect(calendar.filterPanel()).toHaveAttribute(
      "data-collapsed",
      "false",
    );
    await expect(calendar.clearFiltersButton()).toBeDisabled();

    await calendar.statusFilter(statuses[0]).click();
    await expect(calendar.filterPanel()).toHaveAttribute(
      "data-active-count",
      "1",
    );
    await expect(calendar.clearFiltersButton()).toBeEnabled();

    // Collapsed rail keeps the active count, hides the filters
    await calendar.setFiltersExpanded(false);
    await expect(calendar.filterPanel()).toHaveAttribute(
      "data-collapsed",
      "true",
    );
    await expect(calendar.statusFilters().first()).toBeHidden();
    await expect(calendar.filtersActiveCount()).toBeVisible();

    // Collapsed state is remembered per viewer
    await page.reload();
    await expect(calendar.filtersToggle()).toHaveAttribute(
      "aria-expanded",
      "false",
    );

    await calendar.setFiltersExpanded(true);
    await calendar.toggleFirstFilterOption(calendar.channelFilter());
    await expect(calendar.filterPanel()).toHaveAttribute(
      "data-active-count",
      "1",
    );

    await calendar.clearFiltersButton().click();
    await calendar.waitForLoad();
    await expect(calendar.filterPanel()).toHaveAttribute(
      "data-active-count",
      "0",
    );
    await expect(calendar.channelFilter()).toHaveAttribute(
      "data-selected-count",
      "0",
    );
  });

  test("filter sections collapse, show applied count, and clear", async ({
    page,
  }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();

    // Default: status expanded, the rest collapsed
    await expect(calendar.filterSection("status")).toHaveAttribute(
      "data-expanded",
      "true",
    );
    await expect(calendar.statusFilters().first()).toBeVisible();
    for (const section of [
      "workflow-step",
      "content-type",
      "channel",
    ] as const) {
      await expect(calendar.filterSection(section)).toHaveAttribute(
        "data-expanded",
        "false",
      );
      await expect(calendar.filterSectionToggle(section)).toHaveAttribute(
        "aria-expanded",
        "false",
      );
    }
    await expect(calendar.contentTypeFilter()).toBeHidden();
    // Status has no section Clear
    await expect(calendar.filterSectionClear("status")).toHaveCount(0);

    // Collapsed status shows the hidden-status count
    await calendar.statusFilter(statuses[0]).click();
    await calendar.setFilterSectionExpanded("status", false);
    await expect(calendar.statusFilters().first()).toBeHidden();
    await expect(calendar.filterSectionCount("status")).toHaveAttribute(
      "data-count",
      "1",
    );
    await calendar.setFilterSectionExpanded("status", true);
    await calendar.statusFilter(statuses[0]).click();
    await calendar.waitForLoad();

    // Expanded with nothing applied: Clear unavailable
    await calendar.setFilterSectionExpanded("content-type", true);
    await expect(calendar.contentTypeFilter()).toBeVisible();
    await expect(calendar.filterSectionClear("content-type")).toBeDisabled();

    await calendar.toggleFirstFilterOption(calendar.contentTypeFilter());
    await expect(calendar.filterSection("content-type")).toHaveAttribute(
      "data-applied-count",
      "1",
    );
    // Badge only while collapsed
    await expect(calendar.filterSectionCount("content-type")).toHaveCount(0);

    await calendar.setFilterSectionExpanded("content-type", false);
    await expect(calendar.contentTypeFilter()).toBeHidden();
    await expect(calendar.filterSectionCount("content-type")).toHaveAttribute(
      "data-count",
      "1",
    );

    await calendar.setFilterSectionExpanded("content-type", true);
    await calendar.filterSectionClear("content-type").click();
    await calendar.waitForLoad();
    await expect(calendar.contentTypeFilter()).toHaveAttribute(
      "data-selected-count",
      "0",
    );
    await expect(calendar.filterPanel()).toHaveAttribute(
      "data-active-count",
      "0",
    );
    await expect(calendar.filterSectionClear("content-type")).toBeDisabled();
  });

  test("month cells show status counts and focus the panel group", async ({
    page,
  }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.openWithItems();

    const badge = calendar.dayStatusCounts().first();
    await expect(badge).toBeVisible();
    expect(Number(await badge.getAttribute("data-count"))).toBeGreaterThan(0);
    // Month cells are an overview: no item lists
    await expect(calendar.weekItems()).toHaveCount(0);
    await expect(calendar.agendaItems()).toHaveCount(0);

    const status = (await badge.getAttribute("data-status")) ?? "";
    const day = (await cellOf(badge).getAttribute("data-date")) ?? "";
    await badge.click();

    await expect(calendar.dayCell(day)).toHaveAttribute(
      "aria-selected",
      "true",
    );
    await expect(calendar.dayAgenda()).toHaveAttribute("data-date", day);
    await expect(calendar.panelGroup(status)).toHaveAttribute(
      "data-focused",
      "true",
    );
  });

  test("selecting a day shows that day in the side panel", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();

    const cell = calendar.monthDays().nth(10);
    const day = (await cell.getAttribute("data-date")) ?? "";
    await cell.click({ position: { x: 4, y: 40 } });

    await expect(cell).toHaveAttribute("aria-selected", "true");
    await expect(calendar.dayAgenda()).toHaveAttribute("data-date", day);
    await expect(calendar.itemDetails()).toHaveCount(0);
  });

  test("content type filter narrows counts", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();
    const before = await sumLegendCounts(calendar);

    await calendar.toggleFirstFilterOption(calendar.contentTypeFilter());

    await expect(calendar.contentTypeFilter()).toHaveAttribute(
      "data-selected-count",
      "1",
    );
    expect(await sumLegendCounts(calendar)).toBeLessThanOrEqual(before);
  });

  test("channel filter narrows counts", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();
    const before = await sumLegendCounts(calendar);

    await calendar.toggleFirstFilterOption(calendar.channelFilter());

    await expect(calendar.channelFilter()).toHaveAttribute(
      "data-selected-count",
      "1",
    );
    expect(await sumLegendCounts(calendar)).toBeLessThanOrEqual(before);
  });

  // DancingGoat has English (default) and Spanish (CIRepository @global/cms.contentlanguage); every variant has one language
  test("language filter narrows counts", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();
    const before = await sumLegendCounts(calendar);

    await calendar.setFilterSectionExpanded("language", true);
    const options = calendar
      .languageFilter()
      .getByTestId("publishing-calendar-language-filter-option");
    await expect(options).toHaveCount(2);

    await options.nth(0).locator("label").click();
    await calendar.waitForLoad();
    await expect(calendar.languageFilter()).toHaveAttribute(
      "data-selected-count",
      "1",
    );
    await expect(calendar.filterPanel()).toHaveAttribute(
      "data-active-count",
      "1",
    );
    const first = await sumLegendCounts(calendar);

    await options.nth(0).locator("label").click();
    await options.nth(1).locator("label").click();
    await calendar.waitForLoad();
    await expect(calendar.languageFilter()).toHaveAttribute(
      "data-selected-count",
      "1",
    );
    const second = await sumLegendCounts(calendar);

    expect(first).toBeLessThanOrEqual(before);
    expect(second).toBeLessThanOrEqual(before);
    expect(first + second).toBe(before);
  });

  // DancingGoat has a content workflow (CIRepository @global/cms.contentworkflow)
  test("workflow step filter groups steps by workflow and narrows counts", async ({
    page,
  }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();
    const before = await sumLegendCounts(calendar);

    await calendar.setFilterSectionExpanded("workflow-step", true);
    await expect(calendar.workflowStepFilter()).toBeVisible();
    await expect(calendar.workflowGroups().first()).toHaveAttribute(
      "data-workflow-id",
      /^\d+$/,
    );
    await expect(
      calendar
        .workflowGroups()
        .first()
        .getByTestId("publishing-calendar-workflow-step-filter-option")
        .first(),
    ).toBeVisible();

    await calendar.toggleFirstFilterOption(calendar.workflowStepFilter());

    await expect(calendar.workflowStepFilter()).toHaveAttribute(
      "data-selected-count",
      "1",
    );
    await expect(calendar.filterPanel()).toHaveAttribute(
      "data-active-count",
      "1",
    );
    expect(await sumLegendCounts(calendar)).toBeLessThanOrEqual(before);
  });

  // DancingGoat workflows: ArticleReview, ProductCopy, EventReview (2-3 custom steps each). Each group lists
  // the Draft step first and the Final step (scheduled items) last; the custom steps between them hold demo items.
  test("every custom workflow step has items", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();
    await calendar.setFilterSectionExpanded("workflow-step", true);

    const groups = await calendar.workflowGroups().all();
    expect(groups.length).toBeGreaterThanOrEqual(3);

    for (const group of groups) {
      const options = await group
        .getByTestId("publishing-calendar-workflow-step-filter-option")
        .all();
      expect(options.length).toBeGreaterThanOrEqual(3);

      for (const option of options.slice(1, -1)) {
        const count = Number(
          await option
            .getByTestId(
              "publishing-calendar-workflow-step-filter-option-count",
            )
            .textContent(),
        );
        expect(
          count,
          (await option.textContent()) ?? "",
        ).toBeGreaterThanOrEqual(2);

        // Filtering by the step shows its items around today
        await option.locator("label").click();
        await calendar.waitForLoad();
        const totals = await calendar.legendTotalsAroundToday();
        expect(
          totals.get("in-workflow") ?? 0,
          (await option.textContent()) ?? "",
        ).toBeGreaterThan(0);
        await option.locator("label").click();
        await calendar.waitForLoad();
      }
    }
  });

  test("week view lists items per day and +more opens that day in agenda", async ({
    page,
  }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.openWithItems();

    // Select the busy day so the week view opens on its week
    await (
      await calendar.findDayStatusCount("published", publishedItemsPerDay)
    ).click();
    await calendar.switchView("week");

    await expect(calendar.weekDays()).toHaveCount(7);
    await expect(calendar.weekItems().first()).toBeVisible();

    const more = calendar.weekMore().first();
    await expect(more).toBeVisible();
    const day =
      (await more
        .locator(
          'xpath=ancestor::*[@data-testid="publishing-calendar-week-day"]',
        )
        .getAttribute("data-date")) ?? "";
    await more.click();

    await expect(calendar.root()).toHaveAttribute("data-view", "agenda");
    await expect(calendar.agenda()).toHaveAttribute("data-focus-day", day);
    await expect(calendar.dayAgenda()).toHaveAttribute("data-date", day);
  });

  test("agenda shows real item times", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.openWithItems();
    await calendar.switchView("agenda");

    await expect(calendar.agendaItems().first()).toBeVisible();
    // ISO instant with a time part (not an all-day date)
    await expect(calendar.agendaTimes().first()).toHaveAttribute(
      "datetime",
      /T\d{2}:\d{2}/,
    );
  });

  test("item details link to the item", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.openWithItems();

    await calendar.dayStatusCounts().first().click();
    await calendar.panelItems().first().click();

    await expect(calendar.itemDetails()).toBeVisible();
    await expect(calendar.viewLink().locator("a")).toHaveAttribute(
      "href",
      /.+/,
    );
  });

  test("panel loads more items for a capped status", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.openWithItems();

    await (
      await calendar.findDayStatusCount("published", publishedItemsPerDay)
    ).click();
    const capped = calendar.panelGroup("published");
    await expect(
      capped.getByTestId("publishing-calendar-panel-load-more"),
    ).toBeVisible();

    const shown = Number(await capped.getAttribute("data-shown"));
    const total = Number(await capped.getAttribute("data-total"));
    expect(shown).toBeLessThan(total);

    await capped.getByTestId("publishing-calendar-panel-load-more").click();

    await expect
      .poll(async () => Number(await capped.getAttribute("data-shown")))
      .toBeGreaterThan(shown);
  });
});

/** Local date as yyyy-mm-dd (the calendar's day keys use the browser's local date). */
function dayKey(offset: number): string {
  const date = new Date();
  date.setDate(date.getDate() + offset);
  return [date.getFullYear(), date.getMonth() + 1, date.getDate()]
    .map((n) => String(n).padStart(2, "0"))
    .join("-");
}

// The demo data puts activity on today (the day the calendar opens on) and a few published items on yesterday
test.describe("publishing calendar demo data days", () => {
  test("opens on today with items in the side panel", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();

    await expect(calendar.dayCell(dayKey(0))).toHaveAttribute(
      "aria-selected",
      "true",
    );
    await expect(calendar.dayAgenda()).toHaveAttribute("data-date", dayKey(0));
    await expect(calendar.panelItems().first()).toBeVisible();
  });

  test("yesterday has items", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.open();

    const yesterday = dayKey(-1);
    await calendar.showEarlierDay(yesterday);
    await calendar.selectDay(yesterday);
    await expect(calendar.panelItems().first()).toBeVisible();
  });
});

// Month cells too small for their status badges show one total badge instead, and never scroll
test.describe("publishing calendar compact month cells", () => {
  test("default viewport month cells do not overflow", async ({ page }) => {
    const calendar = new PublishingCalendarPage(page);
    await calendar.openWithItems();

    expect(await calendar.overflowingMonthDays()).toEqual([]);
  });

  test.describe("small viewport", () => {
    test.use({ viewport: { width: 1024, height: 640 } });

    test("cells show a total badge instead of overflowing status badges", async ({
      page,
    }) => {
      const calendar = new PublishingCalendarPage(page);
      await calendar.open();

      const cell = calendar.compactDays().first();
      await expect(cell).toBeVisible();
      expect(await calendar.overflowingMonthDays()).toEqual([]);

      // One visible total badge; the status badges are hidden and add up to it
      await expect(calendar.dayTotalCounts()).toHaveCount(
        await calendar.compactDays().count(),
      );
      const total = cell.getByTestId("publishing-calendar-day-total-count");
      await expect(total).toBeVisible();
      await expect(total).toHaveAttribute("aria-label", /^\d+ items?$/);
      await expect(
        cell.getByTestId("publishing-calendar-day-status-count").first(),
      ).toBeHidden();
      const statusCounts = await cell
        .getByTestId("publishing-calendar-day-status-count")
        .evaluateAll((badges) =>
          badges.map((b) => Number(b.getAttribute("data-count"))),
        );
      expect(statusCounts.length).toBeGreaterThan(1);
      expect(Number(await total.getAttribute("data-count"))).toBe(
        statusCounts.reduce((sum, c) => sum + c, 0),
      );

      // The total badge selects its day
      const day = (await cell.getAttribute("data-date")) ?? "";
      await total.click();
      await expect(calendar.dayCell(day)).toHaveAttribute(
        "aria-selected",
        "true",
      );
      await expect(calendar.dayAgenda()).toHaveAttribute("data-date", day);
    });
  });
});
