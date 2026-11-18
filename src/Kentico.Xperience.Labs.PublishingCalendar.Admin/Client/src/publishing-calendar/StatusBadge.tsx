import {
  type Colors,
  SimpleStatusDefault,
  SimpleStatusSize,
} from "@kentico/xperience-admin-components";
import React from "react";
import {
  type PublishingCalendarEvent,
  getStatusIcon,
  getStatusLabel,
} from "./calendarModel";

/** Status color set by the publishing-calendar__status--* class on the wrapper */
// eslint-disable-next-line @typescript-eslint/no-unsafe-enum-assignment -- Colors only lists named tokens; a CSS variable lets the wrapper class pick the color
const statusColor = "var(--pc-fg)" as Colors;

/** Admin SimpleStatus (icon + label) in the item's status color */
export const StatusBadge = ({ event }: { event: PublishingCalendarEvent }) => {
  const label = getStatusLabel(event);
  return (
    <span
      className={`publishing-calendar__badge publishing-calendar__status--${event.status}`}
      data-testid="publishing-calendar-status-badge"
      data-status={event.status}
    >
      <SimpleStatusDefault
        size={SimpleStatusSize.XS}
        labelColor={statusColor}
        iconColor={statusColor}
        content={{ label, iconName: getStatusIcon(event), tooltipText: label }}
      />
    </span>
  );
};
