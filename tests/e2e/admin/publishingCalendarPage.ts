import { expect, type Locator, type Page } from "@playwright/test";
import { adminBaseUrl } from "../shared/config";

// The publishing calendar registered by src/Kentico.Xperience.Labs.PublishingCalendar.Admin:
// UIPages/PublishingCalendar (application slug + calendar child page) and
// Client/src/publishing-calendar (React template). Uses data-testid hooks only.
const applicationSlug = "publishing-calendar";
const calendarPageSlug = "calendar";

export type PublishingCalendarView = "month" | "week" | "agenda";

export type PublishingCalendarFilterSection =
  "status" | "workflow-step" | "content-type" | "language" | "channel";

export class PublishingCalendarPage {
  constructor(private readonly page: Page) {}

  /**
   * Opens the application root, which redirects to its calendar child page, and waits for the first load.
   * Expands the filter panel (it starts collapsed on narrow viewports).
   */
  async open(): Promise<void> {
    await this.page.goto(`${adminBaseUrl}/${applicationSlug}`);
    await expect(this.page).toHaveURL(
      new RegExp(`/${applicationSlug}/${calendarPageSlug}`, "i"),
      {
        timeout: 30_000,
      },
    );
    await expect(this.root()).toBeVisible({ timeout: 30_000 });
    await this.waitForLoad();
    await this.setFiltersExpanded(true);
  }

  /**
   * Opens the calendar on the current month, which always has items: the `demo-data` setup project
   * (shared/demoData.setup.ts) puts every status within a few days of today.
   */
  async openWithItems(): Promise<void> {
    await this.open();
    await expect(this.dayStatusCounts().first()).toBeVisible();
  }

  /** Opens the side panel for a day (yyyy-mm-dd) in the current month view. */
  async selectDay(isoDate: string): Promise<void> {
    await this.dayCell(isoDate).click({ position: { x: 4, y: 40 } });
    await expect(this.dayCell(isoDate)).toHaveAttribute(
      "aria-selected",
      "true",
    );
    await expect(this.dayAgenda()).toHaveAttribute("data-date", isoDate);
    await this.waitForLoad();
  }

  /** Moves the view back one period when `isoDate` is not shown (e.g. yesterday on the 1st). */
  async showEarlierDay(isoDate: string): Promise<void> {
    if ((await this.dayCell(isoDate).count()) === 0) {
      await this.navigate(-1);
    }
  }

  /** Moves the month (or week) view back/forward by `offset` periods. */
  async navigate(offset: number): Promise<void> {
    const button = this.page.getByTestId(
      offset < 0 ? "publishing-calendar-prev" : "publishing-calendar-next",
    );
    for (let i = 0; i < Math.abs(offset); i++) {
      await button.click();
      await this.waitForLoad();
    }
  }

  /**
   * Legend totals per status summed over the previous, current, and next month. The demo data puts
   * every status within a few days of today, which can fall in an adjacent month.
   */
  async legendTotalsAroundToday(): Promise<Map<string, number>> {
    const totals = new Map<string, number>();
    for (const offset of [-1, 1, 1]) {
      await this.navigate(offset);
      for (const filter of await this.statusFilters().all()) {
        const status = (await filter.getAttribute("data-status")) ?? "";
        const count = Number(
          await filter
            .getByTestId("publishing-calendar-status-filter-count")
            .textContent(),
        );
        totals.set(status, (totals.get(status) ?? 0) + count);
      }
    }
    await this.navigate(-1);
    return totals;
  }

  /**
   * First month-cell badge of a status with more than `minCount` items, in the current month or, failing
   * that, the previous one (the demo data's busy day is two days before today).
   * Returns the cell's total badge instead when the cell is compact. When it moves to the previous month, the view stays there.
   */
  async findDayStatusCount(status: string, minCount: number): Promise<Locator> {
    for (const offset of [0, -1]) {
      await this.navigate(offset);
      const badges = this.page.locator(
        `[data-testid="publishing-calendar-day-status-count"][data-status="${status}"]`,
      );
      for (const badge of await badges.all()) {
        if (Number(await badge.getAttribute("data-count")) > minCount) {
          // A compact cell (too many statuses to fit) hides its status badges behind a total badge that selects the day
          return (await badge.isVisible())
            ? badge
            : badge
                .locator(
                  'xpath=ancestor::*[@data-testid="publishing-calendar-month-day"]',
                )
                .getByTestId("publishing-calendar-day-total-count");
        }
      }
    }
    throw new Error(
      `No day with more than ${minCount} "${status}" items this month or last. Run scripts/Update-DemoData.ps1.`,
    );
  }

