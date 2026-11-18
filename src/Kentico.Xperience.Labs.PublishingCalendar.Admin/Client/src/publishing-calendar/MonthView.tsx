import { Icon } from "@kentico/xperience-admin-components";
import React, { useCallback, useLayoutEffect, useRef, useState } from "react";
import {
  type CalendarData,
  type PublishingStatus,
  fromDayKey,
  getCount,
  getStatus,
  weekdayLabels,
} from "./calendarModel";

interface MonthViewProps {
  dayKeys: string[];
  month: number;
  data: CalendarData;
  statuses: PublishingStatus[];
  selectedDay: string;
  todayKey: string;
  OnSelectDay: (day: string, status?: PublishingStatus) => void;
}

const badgesSelector = ".publishing-calendar__month-badges";

/**
 * Days whose status chips (2 or more) do not fit their cell. The chips stay laid out (hidden) in
 * compact cells, so measuring them does not depend on the compact state and cannot oscillate.
 */
function measureCompactDays(grid: HTMLElement): Set<string> {
  const compact = new Set<string>();
  grid.querySelectorAll<HTMLElement>(badgesSelector).forEach((badges) => {
    const day = badges.dataset.day;
    if (
      day &&
      badges.childElementCount > 1 &&
      (badges.scrollHeight > badges.clientHeight + 1 ||
        badges.scrollWidth > badges.clientWidth + 1)
    ) {
      compact.add(day);
    }
  });
  return compact;
}

function sameSet(a: ReadonlySet<string>, b: ReadonlySet<string>): boolean {
  return a.size === b.size && [...a].every((v) => b.has(v));
}

/**
 * Compact cells: CSS cannot tell whether wrapped chips overflow (it depends on each day's chip
 * count and widths), so the grid is observed and each cell's chip area measured.
 */
function useCompactDays(dependencies: unknown[]) {
  const gridRef = useRef<HTMLDivElement>(null);
  const [compactDays, setCompactDays] = useState<ReadonlySet<string>>(
    () => new Set(),
  );

  const measure = useCallback(() => {
    const grid = gridRef.current;
    if (!grid) {
      return;
    }
    const next = measureCompactDays(grid);
    setCompactDays((prev) => (sameSet(prev, next) ? prev : next));
  }, []);

  // Caller passes the dependencies (the data that changes chip layout); measure is stable
  // eslint-disable-next-line react-hooks/exhaustive-deps -- dependencies come from the caller
  useLayoutEffect(measure, dependencies);

  useLayoutEffect(() => {
    const grid = gridRef.current;
    if (!grid || typeof ResizeObserver === "undefined") {
      return undefined;
    }
    let frame = 0;
    const observer = new ResizeObserver(() => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(measure);
    });
    observer.observe(grid);
    return () => {
      cancelAnimationFrame(frame);
      observer.disconnect();
    };
  }, [measure]);

  return { gridRef, compactDays };
}

/** High-level overview: per-status count badges per day, no item lists */
export const MonthView = ({
  dayKeys,
  month,
  data,
  statuses,
  selectedDay,
  todayKey,
  OnSelectDay: onSelectDay,
}: MonthViewProps) => {
  const { gridRef, compactDays } = useCompactDays([data, statuses, dayKeys]);

  return (
    <div
      className="publishing-calendar__month"
      role="grid"
      data-testid="publishing-calendar-month"
    >
      <div className="publishing-calendar__month-row" role="row">
        {weekdayLabels.map((label) => (
          <div
            key={label}
            className="publishing-calendar__weekday"
            role="columnheader"
          >
            {label}
          </div>
        ))}
      </div>
      <div className="publishing-calendar__month-days" ref={gridRef}>
        {dayKeys.map((day) => {
          const date = fromDayKey(day);
          const dayStatuses = statuses.filter(
            (s) => getCount(data, day, s) > 0,
          );
          const isCompact = compactDays.has(day);
          const total = dayStatuses.reduce(
            (sum, s) => sum + getCount(data, day, s),
            0,
          );
          const totalLabel = `${total} ${total === 1 ? "item" : "items"}`;
          const classNames = [
            "publishing-calendar__month-day",
            date.getMonth() !== month
              ? "publishing-calendar__month-day--other"
              : "",
            day === todayKey ? "publishing-calendar__month-day--today" : "",
            day === selectedDay ? "publishing-calendar__day--selected" : "",
            isCompact ? "publishing-calendar__month-day--compact" : "",
          ];
          return (
            <div
              key={day}
              className={classNames.filter(Boolean).join(" ")}
              role="gridcell"
              aria-selected={day === selectedDay}
              onClick={() => {
                onSelectDay(day);
              }}
              data-testid="publishing-calendar-month-day"
              data-date={day}
              data-compact={isCompact}
            >
              <div className="publishing-calendar__month-day-layout">
                <button
                  type="button"
                  className="publishing-calendar__month-day-number"
                  onClick={(e) => {
                    e.stopPropagation();
                    onSelectDay(day);
                  }}
                  aria-label={date.toLocaleDateString(undefined, {
                    weekday: "long",
                    month: "long",
                    day: "numeric",
                    year: "numeric",
                  })}
                >
                  {date.getDate()}
                </button>
                <div className="publishing-calendar__month-content">
                  <div
                    className="publishing-calendar__month-badges"
                    data-day={day}
                    aria-hidden={isCompact || undefined}
                  >
                    {dayStatuses.map((status) => {
                      const definition = getStatus(status);
                      const count = getCount(data, day, status);
                      return (
                        <button
                          key={status}
                          type="button"
                          className={`publishing-calendar__count-badge publishing-calendar__status--${status}`}
                          title={`${count} ${definition.label}`}
                          aria-label={`${count} ${definition.label}`}
                          tabIndex={isCompact ? -1 : undefined}
                          onClick={(e) => {
                            e.stopPropagation();
                            onSelectDay(day, status);
                          }}
                          data-testid="publishing-calendar-day-status-count"
                          data-status={status}
                          data-count={count}
                        >
                          <Icon name={definition.icon} />
                          {count}
                        </button>
                      );
                    })}
                  </div>
                  {isCompact && (
                    <button
                      type="button"
                      className="publishing-calendar__count-badge publishing-calendar__count-badge--total"
                      title={dayStatuses
                        .map(
                          (s) =>
                            `${getCount(data, day, s)} ${getStatus(s).label}`,
                        )
                        .join(", ")}
                      aria-label={totalLabel}
                      onClick={(e) => {
                        e.stopPropagation();
                        onSelectDay(day);
                      }}
                      data-testid="publishing-calendar-day-total-count"
                      data-count={total}
                    >
                      <Icon name="xp-list-bullets" />
                      {total}
                    </button>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
