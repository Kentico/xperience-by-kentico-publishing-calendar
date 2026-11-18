# Usage Guide

## Installation

Add the admin package to the Xperience by Kentico application project (the one that hosts the administration).

```powershell
dotnet add package Kentico.Xperience.Labs.PublishingCalendar.Admin
```

No code changes are needed. Xperience discovers the package's admin module at startup and registers the application and its client module. Rebuild and restart the application, then sign in to the administration.

## Where it appears

The **Publishing calendar** application is in the **Content management** category of the administration (`/admin/publishing-calendar`).

## Permissions

The application uses the standard **View** permission. Administrators see it by default. For other roles, open **Role management**, select the role, and grant **View** for **Publishing calendar**.

The calendar doesn't apply workspace, channel, or page permissions. A role with **View** on the application sees titles, dates, workflow steps, and last editors of items in all workspaces and channels, including ones the role can't open. Editors opened from the calendar still check the role's permissions.

## What the calendar shows

A month, week, or agenda view of content item language variants across all channels (website, email, headless) and the Content hub. Each variant appears on the day of:

| Status | Date used |
| --- | --- |
| Scheduled publish | Scheduled publish date |
| Scheduled unpublish | Scheduled unpublish date |
| Published | Last modified date |
| In workflow | Last modified date (variant is in a custom workflow step) |
| Draft | Last modified date |
| Unpublished | Last modified date |

- Days follow the browser's time zone.
- Each day shows up to 10 published items; use **Load more** for the rest. Other statuses show up to 200 items per day.
- Filter by status, workflow step, content type, language, and channel (including **Content hub (reusable)** for items without a channel). Options within a filter are combined with OR; filters are combined with AND.
- In the month view, a day cell too small to fit its status counts shows one total count (for example, "5 items") instead. Select the day to see its items by status.
- Select a day or item to see details (content type, channel, workspace, language, workflow step, schedule, last modified by) with a link to the item's editor.
