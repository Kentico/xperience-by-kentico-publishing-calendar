import { defineConfig, devices } from "@playwright/test";
import {
  adminStorageStatePath,
  appBaseUrl,
  webServerCommand,
  webServerTimeoutMs,
} from "./shared/config";

const isCi = !!process.env.CI;

export default defineConfig({
  testDir: ".",
  testIgnore: ["node_modules/**"],
  fullyParallel: true,
  forbidOnly: isCi,
  retries: isCi ? 2 : 0,
  workers: isCi ? 1 : undefined,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: appBaseUrl,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },

  projects: [
    // Refreshes the DancingGoat demo data (content states and dates relative to today) before any spec runs
    {
      name: "demo-data",
      testMatch: /[\\/]shared[\\/]demoData\.setup\.ts/,
    },
    {
      name: "live-site",
      use: { ...devices["Desktop Chrome"] },
      testMatch: /[\\/]live-site[\\/].*\.spec\.ts/,
      dependencies: ["demo-data"],
    },
    {
      name: "admin-setup",
      use: { ...devices["Desktop Chrome"] },
      testMatch: /[\\/]admin[\\/]auth\.setup\.ts/,
      dependencies: ["demo-data"],
    },
    {
      name: "admin-tests",
      use: {
        ...devices["Desktop Chrome"],
        storageState: adminStorageStatePath,
      },
      testMatch: /[\\/]admin[\\/].*\.spec\.ts/,
      dependencies: ["admin-setup"],
    },
  ],

  // Starts DancingGoat unless it is already running (locally). Readiness uses
  // /admin because the live site returns 500 until a license is installed.
  webServer: {
    command: webServerCommand,
    url: `${appBaseUrl}/admin`,
    timeout: webServerTimeoutMs,
    reuseExistingServer: !isCi,
    stdout: "pipe",
  },
});
