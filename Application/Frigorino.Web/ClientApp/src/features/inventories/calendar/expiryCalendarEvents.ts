import type { ExpiryCalendarItemResponse } from "../../../lib/api";
import {
    getExpiryLevel,
    parseLocalDate,
    type ExpiryLevel,
} from "../../../utils/dateUtils";
import type { CalendarViewSettings } from "./calendarViewSettings";

export interface ExpiryEventProps {
    itemId: number;
    inventoryId: number;
    inventoryName: string;
    expiryDate: string;
    activeToday: boolean;
    // Bars spanning fewer than COMPACT_SPAN_DAYS are too narrow to render legible inline text
    // (notably expired 1-day markers). The page renders them name-only and opens a details
    // sheet on tap instead of the focus-select highlight.
    compact: boolean;
}

export interface ExpiryCalendarEvent {
    id: string;
    title: string;
    start: string; // YYYY-MM-DD, inclusive
    end: string; // YYYY-MM-DD, EXCLUSIVE (FullCalendar all-day convention)
    allDay: true;
    backgroundColor: string;
    borderColor: string;
    extendedProps: ExpiryEventProps;
}

const MS_PER_DAY = 1000 * 60 * 60 * 24;

// Bars shorter than this many days can't fit readable inline text → rendered name-only + tap-sheet.
const COMPACT_SPAN_DAYS = 4;

function toIsoDate(date: Date): string {
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function addDays(date: Date, days: number): Date {
    const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    copy.setDate(copy.getDate() + days);
    return copy;
}

function wholeDayDiff(from: Date, to: Date): number {
    return Math.round((to.getTime() - from.getTime()) / MS_PER_DAY);
}

// Maps inventory items (each with an expiry) to all-day cook-by bars, applying the user's
// view settings. Rules (see the view-settings design):
//   • Level filter: items whose ExpiryLevel is toggled off are dropped.
//   • Bars never extend into the past: barStart = max(expiry - windowDays, today).
//   • Full runway: barStart = today (bar spans the full remaining runway to expiry).
//   • Expired items (expiry < today) have no remaining window → a 1-day marker on the expiry date.
// FullCalendar's all-day `end` is EXCLUSIVE, so end = expiry + 1 covers the expiry day itself.
// `levelColor` keeps bar color in sync with the MUI theme; `todayIso` and `settings` are passed in
// so the mapping stays pure and deterministic.
export function buildExpiryEvents(
    items: ExpiryCalendarItemResponse[],
    todayIso: string,
    levelColor: (level: ExpiryLevel) => string,
    settings: CalendarViewSettings,
): ExpiryCalendarEvent[] {
    const today = parseLocalDate(todayIso);
    const events: ExpiryCalendarEvent[] = [];

    for (const item of items) {
        const expiry = parseLocalDate(item.expiryDate);
        const daysUntil = wholeDayDiff(today, expiry);
        const level = getExpiryLevel(daysUntil);

        if (!settings.levels[level]) {
            continue;
        }

        const color = levelColor(level);

        let windowStart: Date;
        if (daysUntil < 0) {
            // Already expired: 1-day marker on the actual expiry date, no backward tail.
            windowStart = expiry;
        } else if (settings.fullRunway) {
            windowStart = today;
        } else {
            const rawStart = addDays(expiry, -settings.windowDays);
            // Clamp the start to today so the bar never extends into the past.
            windowStart =
                rawStart.getTime() > today.getTime() ? rawStart : today;
        }

        const activeToday =
            wholeDayDiff(windowStart, today) >= 0 &&
            wholeDayDiff(today, expiry) >= 0;

        // Visible span in whole days, inclusive of both ends (a 1-day marker spans 1).
        const spanDays = wholeDayDiff(windowStart, expiry) + 1;
        const compact = spanDays < COMPACT_SPAN_DAYS;

        events.push({
            id: String(item.id),
            title: item.text,
            start: toIsoDate(windowStart),
            end: toIsoDate(addDays(expiry, 1)),
            allDay: true,
            backgroundColor: color,
            borderColor: color,
            extendedProps: {
                itemId: item.id,
                inventoryId: item.inventoryId,
                inventoryName: item.inventoryName,
                expiryDate: item.expiryDate,
                activeToday,
                compact,
            },
        });
    }

    return events;
}