  async waitForLoad(): Promise<void> {
    await expect(
      this.page.getByTestId("publishing-calendar-loading"),
    ).toHaveCount(0, {
      timeout: 30_000,
    });
  }

  root(): Locator {
    return this.page.getByTestId("publishing-calendar");
  }

  async switchView(view: PublishingCalendarView): Promise<void> {
    await this.page.getByTestId(`publishing-calendar-view-${view}`).click();
    await expect(this.root()).toHaveAttribute("data-view", view);
    await this.waitForLoad();
  }

  statusFilters(): Locator {
    return this.page.getByTestId("publishing-calendar-status-filter");
  }

  statusFilter(status: string): Locator {
    return this.page.locator(
      `[data-testid="publishing-calendar-status-filter"][data-status="${status}"]`,
    );
  }

  contentTypeFilter(): Locator {
    return this.page.getByTestId("publishing-calendar-content-type-filter");
  }

  /** Content languages, default first. */
  languageFilter(): Locator {
    return this.page.getByTestId("publishing-calendar-language-filter");
  }

  channelFilter(): Locator {
    return this.page.getByTestId("publishing-calendar-channel-filter");
  }

  /** Grouped by workflow; hidden when no workflows exist. */
  workflowStepFilter(): Locator {
    return this.page.getByTestId("publishing-calendar-workflow-step-filter");
  }

  workflowGroups(): Locator {
    return this.workflowStepFilter().getByTestId(
      "publishing-calendar-workflow-group",
    );
  }

  /** Collapsible filter panel section (status starts expanded, others collapsed). */
  filterSection(section: PublishingCalendarFilterSection): Locator {
    return this.page.locator(
      `[data-testid="publishing-calendar-filter-section"][data-section="${section}"]`,
    );
  }

  filterSectionToggle(section: PublishingCalendarFilterSection): Locator {
    return this.filterSection(section).getByTestId(
      "publishing-calendar-filter-section-toggle",
    );
  }

  /** Applied-filter count badge; only rendered while the section is collapsed. */
  filterSectionCount(section: PublishingCalendarFilterSection): Locator {
    return this.filterSection(section).getByTestId(
      "publishing-calendar-filter-section-count",
    );
  }

  /** Section Clear; only rendered while expanded, and not for status. */
  filterSectionClear(section: PublishingCalendarFilterSection): Locator {
    return this.filterSection(section).getByTestId(
      "publishing-calendar-filter-section-clear",
    );
  }

  async setFilterSectionExpanded(
    section: PublishingCalendarFilterSection,
    expanded: boolean,
  ): Promise<void> {
    const sectionLocator = this.filterSection(section);
    if (
      (await sectionLocator.getAttribute("data-expanded")) !== String(expanded)
    ) {
      await this.filterSectionToggle(section).click();
    }
    await expect(sectionLocator).toHaveAttribute(
      "data-expanded",
      String(expanded),
    );
  }

  /** Toggles the first option of a checkbox-list filter, expanding its section first. */
  async toggleFirstFilterOption(filter: Locator): Promise<void> {
    const section = filter.locator(
      "xpath=ancestor::*[@data-testid='publishing-calendar-filter-section'][1]",
    );
    if ((await section.getAttribute("data-expanded")) !== "true") {
      await section
        .getByTestId("publishing-calendar-filter-section-toggle")
        .click();
      await expect(section).toHaveAttribute("data-expanded", "true");
    }
    const testId = await filter.getAttribute("data-testid");
    await filter
      .getByTestId(`${testId}-option`)
      .first()
      .locator("label")
      .click();
    await this.waitForLoad();
  }

