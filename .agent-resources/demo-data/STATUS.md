# Demo data – status

## Components

| Component | Where | State |
| --- | --- | --- |
| Workflows + extra items | `App_Data/CIRepository` | done (slice 1) |
| Refresh service + endpoint | `examples/DancingGoat/DemoData/` (`POST /api/demo-data/refresh`) | done (slice 2) |
| Script | `scripts/Update-DemoData.ps1` | done |
| Aspire command | `examples/AppHost/AppHost.cs` "Refresh demo data" (`WithHttpCommand`) | done; not yet clicked in a restarted AppHost |
| e2e | `tests/e2e/shared/demoData.setup.ts` (`demo-data` project, all projects depend on it); calendar specs no longer skip | done |
| CI | e2e job: Playwright starts DancingGoat, `demo-data` project refreshes | done; not yet run in GitHub Actions |
| Today / yesterday items (slice 3) | `DemoDataPlanner`: past offset -1 → today, no activity after the refresh time, 3 busy-pool items on yesterday; summary `todayItems` / `yesterdayItems`; e2e "publishing calendar demo data days" | done; verified locally (today 6 planned, yesterday 3, busy day 15 published, second run changed 0) |
| Content versioning | refresh suspends it in code (`ContentItemVersionActionContext`); setting `CMSContentVersionHistoryEnable` is `False` in the local DB; settings keys are excluded from the CI repository, so the admin setting can't be committed | done |

## Open checks

- First refresh after one rebuild hung (SQL lock waits around `CI_FileMetadata` during startup); not reproduced in later restarts. Watch the CI e2e job.
