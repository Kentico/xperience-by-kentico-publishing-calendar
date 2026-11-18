# Demo data

Calendar is date-focused; DancingGoat content must be refreshed on demand so it is centered on today, spans months, and covers every status and workflow state.

## Decisions

- Users and local agents run the refresh as often as they want (demo, UI checks). CI runs it before every e2e run.
- Refresh changes runtime state only: it must not write to `App_Data/CIRepository` (wrap work in `CMSActionContext { ContinuousIntegrationAllowObjectSerialization = false }`).
- Permanent additions (authored once, committed via CI repository through the running site / management MCP):
  - 10–20 extra sensible DancingGoat content items (articles, coffees, etc.) for volume.
  - More workflows: at least one with 2+ custom steps, assigned to content types other than articles too.
- Refresh trigger: Development/CI-only minimal endpoint in DancingGoat; called by `scripts/Update-DemoData.ps1` and an Aspire `dancinggoat` resource command. CI `e2e` job calls the script after DancingGoat starts.

## Refresh behavior

- Deterministic: each variant's slot from a hash of `ContentItemGUID` + language → same layout every run, shifted to today.
- Past window (~-90..0 days): published, unpublished, draft, in-workflow (1–2+ items in each custom step). Set state via content APIs, then backdate `ContentItemLanguageMetadataModifiedWhen` with SQL.
- Future window (0..+60 days): scheduled publish and scheduled unpublish via `IContentItemManager` / `IWebPageManager` APIs (real scheduler publishes them).
- Busy days: a few days exceed the panel caps (10 published, "+more" in week view) so paging/"+more" e2e tests always run.
- Keep the live site usable: most items keep their published version and get a new draft / workflow step / scheduled publish on top (draft over published stays live). Only a few items take truly unpublished or never-published states; never the home page, navigation, or items the live-site e2e test uses.
- Content versioning disabled in DancingGoat (setting changed through the running site so CI stores it) so repeated API refreshes don't grow version history.
  - Revised (slice 2): `cms.settingskey` is excluded from the CI repository, so the setting can't be committed; the refresh suspends versioning in code (`ContentItemVersionActionContext { EnableStoreVersion = false }`).
- e2e: deterministic layout lets tests assert presence of every status, workflow step items, and busy days instead of skipping.
- Idempotent: running twice in a row gives the same result.
- Revised (slice 3): the calendar opens on today, which was empty (past ended at -1, future started at +1).
  - Past activity covers today: the hashed offset -1 (yesterday) maps to 0. Anchors (first item per state/step) for past states land on today, -2, or -3.
  - Yesterday: 3 published items taken from the busy-day pool (pool = 3 + 14, hash-ordered; first 3 → yesterday, rest → busy day -2). No other activity lands on yesterday.
  - No future activity: on the refresh day, a hashed time (08:00–17:59) later than the refresh (to the minute) is replaced by a hashed time in 08:00..now, or 00:00..now before 08:00. Deterministic per variant and refresh minute; times already past never move. States stay idempotent (second run changed = 0).
  - Scheduled publish/unpublish stay +1 day onward (a schedule for today publishes at once).
  - Summary adds `todayItems` / `yesterdayItems`.

## Slice log

1. Author workflows + extra items.
2. Refresh endpoint, script, Aspire command, CI step; tighten e2e tests.
3. Items today and yesterday (calendar's default day not empty); e2e asserts both.
