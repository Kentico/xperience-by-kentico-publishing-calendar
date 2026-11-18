import { expect, test } from "@playwright/test";
import { PublishingCalendarPage } from "../admin/publishingCalendarPage";
import { capture } from "../shared/screenshot";

// README screenshots of the Publishing calendar application, starting signed in. The `demo-data` project
// puts every status within a few days of today and a busy day (two days ago) with paged published items.

// Calendar items per day before paging (PublishingCalendarDataSource.PUBLISHED_ITEMS_PER_DAY)
const publishedItemsPerDay = 10;

test("month view with today's items", async ({ page }) => {
  const calendar = new PublishingCalendarPage(page);

  await calendar.openWithItems();
  await expect(calendar.panelItems().first()).toBeVisible();

  await capture(page, "publishing-calendar-month");
});

test("week view of the busy day", async ({ page }) => {
  const calendar = new PublishingCalendarPage(page);

  await calendar.openWithItems();
  await (
    await calendar.findDayStatusCount("published", publishedItemsPerDay)
  ).click();
  await calendar.switchView("week");
  await expect(calendar.weekItems().first()).toBeVisible();

  await capture(page, "publishing-calendar-week");
});

test("item details", async ({ page }) => {
  const calendar = new PublishingCalendarPage(page);

  await calendar.openWithItems();
  await calendar.dayStatusCounts().first().click();
  await calendar.panelItems().first().click();
  await expect(calendar.itemDetails()).toBeVisible();

  await capture(page, "publishing-calendar-item-details");
});
