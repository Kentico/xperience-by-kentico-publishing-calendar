import { expect, test } from "@playwright/test";
import { appBaseUrl } from "./config";

// Runs once (the `demo-data` project) before every other project: refreshes the DancingGoat demo data
// (examples/DancingGoat/DemoData, same endpoint as scripts/Update-DemoData.ps1) so content states and dates
// are centered on today. The refresh is deterministic, so specs can rely on every calendar status, items in
// each custom workflow step, activity today (the calendar's default day) and yesterday, and a busy day (two days
// ago) with more published items than the calendar shows without paging.
test("refresh demo data", async ({ request }) => {
  test.setTimeout(10 * 60_000);

  const response = await request.post(`${appBaseUrl}/api/demo-data/refresh`, {
    headers: { "X-Demo-Data-Refresh": "true" },
    timeout: 10 * 60_000,
  });

  expect(response.status(), await response.text()).toBe(200);
  console.log(`Demo data refreshed: ${JSON.stringify(await response.json())}`);
});