  /** Left filter panel (status toggles, content type, channel, workflow step). */
  filterPanel(): Locator {
    return this.page.getByTestId("publishing-calendar-filters");
  }

  filtersToggle(): Locator {
    return this.page.getByTestId("publishing-calendar-filters-toggle");
  }

  filtersActiveCount(): Locator {
    return this.page.getByTestId("publishing-calendar-filters-active-count");
  }

  clearFiltersButton(): Locator {
    return this.page.getByTestId("publishing-calendar-filters-clear");
  }

  async setFiltersExpanded(expanded: boolean): Promise<void> {
    const toggle = this.filtersToggle();
    if ((await toggle.getAttribute("aria-expanded")) !== String(expanded)) {
      await toggle.click();
    }
    await expect(toggle).toHaveAttribute("aria-expanded", String(expanded));
  }

  /** Month-grid day cells. */
  monthDays(): Locator {
    return this.page.getByTestId("publishing-calendar-month-day");
  }

  /** A month-grid day cell, by ISO date (yyyy-mm-dd). */
  dayCell(isoDate: string): Locator {
    return this.page.locator(
      `[data-testid="publishing-calendar-month-day"][data-date="${isoDate}"]`,
    );
  }

  /** Per-status count badges in month cells. Visible only: compact cells hide theirs behind a total badge. */
  dayStatusCounts(): Locator {
    return this.page
      .getByTestId("publishing-calendar-day-status-count")
      .filter({ visible: true });
  }

  /** Month cells too small for their status badges; each shows one total badge instead. */
  compactDays(): Locator {
    return this.page.locator(
      '[data-testid="publishing-calendar-month-day"][data-compact="true"]',
    );
  }

  /** Total count badges of compact month cells. */
  dayTotalCounts(): Locator {
    return this.page.getByTestId("publishing-calendar-day-total-count");
  }

  /**
   * Dates (yyyy-mm-dd) of month cells that overflow or contain a scrollable element. Cells clip their
   * content, so overflow = content larger than the cell.
   */
  async overflowingMonthDays(): Promise<string[]> {
    return this.monthDays().evaluateAll((cells) =>
      cells
        .filter(
          (cell) =>
            cell.scrollHeight > cell.clientHeight + 1 ||
            cell.scrollWidth > cell.clientWidth + 1 ||
            [cell, ...Array.from(cell.querySelectorAll("*"))].some((el) => {
              const style = getComputedStyle(el);
              return [style.overflowX, style.overflowY].some(
                (o) => o === "auto" || o === "scroll",
              );
            }),
        )
        .map((cell) => cell.getAttribute("data-date") ?? ""),
    );
  }

  weekDays(): Locator {
    return this.page.getByTestId("publishing-calendar-week-day");
  }

  weekItems(): Locator {
    return this.page.getByTestId("publishing-calendar-week-item");
  }

  weekMore(): Locator {
    return this.page.getByTestId("publishing-calendar-week-more");
  }

  agenda(): Locator {
    return this.page.getByTestId("publishing-calendar-agenda");
  }

  agendaItems(): Locator {
    return this.page.getByTestId("publishing-calendar-agenda-item");
  }

  agendaTimes(): Locator {
    return this.page.getByTestId("publishing-calendar-agenda-time");
  }

  panel(): Locator {
    return this.page.getByTestId("publishing-calendar-panel");
  }

  dayAgenda(): Locator {
    return this.panel().getByTestId("publishing-calendar-day");
  }

  panelGroups(): Locator {
    return this.panel().getByTestId("publishing-calendar-panel-group");
  }

  panelGroup(status: string): Locator {
    return this.panel().locator(
      `[data-testid="publishing-calendar-panel-group"][data-status="${status}"]`,
    );
  }

  panelItems(): Locator {
    return this.panel().getByTestId("publishing-calendar-day-item");
  }

  itemDetails(): Locator {
    return this.panel().getByTestId("publishing-calendar-item-details");
  }

  viewLink(): Locator {
    return this.panel().getByTestId("publishing-calendar-view-link");
  }
}
